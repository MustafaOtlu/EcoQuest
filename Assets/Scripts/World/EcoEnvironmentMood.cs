using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// GDD v5 §10: the state of the region the player stands in is visible on screen, not only in the panel.
/// Each panel row has its own sign, and it fades in over a few seconds as the player crosses a region border:
/// - Air quality (AQI): smog. The fog closes in around the player, turns yellow-brown, and dust drifts in the air.
/// - Carbon load (whole valley): a warm, hazy sky and a yellower, weaker sun.
/// - Ecosystem health: colour. A sick region looks grey and flat; a healthy one keeps its full colour.
/// - Biodiversity: life in the air. Seeds and pollen drift over healthy land (birds and butterflies are EcoHabitat's).
/// - Water and soil already show on the lakes (EcoLakeTint) and on the ground (EcoPollutionField).
/// Presentation only: nothing here writes to the simulation. EcoAtmosphere reads Smog and Heat when it sets the fog.
/// </summary>
[DefaultExecutionOrder(900)]
public sealed class EcoEnvironmentMood : MonoBehaviour
{
    public static EcoEnvironmentMood Current { get; private set; }
    /// <summary>0 clean air … 1 heavy smog, at the player.</summary>
    public static float Smog => Current != null ? Current.smog : 0f;
    /// <summary>0 low … 1 high carbon load in the whole valley.</summary>
    public static float Heat => Current != null ? Current.heat : 0f;
    /// <summary>0 healthy … 1 sick ecosystem, at the player.</summary>
    public static float Gloom => Current != null ? Current.gloom : 0f;
    /// <summary>0 lifeless … 1 rich biodiversity, at the player.</summary>
    public static float Life => Current != null ? Current.life : 0f;

    public static readonly Color SmogColor = new Color(0.56f, 0.5f, 0.38f);
    public static readonly Color HeatColor = new Color(0.86f, 0.64f, 0.42f);

