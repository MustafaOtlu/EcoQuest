using UnityEngine;

[RequireComponent(typeof(EcoRegion))]
public sealed class EcoHabitat : MonoBehaviour
{
    [Range(0, 1)] public float recovery;
    public int Birds => recovery >= 0.25f ? Mathf.Clamp(Mathf.FloorToInt(recovery * 3), 1, 3) : 0;
    public int Butterflies => recovery >= 0.5f ? 2 : 0;
    private EcoRegion region;
    private readonly GameObject[] creatures = new GameObject[5];
    private void Awake() => region = GetComponent<EcoRegion>();
    private void Update() { Simulate(Time.deltaTime); Animate(); }
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        region ??= GetComponent<EcoRegion>();
        bool healthy = region.airPollution < 40 && region.soilPollution < 40 && region.vegetation >= 0.1f && EcoVegetation.HealthyTreesIn(region) >= 2;
        recovery = Mathf.Clamp01(recovery + seconds * (healthy ? 0.02f : -0.05f));
    }
    public static bool HasBirds(EcoRegion region) => region != null && region.TryGetComponent<EcoHabitat>(out var habitat) && habitat.isActiveAndEnabled && habitat.Birds > 0;
    private void Animate()
    {
        var anchor = EcoVegetation.FirstHealthyTree(region);
        Vector3 center = anchor != null ? anchor.position : transform.position;
        for (int i = 0; i < creatures.Length; i++)
        {
            bool butterfly = i >= 3;
            bool visible = butterfly ? i - 3 < Butterflies : i < Birds;
            if (visible && creatures[i] == null) creatures[i] = EcoWorldArt.Spawn(butterfly ? "Butterfly" : "Bird", center, transform);
            if (creatures[i] == null) continue;
            creatures[i].SetActive(visible); if (!visible) continue;
            float phase = Time.time * (butterfly ? 0.9f : 0.5f) + i * 2.1f;
            float radius = butterfly ? 1.3f : 3.2f;
            creatures[i].transform.position = center + new Vector3(Mathf.Cos(phase) * radius, (butterfly ? 0.8f : 3.6f) + Mathf.Sin(phase * 2) * 0.3f, Mathf.Sin(phase) * radius);
            creatures[i].transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(phase), 0, Mathf.Cos(phase)));
            foreach (var wing in creatures[i].GetComponentsInChildren<Transform>())
                if (wing.name.StartsWith("Wing")) wing.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * (butterfly ? 20 : 12)) * 30 * (wing.name.EndsWith("L") ? 1 : -1));
        }
    }
    private void OnDisable() { foreach (var creature in creatures) if (creature != null) creature.SetActive(false); }
}
