using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// GDD §3: isometric camera that follows the player, zooms with the mouse wheel and fades buildings or
// trees that would hide the character. Also hosts the shared cursor-to-world helpers used by weapons,
// building and click-to-move.
[DisallowMultipleComponent]
public sealed class EcoIsoCamera : MonoBehaviour
{
    public static EcoIsoCamera Current { get; private set; }
    public static Camera Cam => Current != null ? Current.cam : Camera.main;

    public Transform target;
    public const float DefaultZoom = 5.5f;
    public const float MinimumZoom = 4f;
    public const float MaximumZoom = 10f;
    public float yaw = 45f, pitch = 50f;
    // Orthographic half-height: keep the hero readable while retaining room to work around them.
    public float distance = DefaultZoom, minDistance = MinimumZoom, maxDistance = MaximumZoom;
    [Min(0.1f)] public float zoomStep = 0.65f;
    public float followSharpness = 9f;
    public float focusHeight = 1.1f;

    private Camera cam;
    private float wantedDistance;
    private Vector3 focus;
    private bool focusReady;

    private sealed class Fade
    {
        public Renderer renderer;
        public Material[] original, clones;
        public float alpha = 1f;
        public bool seen;
    }
    private readonly Dictionary<Renderer, Fade> fades = new Dictionary<Renderer, Fade>();
    private readonly List<Renderer> done = new List<Renderer>();
    private const float FadedAlpha = 0.22f;

    private void Awake()
    {
        Current = this;
        cam = GetComponent<Camera>();
        minDistance = Mathf.Max(1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        SetZoom(distance, true);
    }

    public void SetZoom(float zoom, bool immediate = false)
    {
        wantedDistance = Mathf.Clamp(zoom, minDistance, maxDistance);
        if (!immediate) return;
        distance = wantedDistance;
        if (cam != null && cam.orthographic) cam.orthographicSize = distance;
    }

    private void OnDestroy()
    {
        foreach (var fade in fades.Values) Restore(fade);
        fades.Clear();
        if (Current == this) Current = null;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        var mouse = Mouse.current;
        if (mouse != null && !EcoUIFlow.InputBlocked && !EcoMarketUI.BlocksGameplay && !PointerOverUI())
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                SetZoom(wantedDistance - Mathf.Sign(scroll) * (EcoSettings.InvertZoom ? -1f : 1f) * zoomStep);
        }
        distance = Mathf.Lerp(distance, wantedDistance, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));

        if (cam.orthographic) cam.orthographicSize = distance;
        Vector3 wantedFocus = target.position + Vector3.up * focusHeight;
        if (EcoGround.HasMap)
        {
            var map = EcoGround.MapRect;
            wantedFocus.x = Mathf.Clamp(wantedFocus.x, map.xMin + 4f, map.xMax - 4f);
            wantedFocus.z = Mathf.Clamp(wantedFocus.z, map.yMin + 4f, map.yMax - 4f);
        }
        else if (Terrain.activeTerrain != null)
        {
            var t = Terrain.activeTerrain; var size = t.terrainData.size; var origin = t.transform.position;
            wantedFocus.x = Mathf.Clamp(wantedFocus.x, origin.x + 5f, origin.x + size.x - 5f);
            wantedFocus.z = Mathf.Clamp(wantedFocus.z, origin.z + 5f, origin.z + size.z - 5f);
        }
        if (!focusReady) { focus = wantedFocus; focusReady = true; }
        else focus = Vector3.Lerp(focus, wantedFocus, 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = focus - rotation * Vector3.forward * (cam.orthographic ? 70f : distance);
        UpdateOcclusion();
    }

