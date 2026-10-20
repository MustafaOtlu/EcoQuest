using System;
using UnityEngine;
using UnityEngine.InputSystem;

// GDD §13: rebindable keys and the hold/toggle option for the fire/aim button. Letter actions are read through
// EcoKeys.Pressed; WASD and sprint are remapped on the PlayerController input actions (binding overrides).
public static class EcoKeys
{
    public enum Action { MoveUp, MoveDown, MoveLeft, MoveRight, Sprint, Interact, Mode, Rotate, Build, Orders, Journal, Skills, Map, Finale, Upgrade, Toggle, Dismantle, Settings, QuickSave, QuickLoad }
    private static readonly Key[] Defaults =
    {
        Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.E, Key.Q, Key.R, Key.B, Key.O, Key.J, Key.K, Key.M, Key.F, Key.U, Key.T, Key.X, Key.F2, Key.F5, Key.F9,
    };
    public static readonly string[] Names =
    {
        "Yürü ileri", "Yürü geri", "Yürü sol", "Yürü sağ", "Koş", "Etkileşim / topla", "Mod değiştir", "Döndür / ikincil", "İnşa modu", "Siparişler", "Proje günlüğü", "Uzmanlık", "Harita", "Final terminali", "Yükselt", "Aç/kapat", "Söküm", "Ses ayarı", "Hızlı kaydet", "Hızlı yükle",
    };
    private static Key[] keys;
    private static bool loadedFire, holdToggle, latch;
    private static int fireFrame = -1;
    private static InputAction moveAction, sprintAction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { keys = null; loadedFire = false; latch = false; fireFrame = -1; moveAction = sprintAction = null; }

    private static void Load()
    {
        if (keys != null) return;
        keys = (Key[])Defaults.Clone();
        for (int i = 0; i < keys.Length; i++)
        {
            string saved = PlayerPrefs.GetString("EcoQuest.key." + i, "");
            if (saved.Length > 0 && Enum.TryParse(saved, out Key parsed) && parsed != Key.None) keys[i] = parsed;
        }
    }
    public static Key Get(Action action) { Load(); return keys[(int)action]; }
    public static string Label(Action action) => Get(action).ToString();
    public static string Tag(Action action) => "[" + Label(action) + "]";
    public static bool Pressed(Action action) { var board = Keyboard.current; return board != null && board[Get(action)].wasPressedThisFrame; }
    public static bool Held(Action action) { var board = Keyboard.current; return board != null && board[Get(action)].isPressed; }

    /// <summary>Binds an action to a key; if another action already uses it the two swap so nothing is lost.</summary>
    public static void Set(Action action, Key key)
    {
        Load();
        int index = (int)action;
        for (int i = 0; i < keys.Length; i++) if (i != index && keys[i] == key) { keys[i] = keys[index]; PlayerPrefs.SetString("EcoQuest.key." + i, keys[i].ToString()); }
        keys[index] = key; PlayerPrefs.SetString("EcoQuest.key." + index, key.ToString()); PlayerPrefs.Save();
        ApplyInput();
    }
    public static void ResetAll()
    {
        Load();
        for (int i = 0; i < keys.Length; i++) { keys[i] = Defaults[i]; PlayerPrefs.DeleteKey("EcoQuest.key." + i); }
        ApplyInput();
    }

    public static void AttachInput(InputAction move, InputAction sprint) { moveAction = move; sprintAction = sprint; ApplyInput(); }
    private static void ApplyInput()
    {
        Load();
        if (moveAction != null)
        {
            string[] parts = { "up", "down", "left", "right" }; string[] defaults = { "w", "s", "a", "d" };
            for (int p = 0; p < 4; p++)
                for (int i = 0; i < moveAction.bindings.Count; i++)
                {
                    var binding = moveAction.bindings[i];
                    if (!binding.isPartOfComposite || binding.name != parts[p] || binding.path != "<Keyboard>/" + defaults[p]) continue;
                    moveAction.ApplyBindingOverride(i, "<Keyboard>/" + KeyPath(keys[p]));
                }
        }
        if (sprintAction != null)
            for (int i = 0; i < sprintAction.bindings.Count; i++)
                if (sprintAction.bindings[i].path == "<Keyboard>/leftShift" || sprintAction.bindings[i].path == "<Keyboard>/shift")
                    sprintAction.ApplyBindingOverride(i, "<Keyboard>/" + KeyPath(keys[(int)Action.Sprint]));
    }
    private static string KeyPath(Key key) { string s = key.ToString(); return char.ToLowerInvariant(s[0]) + s.Substring(1); }

    // ---- hold / toggle for the fire and aim button
    public static bool HoldToggle
    {
        get { if (!loadedFire) { loadedFire = true; holdToggle = PlayerPrefs.GetInt("EcoQuest.fireToggle", 0) == 1; } return holdToggle; }
        set { loadedFire = true; holdToggle = value; latch = false; PlayerPrefs.SetInt("EcoQuest.fireToggle", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    /// <summary>True while the tool button counts as held: physically held, or latched on in toggle mode.</summary>
    public static bool FireHeld(Mouse mouse)
    {
        if (mouse == null) return false;
        if (!HoldToggle) return mouse.leftButton.isPressed;
        if (fireFrame != Time.frameCount)
        {
            fireFrame = Time.frameCount;
            if (EcoUIFlow.InputBlocked) latch = false;
            else if (mouse.leftButton.wasPressedThisFrame && !EcoIsoCamera.PointerOverUI()) latch = !latch;
            if (mouse.rightButton.wasPressedThisFrame) latch = false;
        }
        return latch;
    }
}