    /// <summary>Smog strength for an air quality index: none up to "moderate" (AQI 50), full at "very unhealthy" (200).</summary>
    public static float SmogFor(float aqi) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 200f, aqi));
    /// <summary>Heat haze for the valley's carbon load (the panel's "Karbon yükü"): clear up to 12, full at 40.</summary>
    public static float HeatFor(float carbon) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 40f, carbon));
    /// <summary>Grey-out for an ecosystem health score (0–100): full colour from 70, fully grey at 25.</summary>
    public static float GloomFor(float health) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 25f, health));

    /// <summary>The panel's biodiversity and ecosystem health scores for a region (same formula as EcoWorldHUD).</summary>
    public static void Scores(EcoRegion r, out float biodiversity, out float health)
    {
        biodiversity = health = 0f;
        if (r == null) return;
        float air = 100f - r.airPollution, water = 100f - r.waterPollution, soil = 100f - r.soilPollution;
        float recovery = r.TryGetComponent<EcoHabitat>(out var habitat) ? habitat.recovery : 0f;
        biodiversity = Mathf.Clamp(r.vegetation * 55f + recovery * 30f + water * 0.15f, 0f, 100f);
        health = Mathf.Clamp(air * 0.30f + water * 0.25f + soil * 0.20f + biodiversity * 0.25f, 0f, 100f);
    }

    private const float FadeSeconds = 3.5f;
    private float smog, heat, gloom, life;
    private float smogTarget, heatTarget, gloomTarget, lifeTarget;
    private float nextSample;
    private Transform focus;
    private Volume volume;
    private VolumeProfile profile;
    private ColorAdjustments grade;
    private Vignette vignette;
    private ParticleSystem dust, pollen;
    private Material dustMaterial, pollenMaterial;
    private EcoRegion lastRegion;
    private bool warnedSmog;

    private void OnEnable()
    {
        if (Current != null && Current != this) { enabled = false; return; }
        Current = this;
        CreateGrade();
        dust = CreateParticles("Smog dust", new Color(0.52f, 0.46f, 0.36f, 0.5f), new Color(0.38f, 0.34f, 0.3f, 0.7f), 0.05f, 0.14f, out dustMaterial);
        pollen = CreateParticles("Pollen and seeds", new Color(1f, 0.95f, 0.62f, 0.75f), new Color(0.9f, 1f, 0.8f, 0.9f), 0.04f, 0.08f, out pollenMaterial);
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
        if (volume != null) Destroy(volume.gameObject);
        if (profile != null) Destroy(profile);
        if (dust != null) Destroy(dust.gameObject);
        if (pollen != null) Destroy(pollen.gameObject);
        if (dustMaterial != null) Destroy(dustMaterial);
        if (pollenMaterial != null) Destroy(pollenMaterial);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextSample) { nextSample = Time.unscaledTime + 0.5f; Sample(); }
        float step = Time.unscaledDeltaTime / FadeSeconds;
        smog = Mathf.MoveTowards(smog, smogTarget, step);
        heat = Mathf.MoveTowards(heat, heatTarget, step * 0.5f);
        gloom = Mathf.MoveTowards(gloom, gloomTarget, step);
        life = Mathf.MoveTowards(life, lifeTarget, step);
        ApplyGrade();
        ApplyParticles();
    }

    // The clock sets the sun every Update; this runs after it and only scales that fresh value, so nothing accumulates.
    private void LateUpdate()
    {
        var clock = EcoWorldClock.Current;
        if (clock == null || clock.sun == null || !clock.driveLighting) return;
        float warm = Mathf.Clamp01(heat * 0.55f + smog * 0.35f);
        clock.sun.color *= Color.Lerp(Color.white, HeatColor, warm);
        clock.sun.intensity *= 1f - 0.35f * smog;
    }

    private void Sample()
    {
        if (focus == null || !focus.gameObject.activeInHierarchy)
        {
            var player = FindFirstObjectByType<PlayerController>();
            focus = player != null ? player.transform : Camera.main != null ? Camera.main.transform : null;
        }
        var region = focus != null ? EcoRegion.At(focus.position) : null;
        Scores(region, out float biodiversity, out float health);
        smogTarget = region != null ? SmogFor(region.AQI) : 0f;
        heatTarget = HeatFor(EcoRegion.WorldCarbon());
        gloomTarget = region != null ? GloomFor(health) : 0f;
        lifeTarget = region != null ? Mathf.InverseLerp(45f, 90f, biodiversity) : 0f;
        if (region != lastRegion) { lastRegion = region; warnedSmog = false; }
        // Say once per visit why the view closed in, so the haze reads as air pollution and not as weather.
        if (!warnedSmog && smogTarget >= 0.45f && region != null)
        {
            warnedSmog = true;
            var weapons = FindFirstObjectByType<PlayerWeaponSystem>();
            if (weapons != null) weapons.ShowFeedback(region.regionName + " · hava kirli (AQI " + region.AQI + "): duman görüşü kapatıyor");
        }
    }

    private void CreateGrade()
    {
        var go = new GameObject("Eco environment grade") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 50f; volume.weight = 1f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Eco environment grade";
        grade = profile.Add<ColorAdjustments>(true);
        vignette = profile.Add<Vignette>(true);
        vignette.color.value = new Color(0.22f, 0.17f, 0.1f);
        vignette.smoothness.value = 0.6f;
        volume.sharedProfile = profile;
    }

    private void ApplyGrade()
    {
        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) && !data.renderPostProcessing)
            data.renderPostProcessing = true;
        if (grade == null) return;
        // A sick region loses colour and light; the carbon load warms the whole picture a little.
        grade.saturation.value = Mathf.Lerp(6f, -62f, gloom);
        grade.contrast.value = Mathf.Lerp(4f, -14f, gloom);
        grade.postExposure.value = -0.35f * gloom - 0.15f * smog;
        grade.colorFilter.value = Color.Lerp(Color.white, new Color(1f, 0.93f, 0.8f), Mathf.Clamp01(heat * 0.6f + smog * 0.4f));
        vignette.intensity.value = Mathf.Lerp(0.08f, 0.38f, Mathf.Max(smog, gloom * 0.6f));
    }

    private ParticleSystem CreateParticles(string name, Color a, Color b, float minSize, float maxSize, out Material material)
    {
        material = null;
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.maxParticles = 1500; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.004f;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(46f, 9f, 46f);
        var noise = system.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
        var fade = system.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var emission = system.emission; emission.rateOverTime = 0f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0f; renderer.maxParticleSize = 0.03f;
        var source = Resources.Load<Material>("EcoEnvironment/Particle");
        if (source != null) { material = new Material(source) { name = name }; renderer.sharedMaterial = material; }
        system.Play();
        return system;
    }

    private void ApplyParticles()
    {
        if (focus == null) return;
        var center = focus.position + Vector3.up * 4f;
        if (dust != null)
        {
            dust.transform.position = center;
            var emission = dust.emission; emission.rateOverTime = smog * smog * 260f;
        }
        if (pollen != null)
        {
            pollen.transform.position = center;
            float day = EcoWorldClock.Current != null ? Mathf.Clamp01(EcoWorldClock.Current.SunHeight * 4f) : 1f;
            var emission = pollen.emission; emission.rateOverTime = life * (1f - smog) * 45f * day;
        }
    }
}
