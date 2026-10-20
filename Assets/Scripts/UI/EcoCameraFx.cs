using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GDD §13: hit feedback (short camera shake + edge flash) and optional blur/depth effects, each switchable in settings.
// The shake offsets the camera only while it renders, so the follow camera never feeds back on itself.
public sealed class EcoCameraFx : MonoBehaviour
{
    private PlayerVitals vitals;
    private float lastEnergy = -1f, shake, flash, nextScan;
    private Vector3 applied;
    private Transform shaken;
    private static Texture2D edge;

    private void OnEnable() { RenderPipelineManager.beginCameraRendering += Begin; RenderPipelineManager.endCameraRendering += End; }
    private void OnDisable() { RenderPipelineManager.beginCameraRendering -= Begin; RenderPipelineManager.endCameraRendering -= End; Revert(); }

    private void Update()
    {
        if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>();
        if (vitals != null)
        {
            if (lastEnergy >= 0f && vitals.Energy < lastEnergy - 0.5f) { shake = Mathf.Min(1f, shake + 0.6f); flash = Mathf.Min(1f, flash + 0.7f); }
            lastEnergy = vitals.Energy;
        }
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.5f);
        flash = Mathf.MoveTowards(flash, 0f, Time.unscaledDeltaTime * 1.6f);
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1f; ApplyBlurSetting(); }
    }

    /// <summary>Blur / depth-of-field overrides in scene volumes follow the "reduce blur" setting.</summary>
    public static void ApplyBlurSetting()
    {
        bool allow = !EcoSettings.ReduceBlur;
        foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            var profile = volume.sharedProfile; if (profile == null) continue;
            if (profile.TryGet(out MotionBlur motion)) motion.active = allow;
            if (profile.TryGet(out DepthOfField depth)) depth.active = allow;
        }
    }

    private void Begin(ScriptableRenderContext context, Camera camera)
    {
        if (camera != Camera.main || shake <= 0.001f || !EcoSettings.CameraShake) return;
        applied = Random.insideUnitSphere * 0.18f * shake; shaken = camera.transform;
        shaken.position += applied;
    }
    private void End(ScriptableRenderContext context, Camera camera) { if (camera == Camera.main) Revert(); }
    private void Revert() { if (shaken != null && applied != Vector3.zero) shaken.position -= applied; applied = Vector3.zero; shaken = null; }

    private void OnGUI()
    {
        if (flash <= 0.01f || EcoSettings.ReduceBlur) return;
        if (edge == null) { edge = new Texture2D(1, 1); edge.SetPixel(0, 0, Color.white); edge.Apply(); }
        var color = EcoSettings.Bad; color.a = 0.28f * flash; var previous = GUI.color; GUI.color = color;
        float t = Mathf.Max(16f, Screen.height * 0.035f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, t), edge); GUI.DrawTexture(new Rect(0, Screen.height - t, Screen.width, t), edge);
        GUI.DrawTexture(new Rect(0, 0, t, Screen.height), edge); GUI.DrawTexture(new Rect(Screen.width - t, 0, t, Screen.height), edge);
        GUI.color = previous;
    }
}
