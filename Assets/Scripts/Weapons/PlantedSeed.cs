using UnityEngine;

public sealed class PlantedSeed : MonoBehaviour
{
    public EcoCropCatalog.Kind kind;
    [Range(0, 1)] public float pests;
    [Min(0)] public float pestProtectionRemaining;
    [Min(0)] public float age;
    public bool ClimateSuitable => EcoCropCatalog.Get(kind).Suitable(EcoRegion.At(transform.position)?.Temperature ?? 20);
    [Min(1f)] public float growthSeconds = 60f;
    [Range(0f, 1f)] public float moisture = 0.12f;
    [Range(0f, 1f)] public float growth;
    [Range(0f, 1f)] public float health = 1f;
    public bool contaminated;
    public bool IsMature => growth >= 1f && !IsWithered;
    public bool IsWithered => health <= 0f;
    public string Status => EcoCropCatalog.Get(kind).Name + " • " + (IsWithered ? "Kurumuş bitki" : IsMature ? "Hasada hazır"
        : !ClimateSuitable ? "İklim uygun değil (" + EcoCropCatalog.Get(kind).Climate + ")" : moisture < 0.2f ? "Su gerekiyor" : pests > 0.25f ? "Böcek zararı • Organik gübre uygula" : "Büyüyor");
    private float dryTime;
    private bool harvested;
    private Vector3 baseScale, basePosition;
    private Renderer plantRenderer;
    private MaterialPropertyBlock tint;
    private Transform cropVisual;
    private Renderer[] cropRenderers;
    public static PlantedSeed Create(Vector3 groundPosition, EcoCropCatalog.Kind kind = EcoCropCatalog.Kind.Cabbage)
    {
        var root = new GameObject("Eco Crop");
        root.transform.position = groundPosition;
        var visual = EcoWorldArt.Spawn(EcoCropCatalog.Get(kind).Model, groundPosition, root.transform);
        if (visual == null) { Destroy(root); return null; }
        visual.name = "Crop Visual";
        var collider = root.AddComponent<CapsuleCollider>();
        collider.radius = 0.16f; collider.height = 0.35f; collider.center = Vector3.up * 0.175f;
        var plant = root.AddComponent<PlantedSeed>(); plant.kind = kind; plant.growthSeconds = EcoCropCatalog.Get(kind).GrowthSeconds;
        plant.UpdateVisual(); return plant;
    }
    private void Awake()
    {
        baseScale = transform.localScale; basePosition = transform.position;
        plantRenderer = GetComponent<Renderer>();
        tint = new MaterialPropertyBlock();
        cropVisual = transform.Find("Crop Visual");
        if (cropVisual != null) cropRenderers = cropVisual.GetComponentsInChildren<Renderer>();
        UpdateVisual();
    }
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (harvested || IsWithered || !EcoRegion.Valid(seconds)) return;
        var region = EcoRegion.At(transform.position);
        float unprotected = Mathf.Max(0, seconds - Mathf.Max(pestProtectionRemaining, 45 - age));
        age += seconds;
        pestProtectionRemaining = Mathf.Max(0, pestProtectionRemaining - seconds);
        if (region != null && age > 45 && kind != EcoCropCatalog.Kind.Tree)
        {
            bool birds = EcoHabitat.HasBirds(region);
            pests = Mathf.Clamp01(pests + (birds ? -0.03f * seconds : unprotected * 0.012f));
        }
        if (region != null && region.soilPollution >= 40) contaminated = true;
        float heat = region == null ? 1 : 1 + Mathf.Max(0, region.Temperature - 25) * 0.04f;
        moisture = Mathf.Max(0f, moisture - seconds * EcoCropCatalog.Get(kind).WaterUse * heat);
        if (moisture >= 0.2f)
        {
            dryTime = 0f;
            if (ClimateSuitable) growth = Mathf.Min(1f, growth + seconds / Mathf.Max(1f, growthSeconds) * (contaminated ? 0.5f : 1f) * (1 - pests * 0.7f));
            health = Mathf.Clamp01(health + seconds * (ClimateSuitable ? 0.015f : -0.006f));
        }
        else
        {
            float previous = dryTime; dryTime += seconds;
            health = Mathf.Max(0f, health - (Mathf.Max(0f, dryTime - 15f) - Mathf.Max(0f, previous - 15f)) * 0.02f);
        }
        health = Mathf.Max(0, health - pests * seconds * 0.02f);
        UpdateVisual();
        if (kind == EcoCropCatalog.Kind.Tree && IsMature) MatureTree();
    }
    private void MatureTree()
    {
        if (harvested) return;
        harvested = true;
        var tree = gameObject.AddComponent<EcoVegetation>(); tree.health = health; tree.foliage = cropRenderers;
        if (TryGetComponent<Unity.AI.Navigation.NavMeshModifier>(out var navigation)) navigation.ignoreFromBuild = false;
        var collider = GetComponent<CapsuleCollider>(); collider.height = 3.4f; collider.radius = 0.3f; collider.center = Vector3.up * 1.7f;
        gameObject.name = "Grown tree"; Destroy(this);
    }
    public void ReceiveEnvironmentalDamage(float amount)
    {
        if (harvested || !EcoRegion.Valid(amount)) return;
        health = Mathf.Max(0, health - amount); UpdateVisual();
    }
    private void UpdateVisual()
    {
        if (cropVisual != null)
        {
            // The imported visual grows around its soil-level pivot; interaction collider stays reachable.
            cropVisual.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, growth);
            Color multiplier = IsWithered ? new Color(0.45f, 0.28f, 0.12f) : contaminated ? new Color(0.75f, 0.65f, 0.35f) : Color.white;
            foreach (var renderer in cropRenderers)
            {
                tint.SetColor("_BaseColor", multiplier); tint.SetColor("_Color", multiplier); renderer.SetPropertyBlock(tint);
            }
            return;
        }
        transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + growth * 2f, 1f + growth * 4f, 1f + growth * 2f));
        transform.position = basePosition + Vector3.up * (baseScale.y * growth * 4f);
        if (plantRenderer != null)
        {
            Color color = IsWithered ? new Color(0.4f, 0.25f, 0.1f) : contaminated
                ? new Color(0.5f, 0.45f, 0.1f) : Color.Lerp(new Color(0.25f, 0.8f, 0.3f), new Color(0.1f, 0.45f, 0.12f), growth);
            tint.SetColor("_BaseColor", color); plantRenderer.SetPropertyBlock(tint);
        }
    }
    public void WaterPlant(float amount, bool clean)
    {
        if (IsWithered || harvested || amount <= 0f) return;
        moisture = Mathf.Clamp01(moisture + amount);
        if (!clean) contaminated = true;
        dryTime = 0f;
        UpdateVisual();
    }
    public bool TryHarvest(out int seeds, out int food)
    {
        seeds = food = 0;
        if (!IsMature || harvested || kind == EcoCropCatalog.Kind.Tree) return false;
        harvested = true;
        seeds = contaminated ? 1 : 3; food = contaminated ? 0 : EcoCropCatalog.Get(kind).Food;
        gameObject.SetActive(false); Destroy(gameObject); return true;
    }
    public bool TryCompost()
    {
        if (!IsWithered || harvested) return false;
        harvested = true; gameObject.SetActive(false); Destroy(gameObject); return true;
    }
    public bool Fertilize()
    {
        if (IsWithered || harvested || IsMature) return false;
        // Fertilizer does not bypass climate requirements.
        if (ClimateSuitable) growth = Mathf.Min(1f, growth + 0.2f);
        health = Mathf.Min(1f, health + 0.25f); pests = 0; pestProtectionRemaining = 120; UpdateVisual(); return true;
    }
}
