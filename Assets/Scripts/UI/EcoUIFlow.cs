using UnityEngine;

// All planning screens and pause share one clock; closing one screen cannot unpause another.
[DefaultExecutionOrder(-500)]
public sealed class EcoUIFlow : MonoBehaviour
{
    public static object ActivePanel { get; private set; }
    public static bool Paused { get; private set; }
    public static bool IsOpen => ActivePanel != null;
    internal static bool PanelFreezesWorld { get; private set; }
    public static bool WorldBlocked => Paused || IsOpen;
    private static bool building;
    private static int closedFrame = -10;
    public static bool InputBlocked => WorldBlocked || Time.frameCount <= closedFrame;
    public static bool IsPanel(object owner) => ReferenceEquals(ActivePanel, owner);
    // One Esc press closes one thing: a panel that used Esc marks the frame so the pause menu ignores it.
    private static int escapeFrame = -10;
    public static void ConsumeEscape() => escapeFrame = Time.frameCount;
    public static bool EscapeConsumed => escapeFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { ActivePanel = null; Paused = building = PanelFreezesWorld = false; closedFrame = -10; Time.timeScale = 1; }
    public static void SetPanel(object owner, bool open, bool freezeWorld = false)
    {
        if (open)
        {
            if (PanelFreezesWorld && !IsPanel(owner)) return;
            ActivePanel = owner;
            PanelFreezesWorld = freezeWorld;
        }
        else if (IsPanel(owner)) { ActivePanel = null; PanelFreezesWorld = false; closedFrame = Time.frameCount; }
        Apply();
    }
    public static void SetBuild(bool active) { building = active; Apply(); }
    public static void SetPaused(bool active) { Paused = active; Apply(); }
    private static void Apply() => Time.timeScale = Paused || PanelFreezesWorld ? 0f : IsOpen || building ? 0.1f : 1f;
    private void Update()
    {
        if (ActivePanel is Object owner && owner == null) { ActivePanel = null; PanelFreezesWorld = false; }
        Apply();
    }
    private void OnDestroy() { ActivePanel = null; Paused = building = PanelFreezesWorld = false; Time.timeScale = 1; }
}
