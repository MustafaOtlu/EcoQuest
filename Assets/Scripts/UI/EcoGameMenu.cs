using UnityEngine;
using UnityEngine.InputSystem;

// GDD §13/§14: title screen at start (new game / continue / slots / settings) and the pause menu with the same
// screens. The shared clock (EcoUIFlow) freezes the world while a menu is open. Settings cover UI scale, colour-blind
// palette, shake/blur, hold-or-toggle tool button, audio buses and key rebinding.
[DefaultExecutionOrder(-60)] // reads Esc before PlayerController toggles pause
public sealed class EcoGameMenu : MonoBehaviour
{
    private enum Screen_ { Pause, Main, Slots, Settings, Keys }
    private Screen_ screen = Screen_.Pause;
    private Screen_ back = Screen_.Pause, root = Screen_.Pause;
    private bool opened;
    private EcoWorldSave.SlotInfo[] slots = new EcoWorldSave.SlotInfo[0];
    private float nextSlotScan;
    private int listening = -1;
    private Vector2 scroll;
    private string keyNotice = "";

    private void Start()
    {
        if (GetComponent<EcoSurvey>() == null) gameObject.AddComponent<EcoSurvey>();
        if (GetComponent<EcoPerf>() == null) gameObject.AddComponent<EcoPerf>();
        if (GetComponent<EcoCameraFx>() == null) gameObject.AddComponent<EcoCameraFx>();
        Invoke(nameof(OpenTitle), 0.3f);
    }
    private void OpenTitle()
    {
        if (opened) return; opened = true;
        screen = Screen_.Main; PlayerController.SetPaused(true);
    }

