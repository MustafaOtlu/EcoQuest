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
    public float SlimeRemaining { get; private set; }
    public float GrowthMultiplier => SlimeRemaining > 0f ? 0.5f : 1f;
    public float ContaminationExposure { get; private set; }
    public bool IsMature => growth >= 1f && !IsWithered;
    public bool IsWithered => health <= 0f;
    public string Status => EcoCropCatalog.Get(kind).Name + " • " + (IsWithered ? "Kurumuş bitki" : IsMature ? "Hasada hazır"
        : !ClimateSuitable ? "İklim uygun değil (" + EcoCropCatalog.Get(kind).Climate + ")" : moisture < 0.2f ? "Su gerekiyor" : pests > 0.25f ? "Böcek zararı • Organik gübre uygula" : "Büyüyor")
        + (SlimeRemaining > 0f ? "\nSalgı: büyüme %50 • " + SlimeRemaining.ToString("0.0") + " sn" : "");
    private float dryTime;
    private bool harvested;
    private Vector3 baseScale, basePosition;
    private Renderer plantRenderer;
    private MaterialPropertyBlock tint;
    private Transform cropVisual;
    private Renderer[] cropRenderers;
    private Vector3 visualBaseScale, groundedVisualPosition;
    private float visualHeight;
    public static PlantedSeed Create(Vector3 groundPosition, EcoCropCatalog.Kind kind = EcoCropCatalog.Kind.Cabbage)
    {
        if (!Finite(groundPosition) || !EcoCropCatalog.Valid(kind)) return null;
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
        if (cropVisual != null)
        {
            cropRenderers = cropVisual.GetComponentsInChildren<Renderer>();
            visualBaseScale = cropVisual.localScale;
            groundedVisualPosition = cropVisual.localPosition;
            // Bounds are measured before growth scaling, preserving imported root scales and pivots.
            bool foundBounds = false;
            Bounds bounds = default;
            foreach (var renderer in cropRenderers)
            {
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                    if (!Finite(point)) continue;
                    if (!foundBounds) { bounds = new Bounds(point, Vector3.zero); foundBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (foundBounds && bounds.size.y > 0.001f)
            {
                visualHeight = bounds.size.y;
                groundedVisualPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
        }
        UpdateVisual();
    }
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (!EcoRegion.Valid(seconds)) return;
        float growthTime = seconds - Mathf.Min(seconds, SlimeRemaining) * 0.5f;
        SlimeRemaining = Mathf.Max(0f, SlimeRemaining - seconds);
        if (harvested || IsWithered) return;
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
            if (ClimateSuitable) growth = Mathf.Min(1f, growth + growthTime / Mathf.Max(1f, growthSeconds) * (contaminated ? 0.5f : 1f) * (1 - pests * 0.7f));
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
    public void ApplyAcid(float damage)
    {
        if (harvested || IsWithered || !EcoRegion.Valid(damage)) return;
        contaminated = true; ContaminationExposure = 2f;
        health = Mathf.Max(0f, health - Mathf.Min(damage, 1f));
        UpdateVisual();
    }
    public void ApplySlime(float seconds = 8f)
    {
        if (harvested || IsWithered || !EcoRegion.Valid(seconds)) return;
        SlimeRemaining = Mathf.Max(SlimeRemaining, Mathf.Min(seconds, 30f));
        ContaminationExposure = Mathf.Min(2f, ContaminationExposure + 1f);
        if (ContaminationExposure >= 2f) contaminated = true;
        UpdateVisual();
    }
    private void UpdateVisual()
    {
        float visibleGrowth = EcoRegion.Finite(growth) ? Mathf.Clamp01(growth) : 0f;
        if (cropVisual != null)
        {
            // A newly planted crop must remain readable before watering; old vegetables began at 9.5 cm.
            float saplingHeight = kind == EcoCropCatalog.Kind.Tree ? 1f : 0.28f;
            float seedScale = visualHeight > 0.001f ? Mathf.Clamp(saplingHeight / visualHeight, 0.25f, 1f) : 0.75f;
            float scale = Mathf.Lerp(seedScale, 1f, visibleGrowth);
            cropVisual.localScale = visualBaseScale * scale;
            cropVisual.localPosition = groundedVisualPosition * scale;
            Color multiplier = IsWithered ? new Color(0.45f, 0.28f, 0.12f) : contaminated ? new Color(0.75f, 0.65f, 0.35f) : Color.white;
            foreach (var renderer in cropRenderers)
            {
                if (renderer == null) continue;
                tint.SetColor("_BaseColor", multiplier); tint.SetColor("_Color", multiplier); renderer.SetPropertyBlock(tint);
            }
            return;
        }
        transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + visibleGrowth * 2f, 1f + visibleGrowth * 4f, 1f + visibleGrowth * 2f));
        transform.position = basePosition + Vector3.up * (baseScale.y * visibleGrowth * 4f);
        if (plantRenderer != null)
        {
            Color color = IsWithered ? new Color(0.4f, 0.25f, 0.1f) : contaminated
                ? new Color(0.5f, 0.45f, 0.1f) : Color.Lerp(new Color(0.25f, 0.8f, 0.3f), new Color(0.1f, 0.45f, 0.12f), visibleGrowth);
            tint.SetColor("_BaseColor", color); plantRenderer.SetPropertyBlock(tint);
        }
    }
    public void WaterPlant(float amount, bool clean)
    {
        if (IsWithered || harvested || !EcoRegion.Valid(amount)) return;
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
    private static bool Finite(Vector3 value) => EcoRegion.Finite(value.x) && EcoRegion.Finite(value.y) && EcoRegion.Finite(value.z);
}
