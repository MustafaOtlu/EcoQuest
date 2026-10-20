using UnityEngine;
using UnityEngine.InputSystem;

// GDD §13/§14: title screen at start (new game / continue / slots / settings) and the pause menu with the same
// screens. The shared clock (EcoUIFlow) freezes the world while a menu is open. Settings cover UI scale, colour-blind
// palette, shake/blur, hold-or-toggle tool button, audio buses and key rebinding.
public sealed class EcoGameMenu : MonoBehaviour
{
    private enum Screen_ { Pause, Main, Slots, Settings, Keys }
    private Screen_ screen = Screen_.Pause;
    private Screen_ back = Screen_.Pause;
    private bool opened;
    private EcoWorldSave.SlotInfo[] slots = new EcoWorldSave.SlotInfo[0];
    private float nextSlotScan;
    private int listening = -1;
    private Vector2 scroll;

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

    private void OnGUI()
    {
        if (!PlayerController.Paused) { if (screen != Screen_.Pause) screen = Screen_.Pause; listening = -1; return; }
        if (EcoSurvey.Current != null && EcoSurvey.Current.IsOpen) return; // the survey opened from here sits on top; the menu returns when it closes
        EcoTheme.Begin();
        string heading = screen == Screen_.Main ? "ECOQUEST" : "DURAKLATILDI";
        string sub = screen == Screen_.Main ? "Kirlenen mahalleyi birlikte toparla." : screen == Screen_.Slots ? "Kayıt slotları" : screen == Screen_.Settings ? "Ayarlar"
            : screen == Screen_.Keys ? "Tuş atamaları (değiştirmek için tıkla, sonra yeni tuşa bas)" : "Dünya durdu. Devam etmek için Esc.";
        var r = EcoTheme.Window(620f, screen == Screen_.Settings || screen == Screen_.Keys ? 720f : screen == Screen_.Slots ? 520f : 450f, heading, sub, screen == Screen_.Main ? "yep" : screen == Screen_.Settings ? "ui_gear" : screen == Screen_.Keys ? "ui_help" : screen == Screen_.Slots ? "calendar" : "ui_pause");
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
                if (EcoTheme.IconButton(new Rect(x, y + 216f, w, 44f), EcoSurvey.Done(true) ? "Öğrenme anketi (son) ✓" : "Öğrenme anketi (son, isteğe bağlı)", "ui_journal")) EcoSurvey.Current?.Open(true);
                if (EcoTheme.IconButton(new Rect(x, y + 270f, w, 44f), "Oyundan çık", "ui_close")) Application.Quit();
                break;
            case Screen_.Slots:
                if (save == null) { GUI.Label(new Rect(x, y, w, 40f), "Kayıt sistemi bu sahnede yok.", EcoTheme.Body); }
                else
                {
                    if (Time.unscaledTime >= nextSlotScan) { nextSlotScan = Time.unscaledTime + 1f; slots = save.GetSlots(); }
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var s = slots[i]; float row = y + i * 52f;
                        GUI.Label(new Rect(x, row, w - 190f, 46f), s.Name + "\n" + (s.Exists ? s.Detail : "Boş"), EcoTheme.Body);
                        bool manual = s.Id.StartsWith("manual-");
                        GUI.enabled = manual && back == Screen_.Pause;
                        if (GUI.Button(new Rect(x + w - 180f, row, 86f, 40f), "Kaydet", EcoTheme.Button)) { save.SaveManual(int.Parse(s.Id.Substring(7))); nextSlotScan = 0f; }
                        GUI.enabled = s.CanLoad;
                        if (GUI.Button(new Rect(x + w - 90f, row, 86f, 40f), "Yükle", EcoTheme.Button)) { PlayerController.SetPaused(false); save.LoadSlot(s.Id); }
                        GUI.enabled = true;
                    }
                }
                if (GUI.Button(new Rect(x, r.yMax - 70f, w, 40f), "Geri", EcoTheme.Button)) screen = back;
                break;
            case Screen_.Settings:
                GUI.Label(new Rect(x, y - 12f, w, 28f), "Arayüz ölçeği", EcoTheme.Body);
                float[] scales = { 1f, 1.25f, 1.5f }; string[] names = { "%100", "%125", "%150" };
                for (int i = 0; i < 3; i++)
                {
                    bool on = Mathf.Approximately(EcoSettings.UIScale, scales[i]);
                    var prev = GUI.color; if (on) GUI.color = EcoSettings.Good;
                    if (GUI.Button(new Rect(x + i * (w / 3f), y + 18f, w / 3f - 6f, 36f), names[i], EcoTheme.Button)) EcoSettings.UIScale = scales[i];
                    GUI.color = prev;
                }
                if (Switch(new Rect(x, y + 62f, w, 34f), "Renk körü dostu palet (mavi/turuncu)", EcoSettings.ColorBlind) != EcoSettings.ColorBlind) EcoSettings.ColorBlind = !EcoSettings.ColorBlind;
                if (Switch(new Rect(x, y + 100f, w, 34f), "Kamera sarsıntısı", EcoSettings.CameraShake) != EcoSettings.CameraShake) EcoSettings.CameraShake = !EcoSettings.CameraShake;
                if (Switch(new Rect(x, y + 138f, w, 34f), "Bulanıklık ve hasar vurgusunu azalt", EcoSettings.ReduceBlur) != EcoSettings.ReduceBlur) EcoSettings.ReduceBlur = !EcoSettings.ReduceBlur;
                if (Switch(new Rect(x, y + 176f, w, 34f), "Araç düğmesi: basılı tutmak yerine aç/kapat", EcoKeys.HoldToggle) != EcoKeys.HoldToggle) EcoKeys.HoldToggle = !EcoKeys.HoldToggle;
                EcoAudio.DrawSliders(x, y + 220f, w, EcoTheme.Body);
                if (EcoTheme.IconButton(new Rect(x, y + 412f, w, 40f), "Tuş atamaları", "ui_help")) Go(Screen_.Keys, Screen_.Settings);
                GUI.Label(new Rect(x, y + 456f, w, 30f), "F3: performans göstergesi", EcoTheme.Muted);
                if (GUI.Button(new Rect(x, r.yMax - 70f, w, 40f), "Geri", EcoTheme.Button)) screen = back;
                break;
            case Screen_.Keys:
                DrawKeys(x, y - 20f, w, r.height - 215f);
                if (GUI.Button(new Rect(x, r.yMax - 70f, w * 0.5f - 6f, 40f), "Varsayılana dön", EcoTheme.Button)) EcoKeys.ResetAll();
                if (GUI.Button(new Rect(x + w * 0.5f + 6f, r.yMax - 70f, w * 0.5f - 6f, 40f), "Geri", EcoTheme.Button)) { listening = -1; screen = back; }
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
            if (e.keyCode != KeyCode.Escape && System.Enum.TryParse(e.keyCode.ToString(), out Key key) && key != Key.None) EcoKeys.Set((EcoKeys.Action)listening, key);
            listening = -1; e.Use();
        }
    }
    private static bool Switch(Rect rect, string label, bool value)
    {
        var previous = GUI.color; if (value) GUI.color = EcoSettings.Good;
        bool click = GUI.Button(rect, (value ? "✓ " : "✗ ") + label, EcoTheme.Button);
        GUI.color = previous; return click ? !value : value;
    }
    private void Go(Screen_ target, Screen_ from) { back = from; screen = target; nextSlotScan = 0f; }
}