    // Esc steps back one screen (Settings/Slots/Keys → where they were opened from). On the title screen it does nothing:
    // previously it unpaused straight into an unchosen world, skipping "Yeni oyun" / "Devam et".
    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || !PlayerController.Paused || EcoUIFlow.EscapeConsumed) return;
        if (EcoSurvey.Current != null && EcoSurvey.Current.IsOpen) return;
        if (screen == Screen_.Keys && listening >= 0) { listening = -1; EcoUIFlow.ConsumeEscape(); return; }
        if (screen == Screen_.Slots || screen == Screen_.Settings || screen == Screen_.Keys) { Back(); EcoUIFlow.ConsumeEscape(); }
        else if (screen == Screen_.Main) EcoUIFlow.ConsumeEscape();
    }

    private void OnGUI()
    {
        if (!PlayerController.Paused) { if (screen != Screen_.Pause) screen = Screen_.Pause; listening = -1; return; }
        if (EcoSurvey.Current != null && EcoSurvey.Current.IsOpen) return; // the survey opened from here sits on top; the menu returns when it closes
        EcoTheme.Begin();
        string heading = screen == Screen_.Main ? "ECOQUEST" : screen == Screen_.Settings ? "AYARLAR" : screen == Screen_.Keys ? "TUŞ ATAMALARI"
            : screen == Screen_.Slots ? "KAYIT SLOTLARI" : "DURAKLATILDI";
        string sub = screen == Screen_.Main ? "Kirlenen mahalleyi birlikte toparla." : screen == Screen_.Slots ? "Hızlı, elle ve final öncesi kayıtlar." : screen == Screen_.Settings ? "Arayüz, erişilebilirlik ve ses."
            : screen == Screen_.Keys ? "Değiştirmek için tıkla, sonra yeni tuşa bas." : "Dünya durdu. Devam etmek için Esc." + LastSaveNote();
        var r = EcoTheme.Window(620f, screen == Screen_.Settings || screen == Screen_.Keys ? 720f : screen == Screen_.Slots ? 680f : screen == Screen_.Pause ? 560f : 450f, heading, sub, screen == Screen_.Main ? "yep" : screen == Screen_.Settings ? "ui_gear" : screen == Screen_.Keys ? "ui_help" : screen == Screen_.Slots ? "calendar" : "ui_pause");
        float x = r.x + 40f, w = r.width - 80f, y = r.y + 110f;
        var save = EcoWorldSave.Current;
        switch (screen)
        {
            case Screen_.Main:
                if (EcoTheme.IconButton(new Rect(x, y, w, 44f), "Yeni oyun", "player")) PlayerController.SetPaused(false);
                GUI.enabled = save != null && save.HasSave;
                if (EcoTheme.IconButton(new Rect(x, y + 54f, w, 44f), "Devam et (son kayıt)", "ui_check")) { PlayerController.SetPaused(false); save.QuickLoad(); }
                GUI.enabled = true;
                if (EcoTheme.IconButton(new Rect(x, y + 108f, w, 44f), "Kayıt slotları", "calendar")) Go(Screen_.Slots, Screen_.Main);
                if (EcoTheme.IconButton(new Rect(x, y + 162f, w, 44f), "Ayarlar", "ui_gear")) Go(Screen_.Settings, Screen_.Main);
                if (EcoTheme.IconButton(new Rect(x, y + 216f, w, 44f), EcoSurvey.Done(false) ? "Öğrenme anketi (ön) ✓" : "Öğrenme anketi (ön, isteğe bağlı)", "ui_journal")) EcoSurvey.Current?.Open(false);
                if (EcoTheme.IconButton(new Rect(x, y + 270f, w, 44f), "Oyundan çık", "ui_close")) Application.Quit();
                break;
            case Screen_.Pause:
                if (EcoTheme.IconButton(new Rect(x, y, w, 44f), "Devam et", "player")) PlayerController.SetPaused(false);
                if (EcoTheme.IconButton(new Rect(x, y + 54f, w, 44f), "Hızlı kaydet", "calendar")) save?.QuickSave();
                if (EcoTheme.IconButton(new Rect(x, y + 108f, w, 44f), "Kayıt slotları", "calendar")) Go(Screen_.Slots, Screen_.Pause);
                if (EcoTheme.IconButton(new Rect(x, y + 162f, w, 44f), "Ayarlar", "ui_gear")) Go(Screen_.Settings, Screen_.Pause);
                // GDD §14 pause menu: also controls/tutorial and a way back to the title screen.
                if (EcoTheme.IconButton(new Rect(x, y + 216f, w, 44f), "Kontroller [F1]", "ui_help"))
                { PlayerController.SetPaused(false); FindFirstObjectByType<EcoWorldHUD>()?.ShowHelp(); }
                if (EcoTheme.IconButton(new Rect(x, y + 270f, w, 44f), EcoSurvey.Done(true) ? "Öğrenme anketi (son) ✓" : "Öğrenme anketi (son, isteğe bağlı)", "ui_journal")) EcoSurvey.Current?.Open(true);
                if (EcoTheme.IconButton(new Rect(x, y + 324f, w, 44f), "Ana menüye dön (kaydedilmemiş ilerleme kaybolur)", "yep")) ReturnToTitle();
                if (EcoTheme.IconButton(new Rect(x, y + 378f, w, 44f), "Oyundan çık", "ui_close")) Application.Quit();
                break;
            case Screen_.Slots:
                if (save == null) { GUI.Label(new Rect(x, y, w, 40f), "Kayıt sistemi bu sahnede yok.", EcoTheme.Body); }
                else
                {
                    if (Time.unscaledTime >= nextSlotScan) { nextSlotScan = Time.unscaledTime + 1f; slots = save.GetSlots(); }
                    for (int i = 0; i < slots.Length; i++)
                    {
                        // Eight slots on 58 px rows fit above "Geri"; name and detail get their own lines so neither is clipped.
                        var s = slots[i]; float row = y + i * 58f;
                        GUI.Label(new Rect(x, row - 2f, w - 190f, 26f), s.Name, EcoTheme.Body);
                        GUI.Label(new Rect(x, row + 23f, w - 190f, 24f), s.Exists ? s.Detail : "Boş", EcoTheme.Muted);
                        bool manual = s.Id.StartsWith("manual-");
                        GUI.enabled = manual && back == Screen_.Pause;
                        if (GUI.Button(new Rect(x + w - 180f, row + 4f, 86f, 40f), "Kaydet", EcoTheme.Button)) { save.SaveManual(int.Parse(s.Id.Substring(7))); nextSlotScan = 0f; }
                        GUI.enabled = s.CanLoad;
                        if (GUI.Button(new Rect(x + w - 90f, row + 4f, 86f, 40f), "Yükle", EcoTheme.Button)) { PlayerController.SetPaused(false); save.LoadSlot(s.Id); }
                        GUI.enabled = true;
                    }
                }
                if (GUI.Button(new Rect(x, r.yMax - 70f, w, 40f), "Geri", EcoTheme.Button)) Back();
                break;
            case Screen_.Settings:
            {
                // Laid out top to bottom inside a scroll area so a large UI scale on a small screen never overlaps "Geri".
                const float contentHeight = 560f;
                var view = new Rect(x, y - 14f, w + 18f, r.yMax - 84f - (y - 14f));
                bool scrolls = view.height < contentHeight;
                scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, scrolls ? w - 6f : w, contentHeight));
                float cw = scrolls ? w - 6f : w;
                GUI.Label(new Rect(0f, 0f, cw, 28f), "Arayüz ölçeği", EcoTheme.Body);
                float[] scales = { 1f, 1.25f, 1.5f }; string[] names = { "%100", "%125", "%150" };
                for (int i = 0; i < 3; i++)
                {
                    bool on = Mathf.Approximately(EcoSettings.UIScale, scales[i]);
                    var prev = GUI.color; if (on) GUI.color = EcoSettings.Good;
                    if (GUI.Button(new Rect(i * (cw / 3f), 30f, cw / 3f - 6f, 36f), names[i], EcoTheme.Button)) EcoSettings.UIScale = scales[i];
                    GUI.color = prev;
                }
                if (Switch(new Rect(0f, 76f, cw, 34f), "Renk körü dostu palet (mavi/turuncu)", EcoSettings.ColorBlind) != EcoSettings.ColorBlind) EcoSettings.ColorBlind = !EcoSettings.ColorBlind;
                if (Switch(new Rect(0f, 114f, cw, 34f), "Kamera sarsıntısı", EcoSettings.CameraShake) != EcoSettings.CameraShake) EcoSettings.CameraShake = !EcoSettings.CameraShake;
                if (Switch(new Rect(0f, 152f, cw, 34f), "Bulanıklık ve hasar vurgusunu azalt", EcoSettings.ReduceBlur) != EcoSettings.ReduceBlur) EcoSettings.ReduceBlur = !EcoSettings.ReduceBlur;
                if (Switch(new Rect(0f, 190f, cw, 34f), "Araç düğmesi: basılı tutmak yerine aç/kapat", EcoKeys.HoldToggle) != EcoKeys.HoldToggle) EcoKeys.HoldToggle = !EcoKeys.HoldToggle;
                if (Switch(new Rect(0f, 228f, cw, 34f), "Tekerlek yönünü ters çevir (yakınlaştırma)", EcoSettings.InvertZoom) != EcoSettings.InvertZoom) EcoSettings.InvertZoom = !EcoSettings.InvertZoom;
                float audioHeight = EcoAudio.DrawSliders(0f, 274f, cw, EcoTheme.Body);
                if (EcoTheme.IconButton(new Rect(0f, 274f + audioHeight + 10f, cw, 40f), "Tuş atamaları", "ui_help")) Go(Screen_.Keys, Screen_.Settings);
                GUI.Label(new Rect(0f, 274f + audioHeight + 56f, cw, 26f), "F3: performans göstergesi", EcoTheme.Muted);
                GUI.EndScrollView();
                if (GUI.Button(new Rect(x, r.yMax - 70f, w, 40f), "Geri", EcoTheme.Button)) Back();
                break;
            }
            case Screen_.Keys:
                DrawKeys(x, y - 20f, w, r.height - 245f);
                // GDD §13: a conflicting binding is not silent; the player sees which action took the old key.
                if (keyNotice.Length > 0) GUI.Label(new Rect(x, r.yMax - 106f, w, 30f), keyNotice, EcoTheme.Muted);
                if (GUI.Button(new Rect(x, r.yMax - 70f, w * 0.5f - 6f, 40f), "Varsayılana dön", EcoTheme.Button)) { EcoKeys.ResetAll(); keyNotice = ""; }
                if (GUI.Button(new Rect(x + w * 0.5f + 6f, r.yMax - 70f, w * 0.5f - 6f, 40f), "Geri", EcoTheme.Button)) { listening = -1; Back(); }
                break;
        }
        EcoTheme.End();
    }

    private void DrawKeys(float x, float y, float w, float height)
    {
        int count = EcoKeys.Names.Length;
        var view = new Rect(x, y, w, height);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, w - 24f, count * 36f));
        for (int i = 0; i < count; i++)
        {
            GUI.Label(new Rect(0f, i * 36f, w * 0.55f, 32f), EcoKeys.Names[i], EcoTheme.Body);
            string label = listening == i ? "bir tuşa bas…" : EcoKeys.Label((EcoKeys.Action)i);
            if (GUI.Button(new Rect(w * 0.55f, i * 36f, w * 0.38f - 24f, 32f), label, EcoTheme.Button)) listening = listening == i ? -1 : i;
        }
        GUI.EndScrollView();
        var e = Event.current;
        if (listening >= 0 && e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
        {
            if (e.keyCode != KeyCode.Escape && System.Enum.TryParse(e.keyCode.ToString(), out Key key) && key != Key.None)
            {
                var action = (EcoKeys.Action)listening;
                int swapped = EcoKeys.Set(action, key);
                keyNotice = swapped < 0 ? "" : "Dikkat: " + EcoKeys.Label(action) + " \"" + EcoKeys.Names[swapped] + "\" için kullanılıyordu; o eylem artık "
                    + EcoKeys.Label((EcoKeys.Action)swapped) + ".";
            }
            listening = -1; e.Use();
        }
    }
    private static bool Switch(Rect rect, string label, bool value)
    {
        var previous = GUI.color; if (value) GUI.color = EcoSettings.Good;
        bool click = GUI.Button(rect, (value ? "✓ " : "✗ ") + label, EcoTheme.Button);
        GUI.color = previous; return click ? !value : value;
    }
    // Reloading the scene gives the title a fresh world, so "Yeni oyun" there really starts over; the clock is
    // released first because the title opens through a scaled-time Invoke.
    private void ReturnToTitle()
    {
        PlayerController.SetPaused(false);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
    }

    // GDD §14: the pause menu tells how old the newest save is, so unsaved progress is visible before quitting or loading.
    private float nextSaveNote; private string saveNote = "";
    private string LastSaveNote()
    {
        if (Time.unscaledTime < nextSaveNote) return saveNote;
        nextSaveNote = Time.unscaledTime + 1f; saveNote = "";
        var save = EcoWorldSave.Current; if (save == null) return saveNote;
        System.DateTime newest = System.DateTime.MinValue;
        foreach (var slot in save.GetSlots())
            if (slot.CanLoad && System.DateTime.TryParse(slot.SavedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) && at > newest) newest = at;
        if (newest == System.DateTime.MinValue) { saveNote = "  •  Henüz kayıt yok"; return saveNote; }
        double minutes = (System.DateTime.UtcNow - newest.ToUniversalTime()).TotalMinutes;
        saveNote = "  •  Son kayıt: " + (minutes < 1 ? "az önce" : minutes < 60 ? Mathf.FloorToInt((float)minutes) + " dk önce"
            : minutes < 1440 ? Mathf.FloorToInt((float)(minutes / 60)) + " sa önce" : Mathf.FloorToInt((float)(minutes / 1440)) + " gün önce");
        return saveNote;
    }

    private void Go(Screen_ target, Screen_ from)
    {
        if (from == Screen_.Main || from == Screen_.Pause) root = from;
        back = from; screen = target; nextSlotScan = 0f; scroll = Vector2.zero; keyNotice = "";
    }
    // Keys sits under Settings; leaving it must restore Settings' own way back, or Settings' "Geri" returned to itself.
    private void Back()
    {
        listening = -1; scroll = Vector2.zero;
        if (screen == Screen_.Keys) { screen = Screen_.Settings; back = root; }
        else screen = back;
    }
}
