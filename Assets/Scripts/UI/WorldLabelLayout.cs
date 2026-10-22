using System.Collections.Generic;
using UnityEngine;

// World captions share one screen-space budget, below HUD/modal layers and clear of the player.
public static class WorldLabelLayout
{
    private struct Candidate
    {
        public WorldSpaceLabel label;
        public Rect bounds;
        public float priority;
    }

    private static readonly List<WorldSpaceLabel> labels = new List<WorldSpaceLabel>();
    private static readonly List<Candidate> candidates = new List<Candidate>();
    private static readonly List<Rect> occupied = new List<Rect>();
    private static readonly Vector3[] corners = new Vector3[8];
    private static PlayerController player;
    private static SkinnedMeshRenderer[] playerRenderers;
    private static int lastFrame = -1, lastCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        labels.Clear(); candidates.Clear(); occupied.Clear();
        player = null; playerRenderers = null; lastFrame = -1; lastCamera = 0;
    }

    internal static void Register(WorldSpaceLabel label)
    {
        if (!labels.Contains(label)) labels.Add(label);
        lastFrame = -1;
    }
    internal static void Unregister(WorldSpaceLabel label) { labels.Remove(label); lastFrame = -1; }

    internal static void Refresh(Camera camera)
    {
        if (camera == null || (lastFrame == Time.frameCount && lastCamera == camera.GetInstanceID())) return;
        lastFrame = Time.frameCount; lastCamera = camera.GetInstanceID();
        if (player == null)
        {
            player = Object.FindFirstObjectByType<PlayerController>();
            playerRenderers = player != null ? player.GetComponentsInChildren<SkinnedMeshRenderer>(true) : null;
        }
        Arrange(camera, labels, player != null ? player.transform : null, playerRenderers, true);
    }

    // Isolated editor layout previews can supply clone labels without touching the authored scene.
    public static void Preview(Camera camera, WorldSpaceLabel[] previewLabels, Transform actor = null)
        => Arrange(camera, previewLabels, actor,
            actor != null ? actor.GetComponentsInChildren<SkinnedMeshRenderer>(true) : null, false);

    private static void Arrange(Camera camera, IList<WorldSpaceLabel> source, Transform actor,
        SkinnedMeshRenderer[] renderers, bool respectMenus)
    {
        if (camera == null || camera.pixelHeight < 1 || camera.pixelWidth < 1) return;
        candidates.Clear(); occupied.Clear();
        EcoUI.AppendWorldLabelObstacles(occupied);
        if (actor != null) ReserveActor(camera, actor, renderers);
        Rect viewport = camera.pixelRect;
        Rect safe = Screen.safeArea;
        if (camera.targetTexture == null && safe.width > 0 && safe.height > 0)
            viewport = Rect.MinMaxRect(Mathf.Max(viewport.xMin, safe.xMin), Mathf.Max(viewport.yMin, safe.yMin),
                Mathf.Min(viewport.xMax, safe.xMax), Mathf.Min(viewport.yMax, safe.yMax));
        float gap = 10f * Mathf.Clamp(camera.pixelHeight / 900f, 0.85f, 1.25f);
        bool blocked = respectMenus && (EcoUIFlow.WorldBlocked || EcoUI.ForeignWindowOpen || EcoMarketUI.IsOpen || EcoWorldHUD.MapOpen);
        foreach (var label in source)
        {
            if (label == null || !label.isActiveAndEnabled) continue;
            label.RefreshForCamera(camera);
            Rect bounds = label.ScreenBounds;
            float distance = actor != null ? Vector3.ProjectOnPlane(label.Anchor - actor.position, Vector3.up).sqrMagnitude : 0;
            float range = label.IsPlantHint ? 8f : label.IsPhysicalSign ? 28f : 20f;
            if (blocked || label.AnchorDepth <= camera.nearClipPlane || !viewport.Contains(bounds.center)
                || (actor != null && distance > range * range) || bounds.width < 1 || bounds.height < 1)
            {
                label.SetVisible(false); label.WasPlaced = false; continue;
            }
            candidates.Add(new Candidate { label = label, bounds = bounds,
                priority = distance - (label.IsPlantHint ? 1000 : label.IsPhysicalSign ? 100 : 0) - (label.WasPlaced ? 3 : 0) });
        }
        candidates.Sort((a, b) => { int order = a.priority.CompareTo(b.priority); return order != 0 ? order : a.label.GetInstanceID().CompareTo(b.label.GetInstanceID()); });
        int visible = 0; bool plantShown = false;
        foreach (var candidate in candidates)
        {
            var label = candidate.label;
            Vector2 center = Vector2.zero;
            bool show = visible < 7 && (!label.IsPlantHint || !plantShown)
                && FindSpace(candidate.bounds, viewport, gap, occupied, out center);
            if (!show) { label.SetVisible(false); label.WasPlaced = false; continue; }
            label.PlaceAt(camera, center);
            label.WasPlaced = true;
            occupied.Add(new Rect(center - candidate.bounds.size * 0.5f, candidate.bounds.size));
            visible++; plantShown |= label.IsPlantHint;
        }
    }

    private static bool FindSpace(Rect original, Rect viewport, float gap, List<Rect> obstacles, out Vector2 result)
    {
        Vector2 half = original.size * 0.5f;
        var allowed = Rect.MinMaxRect(viewport.xMin + gap + half.x, viewport.yMin + gap + half.y,
            viewport.xMax - gap - half.x, viewport.yMax - gap - half.y);
        result = original.center;
        if (allowed.width < 0 || allowed.height < 0) return false;
        float best = float.PositiveInfinity;
        Consider(original.center, original, allowed, gap, obstacles, ref best, ref result);
        if (best < 0.5f) return true;
        foreach (Rect obstacle in obstacles)
        {
            float left = obstacle.xMin - half.x - gap, right = obstacle.xMax + half.x + gap;
            float below = obstacle.yMin - half.y - gap, above = obstacle.yMax + half.y + gap;
            Consider(new Vector2(original.center.x, above), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(original.center.x, below), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(left, original.center.y), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(right, original.center.y), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(left, above), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(right, above), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(left, below), original, allowed, gap, obstacles, ref best, ref result);
            Consider(new Vector2(right, below), original, allowed, gap, obstacles, ref best, ref result);
        }
        return !float.IsPositiveInfinity(best);
    }

    private static void Consider(Vector2 point, Rect original, Rect allowed, float gap, List<Rect> obstacles,
        ref float best, ref Vector2 result)
    {
        point.x = Mathf.Clamp(point.x, allowed.xMin, allowed.xMax);
        point.y = Mathf.Clamp(point.y, allowed.yMin, allowed.yMax);
        float cost = (point - original.center).sqrMagnitude;
        if (cost >= best || cost > 260f * 260f) return;
        var bounds = new Rect(point - original.size * 0.5f - Vector2.one * (gap * 0.45f), original.size + Vector2.one * (gap * 0.9f));
        foreach (var obstacle in obstacles) if (bounds.Overlaps(obstacle)) return;
        best = cost; result = point;
    }

    private static void ReserveActor(Camera camera, Transform actor, SkinnedMeshRenderer[] renderers)
    {
        Bounds body = new Bounds(actor.position + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
        bool found = false;
        if (renderers != null) foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found) { body = renderer.bounds; found = true; } else body.Encapsulate(renderer.bounds);
        }
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = body.center + Vector3.Scale(body.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 screen = camera.WorldToScreenPoint(corners[i]);
            if (screen.z <= camera.nearClipPlane) return;
            min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
        }
        occupied.Add(Rect.MinMaxRect(min.x - 12, min.y - 12, max.x + 12, max.y + 12));
    }
}
