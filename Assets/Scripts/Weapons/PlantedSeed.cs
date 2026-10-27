using UnityEngine;
using UnityEngine.UI;

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
    public bool NeedsWater => !IsMature && !IsWithered && moisture < 0.2f;
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

    // Unwatered visual indicator & floating text
    private GameObject soilMound;
    private Renderer soilMoundRenderer;
    private GameObject unwateredIndicatorRoot;
    private Transform pulseRing;
    private Renderer pulseRingRenderer;
    private Transform floatingDropMarker;
    private Renderer[] dropMarkerRenderers;
    private ParticleSystem unwateredParticles;
    private GameObject unwateredSignRoot;
    private Text unwateredSignText;

    private static Material sharedMaterial;
    private static Material sharedParticleMaterial;
    private static Texture2D sharedParticleTexture;

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
        plant.EnsureIndicatorObjects();
        plant.UpdateVisual();
        return plant;
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
        EnsureIndicatorObjects();
        UpdateVisual();
    }

    private void Update()
    {
        Simulate(Time.deltaTime);
        AnimateUnwateredIndicator();
    }

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
            health = Mathf.Max(0f, health - (Mathf.Max(0f, dryTime - 120f) - Mathf.Max(0f, previous - 120f)) * 0.005f);
        }
        health = Mathf.Max(0, health - pests * seconds * 0.02f);
        UpdateVisual();
        if (kind == EcoCropCatalog.Kind.Tree && IsMature) MatureTree();
    }

    private void MatureTree()
    {
        if (harvested) return;
        harvested = true;
        CleanupIndicatorObjects();
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

    private void EnsureIndicatorObjects()
    {
        if (soilMound != null) return;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");
        if (sharedMaterial == null && litShader != null)
        {
            sharedMaterial = new Material(litShader) { enableInstancing = true };
        }

        bool isTree = kind == EcoCropCatalog.Kind.Tree;

        // 1. Soil mound at the base so the planted spot is always visible
        soilMound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soilMound.name = "Soil Mound";
        soilMound.transform.SetParent(transform, false);
        soilMound.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        soilMound.transform.localScale = isTree ? new Vector3(0.52f, 0.09f, 0.52f) : new Vector3(0.36f, 0.07f, 0.36f);
        Destroy(soilMound.GetComponent<Collider>());
        soilMoundRenderer = soilMound.GetComponent<Renderer>();
        if (sharedMaterial != null) soilMoundRenderer.sharedMaterial = sharedMaterial;

        // 2. Unwatered visual effect / indicator (pulsing ring + bobbing water drop + sparkles)
        unwateredIndicatorRoot = new GameObject("Unwatered Indicator");
        unwateredIndicatorRoot.transform.SetParent(transform, false);
        unwateredIndicatorRoot.transform.localPosition = Vector3.zero;

        GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ringObj.name = "Indicator Ring";
        ringObj.transform.SetParent(unwateredIndicatorRoot.transform, false);
        ringObj.transform.localPosition = new Vector3(0f, 0.018f, 0f);
        float ringDiameter = isTree ? 0.65f : 0.46f;
        ringObj.transform.localScale = new Vector3(ringDiameter, 0.008f, ringDiameter);
        Destroy(ringObj.GetComponent<Collider>());
        pulseRing = ringObj.transform;
        pulseRingRenderer = ringObj.GetComponent<Renderer>();
        if (sharedMaterial != null) pulseRingRenderer.sharedMaterial = sharedMaterial;

        GameObject dropRoot = new GameObject("Water Drop Marker");
        dropRoot.transform.SetParent(unwateredIndicatorRoot.transform, false);
        dropRoot.transform.localPosition = new Vector3(0f, isTree ? 0.55f : 0.38f, 0f);
        floatingDropMarker = dropRoot.transform;

        GameObject dropSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dropSphere.name = "Drop Base";
        dropSphere.transform.SetParent(floatingDropMarker, false);
        dropSphere.transform.localPosition = Vector3.zero;
        dropSphere.transform.localScale = new Vector3(0.11f, 0.14f, 0.11f);
        Destroy(dropSphere.GetComponent<Collider>());

        GameObject dropTip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dropTip.name = "Drop Pointer";
        dropTip.transform.SetParent(floatingDropMarker, false);
        dropTip.transform.localPosition = new Vector3(0f, -0.09f, 0f);
        dropTip.transform.localScale = new Vector3(0.035f, 0.05f, 0.035f);
        Destroy(dropTip.GetComponent<Collider>());

        dropMarkerRenderers = floatingDropMarker.GetComponentsInChildren<Renderer>(true);
        if (sharedMaterial != null)
        {
            foreach (var r in dropMarkerRenderers) r.sharedMaterial = sharedMaterial;
        }

        unwateredParticles = CreateSparkleParticles(unwateredIndicatorRoot.transform, isTree);

        // 3. World-space floating label showing what was planted and that it needs water
        unwateredSignRoot = new GameObject("Planted Seed Sign", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        unwateredSignRoot.transform.SetParent(transform, false);
        unwateredSignRoot.transform.localPosition = new Vector3(0f, isTree ? 0.95f : 0.68f, 0f);
        unwateredSignRoot.transform.localScale = Vector3.one * 0.0022f;

        Canvas canvas = unwateredSignRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        unwateredSignRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(680f, 115f);

        GameObject plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(unwateredSignRoot.transform, false);
        plate.GetComponent<RectTransform>().sizeDelta = new Vector2(680f, 115f);
        Image plateImg = plate.GetComponent<Image>();
        plateImg.color = new Color(0.04f, 0.11f, 0.08f, 0.92f);
        plateImg.raycastTarget = false;

        GameObject captionObj = new GameObject("Caption", typeof(RectTransform), typeof(Text));
        captionObj.transform.SetParent(unwateredSignRoot.transform, false);
        captionObj.GetComponent<RectTransform>().sizeDelta = new Vector2(660f, 105f);
        unwateredSignText = captionObj.GetComponent<Text>();
        unwateredSignText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        unwateredSignText.fontSize = 30;
        unwateredSignText.alignment = TextAnchor.MiddleCenter;
        unwateredSignText.color = new Color(0.95f, 0.96f, 0.78f);
        unwateredSignText.raycastTarget = false;
    }

    private static ParticleSystem CreateSparkleParticles(Transform parent, bool isTree)
    {
        var go = new GameObject("Unwatered Sparkles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = 1.2f;
        main.startSpeed = 0.35f;
        main.startSize = 0.07f;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.35f, 0.9f, 1f, 0.9f);

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = isTree ? 0.28f : 0.18f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var colors = ps.colorOverLifetime;
        colors.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.4f, 0.95f, 0.5f), 0f), new GradientColorKey(new Color(0.25f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colors.color = grad;

        if (sharedParticleMaterial == null)
        {
            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pShader == null) pShader = Shader.Find("Particles/Standard Unlit");
            if (pShader != null)
            {
                sharedParticleMaterial = new Material(pShader);
                if (sharedParticleTexture == null)
                {
                    sharedParticleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    for (int y = 0; y < 32; y++)
                    {
                        for (int x = 0; x < 32; x++)
                        {
                            float d = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                            sharedParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                        }
                    }
                    sharedParticleTexture.Apply();
                }
                sharedParticleMaterial.SetTexture("_BaseMap", sharedParticleTexture);
                sharedParticleMaterial.SetTexture("_MainTex", sharedParticleTexture);
                sharedParticleMaterial.SetFloat("_Surface", 1f);
                sharedParticleMaterial.SetFloat("_ZWrite", 0f);
                sharedParticleMaterial.SetFloat("_SrcBlend", 5f);
                sharedParticleMaterial.SetFloat("_DstBlend", 10f);
                sharedParticleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                sharedParticleMaterial.renderQueue = 3000;
            }
        }

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null && sharedParticleMaterial != null)
            psRenderer.sharedMaterial = sharedParticleMaterial;

        ps.Play();
        return ps;
    }

    private void AnimateUnwateredIndicator()
    {
        if (unwateredIndicatorRoot == null || !unwateredIndicatorRoot.activeSelf) return;

        bool isTree = kind == EcoCropCatalog.Kind.Tree;
        float t = Time.time * 3.2f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t);

        if (pulseRing != null)
        {
            float baseDiam = isTree ? 0.65f : 0.46f;
            float d = baseDiam * Mathf.Lerp(0.9f, 1.15f, pulse);
            pulseRing.localScale = new Vector3(d, 0.008f, d);
        }

        if (pulseRingRenderer != null)
        {
            Color ringColor = Color.Lerp(new Color(0.2f, 0.85f, 0.95f), new Color(1f, 0.85f, 0.25f), pulse);
            tint.SetColor("_BaseColor", ringColor);
            tint.SetColor("_Color", ringColor);
            pulseRingRenderer.SetPropertyBlock(tint);
        }

        if (floatingDropMarker != null)
        {
            float baseY = isTree ? 0.52f : 0.36f;
            floatingDropMarker.localPosition = new Vector3(0f, baseY + Mathf.Sin(t * 1.2f) * 0.05f, 0f);
            floatingDropMarker.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
        }

        if (dropMarkerRenderers != null)
        {
            Color dropColor = new Color(0.18f, 0.82f, 1f);
            tint.SetColor("_BaseColor", dropColor);
            tint.SetColor("_Color", dropColor);
            for (int i = 0; i < dropMarkerRenderers.Length; i++)
            {
                if (dropMarkerRenderers[i] != null)
                    dropMarkerRenderers[i].SetPropertyBlock(tint);
            }
        }
    }

    private void CleanupIndicatorObjects()
    {
        if (soilMound != null) Destroy(soilMound);
        if (unwateredIndicatorRoot != null) Destroy(unwateredIndicatorRoot);
        if (unwateredSignRoot != null) Destroy(unwateredSignRoot);
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
        tint ??= new MaterialPropertyBlock();

        bool showUnwatered = NeedsWater;
        float visibleGrowth = EcoRegion.Finite(growth) ? Mathf.Clamp01(growth) : 0f;
        if (unwateredIndicatorRoot != null && unwateredIndicatorRoot.activeSelf != showUnwatered)
        {
            unwateredIndicatorRoot.SetActive(showUnwatered);
            if (showUnwatered && unwateredParticles != null && !unwateredParticles.isPlaying)
                unwateredParticles.Play();
        }

        if (unwateredSignRoot != null)
        {
            if (unwateredSignRoot.activeSelf != showUnwatered)
                unwateredSignRoot.SetActive(showUnwatered);

            if (showUnwatered && unwateredSignText != null)
            {
                string plantedTitle = kind == EcoCropCatalog.Kind.Tree ? "Ağaç fidanı" : EcoCropCatalog.Get(kind).Name;
                unwateredSignText.text = plantedTitle + " · Su gerekiyor\nYanında " + EcoKeys.Tag(EcoKeys.Action.Interact) + " ile sula";
            }
        }

        if (soilMoundRenderer != null)
        {
            Color soilColor = showUnwatered
                ? new Color(0.46f, 0.31f, 0.16f) // Dry soil
                : new Color(0.20f, 0.12f, 0.06f); // Wet soil
            tint.SetColor("_BaseColor", soilColor);
            tint.SetColor("_Color", soilColor);
            soilMoundRenderer.SetPropertyBlock(tint);
        }

        if (cropVisual != null)
        {
            // A newly planted crop must remain readable before watering; old vegetables began at 9.5 cm.
            float saplingHeight = kind == EcoCropCatalog.Kind.Tree ? 1f : 0.28f;
            float seedScale = visualHeight > 0.001f ? Mathf.Clamp(saplingHeight / visualHeight, 0.25f, 1f) : 0.75f;
            float scale = Mathf.Lerp(seedScale, 1f, visibleGrowth);
            cropVisual.localScale = visualBaseScale * scale;
            cropVisual.localPosition = groundedVisualPosition * scale;
            Color multiplier = IsWithered ? new Color(0.45f, 0.28f, 0.12f) : contaminated ? new Color(0.75f, 0.65f, 0.35f) : Color.white;
            if (cropRenderers != null)
            {
                foreach (var renderer in cropRenderers)
                {
                    if (renderer == null) continue;
                    tint.SetColor("_BaseColor", multiplier); tint.SetColor("_Color", multiplier); renderer.SetPropertyBlock(tint);
                }
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
        seeds = 1; food = contaminated ? 0 : EcoCropCatalog.Get(kind).Food;
        gameObject.SetActive(false); Destroy(gameObject); return true;
    }

    public bool TryCompost()
    {
        if ((!IsWithered && !(IsMature && contaminated)) || harvested) return false;
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
