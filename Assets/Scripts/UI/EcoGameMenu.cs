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
        if (EcoExpertDemo.PanelOpen) return; // the expert demo list is drawn instead of the title menu
        if (EcoSurvey.Current != null && EcoSurvey.Current.IsOpen) return; // the survey opened from here sits on top; the menu returns when it closes
        EcoTheme.Begin();
        string heading = screen == Screen_.Main ? "EcoQuest" : screen == Screen_.Settings ? "Ayarlar" : screen == Screen_.Keys ? "Tuş atamaları"
            : screen == Screen_.Slots ? "Kayıtlar" : "Duraklatıldı";
        string sub = screen == Screen_.Main ? "Kirlenen mahalleyi birlikte toparla." : screen == Screen_.Slots ? "Hızlı, elle ve final öncesi kayıtlar." : screen == Screen_.Settings ? "Arayüz, erişilebilirlik ve ses."
            : screen == Screen_.Keys ? "Değiştirmek için tıkla, sonra yeni tuşa bas." : "Dünya durdu. Devam etmek için Esc." + LastSaveNote();
        var r = EcoTheme.Window(620f, screen == Screen_.Settings || screen == Screen_.Keys ? 720f : screen == Screen_.Slots ? 680f : screen == Screen_.Pause ? 530f : 486f, heading, sub, screen == Screen_.Main ? "yep" : screen == Screen_.Settings ? "ui_gear" : screen == Screen_.Keys ? "ui_help" : screen == Screen_.Slots ? "calendar" : "ui_pause");
        float x = r.x + 40f, w = r.width - 80f, y = r.y + 110f;
        var footer = new Rect(x, r.yMax - 62f, w, 38f);
        var view = new Rect(x, y, w, Mathf.Max(1f, footer.y - y - 12f));
        var save = EcoWorldSave.Current;
        switch (screen)
        {
            case Screen_.Main:
            case Screen_.Pause:
                DrawSession(new Rect(x, y, w, Mathf.Max(1f, r.yMax - y - 24f)), save);
                break;
            case Screen_.Slots:
                DrawSlots(view, save);
                if (GUI.Button(footer, "Geri", EcoTheme.Button)) Back();
                break;
            case Screen_.Settings:
                DrawSettings(view);
                if (GUI.Button(footer, "Geri", EcoTheme.Button)) Back();
                break;
            case Screen_.Keys:
                view.height = Mathf.Max(1f, view.height - 40f);
                DrawKeys(view);
                // GDD §13: a conflicting binding is not silent; the player sees which action took the old key.
                if (keyNotice.Length > 0) GUI.Label(new Rect(x, view.yMax + 3f, w, 44f), keyNotice, EcoTheme.Muted);
                float half = (w - 10f) * 0.5f;
                if (GUI.Button(new Rect(x, footer.y, half, footer.height), "Varsayılana dön", EcoTheme.Button)) { EcoKeys.ResetAll(); keyNotice = ""; }
                if (GUI.Button(new Rect(footer.xMax - half, footer.y, half, footer.height), "Geri", EcoTheme.Button)) { listening = -1; Back(); }
                break;
        }
        EcoTheme.End();
    }

    private void DrawSession(Rect area, EcoWorldSave save)
    {
        bool main = screen == Screen_.Main;
        var origin = screen;
        if (EcoTheme.IconButton(new Rect(area.x, area.y, area.width, 46f), main ? "Yeni oyun" : "Devam et", "player", EcoTheme.PrimaryButton))
            PlayerController.SetPaused(false);
        GUI.Label(new Rect(area.x, area.y + 57f, area.width, 22f), main ? "Oyun seçenekleri" : "Oturum seçenekleri", EcoTheme.Muted);

        const float gap = 5f;
        int count = main ? 6 : 7;
        var view = new Rect(area.x, area.y + 82f, area.width, Mathf.Max(1f, area.height - 82f));
        float height = Mathf.Clamp((view.height + gap) / count - gap, 34f, 40f);
        float w = BeginRows(view, count * (height + gap) - gap);
        int row = 0;
        Rect Next() => new Rect(0f, row++ * (height + gap), w, height);
        if (main)
        {
            GUI.enabled = save != null && save.HasSave;
            if (EcoTheme.IconButton(Next(), "Devam et (son kayıt)", "ui_check")) { PlayerController.SetPaused(false); save.QuickLoad(); }
            GUI.enabled = true;
        }
        else if (EcoTheme.IconButton(Next(), "Hızlı kaydet", "calendar")) save?.QuickSave();
        if (EcoTheme.IconButton(Next(), "Kayıt slotları", "calendar")) Go(Screen_.Slots, origin);
        if (EcoTheme.IconButton(Next(), "Ayarlar", "ui_gear")) Go(Screen_.Settings, origin);
        if (main)
        {
            if (EcoTheme.IconButton(Next(), "Uzman demo senaryoları", "ui_journal")) EcoExpertDemo.Current?.Open();
        }
        else if (EcoTheme.IconButton(Next(), "Kontroller [F1]", "ui_help"))
        { PlayerController.SetPaused(false); FindFirstObjectByType<EcoWorldHUD>()?.ShowHelp(); }
        string survey = "Sürdürülebilirlik anketi (" + (main ? "ön" : "son") + (EcoSurvey.Done(!main) ? ") ✓" : ", isteğe bağlı)");
        if (EcoTheme.IconButton(Next(), survey, "ui_journal")) EcoSurvey.Current?.Open(!main);
        if (!main && EcoTheme.IconButton(Next(), "Ana menüye dön (kaydedilmemiş ilerleme kaybolur)", "yep")) ReturnToTitle();
        if (EcoTheme.IconButton(Next(), "Oyundan çık", "ui_close")) Application.Quit();
        GUI.EndScrollView();
    }

    private void DrawSlots(Rect view, EcoWorldSave save)
    {
        if (save == null) { GUI.Label(view, "Kayıt sistemi bu sahnede yok.", EcoTheme.Body); return; }
        if (Time.unscaledTime >= nextSlotScan) { nextSlotScan = Time.unscaledTime + 1f; slots = save.GetSlots(); }
        const float height = 64f;
        float w = BeginRows(view, slots.Length * height);
        float actionWidth = Mathf.Min(86f, w * 0.24f);
        float loadX = w - actionWidth - 6f, saveX = loadX - actionWidth - 6f;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i]; float y = i * height;
            GUI.Box(new Rect(0f, y, w, height - 5f), GUIContent.none, EcoTheme.Card);
            GUI.Label(new Rect(10f, y + 3f, saveX - 18f, 25f), s.Name, EcoTheme.Body);
            GUI.Label(new Rect(10f, y + 28f, saveX - 18f, 28f), s.Exists ? s.Detail : "Boş", EcoTheme.Muted);
            GUI.enabled = s.Id.StartsWith("manual-") && back == Screen_.Pause;
            if (GUI.Button(new Rect(saveX, y + 11f, actionWidth, 36f), "Kaydet", EcoTheme.Button))
            { save.SaveManual(int.Parse(s.Id.Substring(7))); nextSlotScan = 0f; }
            GUI.enabled = s.CanLoad;
            if (GUI.Button(new Rect(loadX, y + 11f, actionWidth, 36f), "Yükle", EcoTheme.Button))
            { PlayerController.SetPaused(false); save.LoadSlot(s.Id); }
            GUI.enabled = true;
        }
        GUI.EndScrollView();
    }

    private void DrawSettings(Rect view)
    {
        const float audioTop = 336f;
        float w = BeginRows(view, audioTop + 308f);
        GUI.Label(new Rect(0f, 0f, w, 28f), "Görünüm ve erişilebilirlik", EcoTheme.Subtitle);
        GUI.Label(new Rect(0f, 34f, 114f, 34f), "Arayüz ölçeği", EcoTheme.Body);
        float step = (w - 124f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            float scale = 1f + i * 0.25f;
            var style = Mathf.Approximately(EcoSettings.UIScale, scale) ? EcoTheme.PrimaryButton : EcoTheme.Button;
            if (GUI.Button(new Rect(124f + i * step, 34f, step - 5f, 34f), "%" + (100 + i * 25), style)) EcoSettings.UIScale = scale;
        }
        if (Switch(new Rect(0f, 77f, w, 34f), "Renk körü dostu palet (mavi/turuncu)", EcoSettings.ColorBlind) != EcoSettings.ColorBlind) EcoSettings.ColorBlind = !EcoSettings.ColorBlind;
        if (Switch(new Rect(0f, 115f, w, 34f), "Kamera sarsıntısı", EcoSettings.CameraShake) != EcoSettings.CameraShake) EcoSettings.CameraShake = !EcoSettings.CameraShake;
        if (Switch(new Rect(0f, 153f, w, 34f), "Bulanıklık ve hasar vurgusunu azalt", EcoSettings.ReduceBlur) != EcoSettings.ReduceBlur) EcoSettings.ReduceBlur = !EcoSettings.ReduceBlur;
        GUI.Label(new Rect(0f, 198f, w, 28f), "Kontroller", EcoTheme.Subtitle);
        if (Switch(new Rect(0f, 231f, w, 34f), "Araç düğmesi: basılı tutmak yerine aç/kapat", EcoKeys.HoldToggle) != EcoKeys.HoldToggle) EcoKeys.HoldToggle = !EcoKeys.HoldToggle;
        if (Switch(new Rect(0f, 269f, w, 34f), "Tekerlek yönünü ters çevir (yakınlaştırma)", EcoSettings.InvertZoom) != EcoSettings.InvertZoom) EcoSettings.InvertZoom = !EcoSettings.InvertZoom;
        GUI.Label(new Rect(0f, 307f, w, 28f), "Ses", EcoTheme.Subtitle);
        float audioHeight = EcoAudio.DrawSliders(0f, audioTop, w, EcoTheme.Body);
        if (EcoTheme.IconButton(new Rect(0f, audioTop + audioHeight + 10f, w, 40f), "Tuş atamaları", "ui_help")) Go(Screen_.Keys, Screen_.Settings);
        GUI.Label(new Rect(0f, audioTop + audioHeight + 56f, w, 26f), "F3: performans göstergesi", EcoTheme.Muted);
        GUI.EndScrollView();
    }

    private float BeginRows(Rect view, float height)
    {
        var scrollbar = GUI.skin.verticalScrollbar;
        float gutter = view.height < height ? Mathf.Max(16f, scrollbar.fixedWidth + scrollbar.margin.left) + 3f : 0f;
        float width = Mathf.Max(1f, view.width - gutter);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, width, height), false, false, GUIStyle.none, scrollbar);
        return width;
    }

    private void DrawKeys(Rect view)
    {
        int count = EcoKeys.Names.Length;
        float w = BeginRows(view, count * 36f);
        for (int i = 0; i < count; i++)
        {
            GUI.Label(new Rect(0f, i * 36f, w * 0.55f, 32f), EcoKeys.Names[i], EcoTheme.Body);
            string label = listening == i ? "bir tuşa bas…" : EcoKeys.Label((EcoKeys.Action)i);
            if (GUI.Button(new Rect(w * 0.56f, i * 36f, w * 0.44f, 32f), label, EcoTheme.Button)) listening = listening == i ? -1 : i;
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
        var style = value ? EcoTheme.PrimaryButton : EcoTheme.Button;
        return GUI.Button(rect, (value ? "✓ " : "✗ ") + label, style) ? !value : value;
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
