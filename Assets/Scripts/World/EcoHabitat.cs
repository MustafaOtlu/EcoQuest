using UnityEngine;

[RequireComponent(typeof(EcoRegion))]
public sealed class EcoHabitat : MonoBehaviour
{
    [Range(0, 1)] public float recovery;
    public int Birds => recovery >= 0.25f ? Mathf.Clamp(Mathf.FloorToInt(recovery * 3), 1, 3) : 0;
    public int Butterflies => recovery >= 0.5f ? 2 : 0;
    private EcoRegion region;
    private readonly GameObject[] creatures = new GameObject[5];
    private readonly Transform[][] wings = new Transform[5][];
    // Counting the healthy trees walks every tree in the valley; twice a second is plenty and keeps late runs with
    // many planted trees from costing a scan per habitat per frame.
    private int healthyTrees;
    private Transform anchor;
    private float treesAt = -10f;
    private void Awake() => region = GetComponent<EcoRegion>();
    private void Update() { Simulate(Time.deltaTime); Animate(); }
    private void RefreshTrees(bool force)
    {
        if (!force && Time.time - treesAt < 0.5f && Time.time >= treesAt) return;
        treesAt = Time.time;
        healthyTrees = EcoVegetation.HealthyTreesIn(region);
        anchor = EcoVegetation.FirstHealthyTree(region);
    }
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        region ??= GetComponent<EcoRegion>();
        RefreshTrees(seconds > 1f); // long catch-up steps read the trees as they are now
        bool healthy = region.airPollution < 40 && region.soilPollution < 40 && region.vegetation >= 0.1f && healthyTrees >= 2;
        recovery = Mathf.Clamp01(recovery + seconds * (healthy ? 0.02f : -0.05f));
    }
    public static bool HasBirds(EcoRegion region) => region != null && region.TryGetComponent<EcoHabitat>(out var habitat) && habitat.isActiveAndEnabled && habitat.Birds > 0;
    private void Animate()
    {
        if (anchor != null && !anchor.gameObject.activeInHierarchy) anchor = null;
        Vector3 center = anchor != null ? anchor.position : transform.position;
        for (int i = 0; i < creatures.Length; i++)
        {
            bool butterfly = i >= 3;
            bool visible = butterfly ? i - 3 < Butterflies : i < Birds;
            if (visible && creatures[i] == null) { creatures[i] = EcoWorldArt.Spawn(butterfly ? "Butterfly" : "Bird", center, transform); wings[i] = null; }
            if (creatures[i] == null) continue;
            creatures[i].SetActive(visible); if (!visible) continue;
            float phase = Time.time * (butterfly ? 0.9f : 0.5f) + i * 2.1f;
            float radius = butterfly ? 1.3f : 3.2f;
            creatures[i].transform.position = center + new Vector3(Mathf.Cos(phase) * radius, (butterfly ? 0.8f : 3.6f) + Mathf.Sin(phase * 2) * 0.3f, Mathf.Sin(phase) * radius);
            creatures[i].transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(phase), 0, Mathf.Cos(phase)));
            if (wings[i] == null) wings[i] = System.Array.FindAll(creatures[i].GetComponentsInChildren<Transform>(), t => t.name.StartsWith("Wing"));
            foreach (var wing in wings[i])
                if (wing != null) wing.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * (butterfly ? 20 : 12)) * 30 * (wing.name.EndsWith("L") ? 1 : -1));
        }
    }
    private void OnDisable() { foreach (var creature in creatures) if (creature != null) creature.SetActive(false); }
}