    /// <summary>Camera-relative flat directions for WASD.</summary>
    public Vector3 FlatForward => Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, Vector3.up).normalized;
    public Vector3 FlatRight => Vector3.ProjectOnPlane(Quaternion.Euler(0f, yaw, 0f) * Vector3.right, Vector3.up).normalized;

    // ------------------------------------------------------------------ occlusion fade

    private void UpdateOcclusion()
    {
        foreach (var fade in fades.Values) fade.seen = false;
        Vector3 from = transform.position;
        Vector3 to = target.position + Vector3.up * 1.0f;
        Vector3 direction = to - from;
        float length = direction.magnitude - 0.4f;
        if (length > 0.5f)
        {
            var hits = Physics.SphereCastAll(from, 0.45f, direction / (length + 0.4f), length, ~(1 << 2), QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (EcoGround.IsGround(hit.collider) || hit.transform.IsChildOf(target)) continue;
                var renderer = hit.collider.GetComponent<Renderer>();
                if (renderer != null) Touch(renderer);
                else foreach (var child in hit.collider.GetComponentsInChildren<Renderer>()) Touch(child);
            }
        }
        done.Clear();
        foreach (var pair in fades)
        {
            var fade = pair.Value;
            if (fade.renderer == null) { done.Add(pair.Key); continue; }
            float goal = fade.seen ? FadedAlpha : 1f;
            fade.alpha = Mathf.MoveTowards(fade.alpha, goal, 4f * Time.unscaledDeltaTime);
            for (int i = 0; i < fade.clones.Length; i++)
            {
                var clone = fade.clones[i];
                if (clone == null) continue;
                Color color = fade.original[i].HasProperty("_BaseColor") ? fade.original[i].GetColor("_BaseColor") : Color.white;
                color.a *= fade.alpha;
                clone.SetColor("_BaseColor", color);
            }
            if (!fade.seen && fade.alpha >= 0.99f) { Restore(fade); done.Add(pair.Key); }
        }
        foreach (var key in done) fades.Remove(key);
    }

    private void Touch(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) return;
        if (renderer.bounds.size.y < 1.6f) return; // low props never hide the character
        if (!fades.TryGetValue(renderer, out var fade))
        {
            var shared = renderer.sharedMaterials;
            var clones = new Material[shared.Length];
            bool usable = false;
            for (int i = 0; i < shared.Length; i++)
            {
                var source = shared[i];
                if (source == null || source.shader == null || !source.HasProperty("_BaseColor")) { clones[i] = source; continue; }
                var clone = new Material(source) { name = source.name + " (fade)" };
                clone.SetFloat("_Surface", 1f); clone.SetFloat("_Blend", 0f); clone.SetFloat("_ZWrite", 0f);
                clone.SetFloat("_SrcBlend", 5f); clone.SetFloat("_DstBlend", 10f);
                clone.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); clone.renderQueue = 3000;
                clone.SetOverrideTag("RenderType", "Transparent");
                clones[i] = clone; usable = true;
            }
            if (!usable) return;
            fade = new Fade { renderer = renderer, original = shared, clones = clones };
            renderer.sharedMaterials = clones;
            fades[renderer] = fade;
        }
        fade.seen = true;
    }

    private static void Restore(Fade fade)
    {
        if (fade.renderer != null) fade.renderer.sharedMaterials = fade.original;
        if (fade.clones == null) return;
        for (int i = 0; i < fade.clones.Length; i++)
            if (fade.clones[i] != null && fade.clones[i] != fade.original[i]) Destroy(fade.clones[i]);
    }

    // ------------------------------------------------------------------ cursor helpers

    public static bool PointerOverUI()
    {
        return EcoUIFlow.InputBlocked || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
    }

    public static bool TryCursorRay(out Ray ray)
    {
        ray = default;
        var camera = Cam;
        var mouse = Mouse.current;
        if (camera == null || mouse == null) return false;
        ray = camera.ScreenPointToRay(mouse.position.ReadValue());
        return true;
    }

    /// <summary>Nearest solid point under the cursor, ignoring the player's own colliders.</summary>
    public static bool TryCursorHit(Transform ignore, out RaycastHit nearest, float maxDistance = 400f)
    {
        nearest = default;
        if (!TryCursorRay(out var ray)) return false;
        float closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(ray, maxDistance, ~(1 << 2), QueryTriggerInteraction.Ignore))
        {
            if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
            if (hit.distance < closest) { closest = hit.distance; nearest = hit; }
        }
        return closest < float.PositiveInfinity;
    }

    /// <summary>World point under the cursor: the first solid hit, or the horizontal plane at the player's feet.</summary>
    public static bool TryCursorPoint(Transform player, out Vector3 point)
    {
        point = default;
        if (TryCursorHit(player, out var hit)) { point = hit.point; return true; }
        if (!TryCursorRay(out var ray) || player == null) return false;
        var plane = new Plane(Vector3.up, player.position);
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }
}
