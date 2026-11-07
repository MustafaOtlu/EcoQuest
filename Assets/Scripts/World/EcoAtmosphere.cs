using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Owns only presentation and outdoor rain effects, leaving authored terrain and building colliders intact.
[DefaultExecutionOrder(1000)]
public sealed class EcoAtmosphere : MonoBehaviour
{
    public static EcoAtmosphere Current { get; private set; }
    public EcoWorldClock clock;
    public Camera viewCamera;
    [Min(50)] public float renderDistance = 500;
    [Min(10)] public float fogStart = 220;
    public bool automatic = true;
    public float Rain => clock != null ? clock.RainIntensity : 0;
    public ParticleSystem RainParticles => rain;
    public Material SkyMaterial => sky;
    private Material sky, rainMaterial, originalSky;
    private bool originalFog;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalFogStart, originalFogEnd, originalFogDensity;
    private readonly Dictionary<Camera, float> originalCameraDistances = new Dictionary<Camera, float>();
    private ParticleSystem rain;
    private Transform rainFocus;
    private PlantedSeed[] crops = System.Array.Empty<PlantedSeed>();
    private EcoFire[] fires = System.Array.Empty<EcoFire>();
    private float rainStep, nextCache;
    private bool initialized;

    private void OnEnable()
    {
        if (Current != null && Current != this) { enabled = false; return; }
        Current = this;
        originalSky = RenderSettings.skybox; originalFog = RenderSettings.fog; originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode; originalFogStart = RenderSettings.fogStartDistance;
        originalFogEnd = RenderSettings.fogEndDistance; originalFogDensity = RenderSettings.fogDensity;
        initialized = true;
        var shader = Resources.Load<Shader>("EcoAtmosphere/Sky");
        if (shader != null) { sky = new Material(shader) { name = "EcoQuest living sky" }; RenderSettings.skybox = sky; }
        CreateRain();
    }
    private void LateUpdate()
    {
        if (!automatic) return;
        ApplyView(); Simulate(Time.deltaTime);
    }
    public void ApplyView()
    {
        if (!isActiveAndEnabled) return;
        // Gameplay follows clock ownership changes; manual fixtures retain their assigned clock.
        if (automatic || clock == null) clock = EcoWorldClock.Current;
        if (viewCamera == null) viewCamera = Camera.main;
        float far = Mathf.Clamp(renderDistance, 50, 2000);
        float daylight = clock == null ? 1 : Mathf.Clamp01(clock.SunHeight);
        float cloud = clock != null ? Mathf.Clamp01(clock.cloudCover) : 0.2f;
        var horizon = Color.Lerp(new Color(0.055f, 0.075f, 0.13f), new Color(0.69f, 0.8f, 0.84f), daylight);
        horizon = Color.Lerp(horizon, new Color(0.36f, 0.43f, 0.47f), Rain * daylight * 0.65f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = horizon;
        RenderSettings.fogEndDistance = far * Mathf.Lerp(0.96f, 0.8f, Rain);
        RenderSettings.fogStartDistance = Mathf.Clamp(fogStart * Mathf.Lerp(1, 0.5f, Rain), 10, RenderSettings.fogEndDistance - 10);
        // A physical far plane stops draw submissions beyond the fog, including Cinemachine lens updates.
        if (viewCamera != null)
        {
            if (!originalCameraDistances.ContainsKey(viewCamera)) originalCameraDistances.Add(viewCamera, viewCamera.farClipPlane);
            viewCamera.farClipPlane = Mathf.Max(viewCamera.nearClipPlane + 1, far);
        }
        if (sky != null)
        {
            sky.SetColor("_Horizon", horizon);
            sky.SetColor("_Zenith", Color.Lerp(new Color(0.012f, 0.022f, 0.065f), new Color(0.12f, 0.39f, 0.66f), daylight));
            sky.SetColor("_CloudColor", Color.Lerp(new Color(0.09f, 0.12f, 0.19f), Color.Lerp(new Color(0.97f, 0.96f, 0.91f), new Color(0.34f, 0.39f, 0.43f), Rain), daylight));
            sky.SetVector("_SunDirection", clock != null ? clock.SunDirection : Vector3.up);
            sky.SetFloat("_CloudCover", cloud); sky.SetFloat("_Daylight", daylight);
            sky.SetFloat("_Wind", clock != null ? clock.windStrength : 0.5f);
        }
        if (rain == null) return;
        if (rainFocus == null && viewCamera != null)
        {
            var iso = viewCamera.GetComponent<EcoIsoCamera>();
            rainFocus = iso != null && iso.target != null ? iso.target : viewCamera.transform;
        }
        if (rainFocus != null) rain.transform.position = rainFocus.position + Vector3.up * 10f;
        // Keep the rain in world space near the scene the player sees, including under an orthographic camera.
        bool exposed = rainFocus != null && (Rain <= 0f || Outdoors(rainFocus.position + Vector3.up * 0.9f, rainFocus));
        var emission = rain.emission; emission.rateOverTime = exposed ? Rain * 655 : 0;
        if (!exposed) rain.Clear();
        var velocity = rain.velocityOverLifetime; velocity.enabled = true;
        velocity.x = clock != null ? clock.windStrength * 3 : 0; velocity.y = -14; velocity.z = 0.6f;
    }
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        rainStep += seconds;
        if (rainStep < 1) return;
        float step = rainStep; rainStep = 0;
        if (Rain <= 0) return;
        if (Time.time >= nextCache)
        {
            nextCache = Time.time + 2;
            crops = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None);
            fires = FindObjectsByType<EcoFire>(FindObjectsSortMode.None);
        }
        foreach (var crop in crops)
        {
            if (crop == null || !crop.isActiveAndEnabled || crop.IsWithered || !Outdoors(crop.transform.position, crop.transform)) continue;
            bool acid = EcoRegion.At(crop.transform.position)?.airPollution >= 55;
            if (acid) crop.ApplyAcid(Mathf.Min(0.1f, step * Rain * 0.004f));
            else crop.WaterPlant(Mathf.Min(1, step * Rain * 0.025f), true);
        }
        foreach (var fire in fires)
            if (fire != null && fire.isActiveAndEnabled && fire.Burning && Outdoors(fire.transform.position, fire.transform)) fire.Extinguish(step * Rain * 0.8f);
    }
    private static bool Outdoors(Vector3 point, Transform owner)
    {
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 0.2f, Vector3.up, 150, ~(1 << 2 | 1 << 5), QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(owner)) return false;
        return true;
    }
    private void CreateRain()
    {
        var go = new GameObject("Local rainfall"); go.transform.SetParent(transform, false);
        rain = go.AddComponent<ParticleSystem>(); rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = rain.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.15f, 1.4f); main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(0.028f, 0.046f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.57f, 0.76f, 0.84f, 0.37f), new Color(0.78f, 0.89f, 0.91f, 0.58f));
        main.maxParticles = 1800; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(30, 0.5f, 30);
        var emission = rain.emission; emission.rateOverTime = 0;
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.7f; renderer.velocityScale = 0.014f;
        renderer.minParticleSize = 0f; renderer.maxParticleSize = 0.012f;
        var source = Resources.Load<Material>("EcoEnvironment/Particle");
        if (source != null) { rainMaterial = new Material(source) { name = "Rain droplets" }; renderer.sharedMaterial = rainMaterial; }
        // Rain is local and uses collision to keep droplets out of buildings without thousands of objects.
        var collision = rain.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D; collision.quality = ParticleSystemCollisionQuality.Low;
        collision.collidesWith = ~(1 << 2 | 1 << 5); collision.lifetimeLoss = 1;
        rain.Play();
    }
    private void OnDisable()
    {
        if (!initialized) return;
        initialized = false;
        RenderSettings.skybox = originalSky; RenderSettings.fog = originalFog; RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode; RenderSettings.fogStartDistance = originalFogStart;
        RenderSettings.fogEndDistance = originalFogEnd; RenderSettings.fogDensity = originalFogDensity;
        foreach (var pair in originalCameraDistances) if (pair.Key != null) pair.Key.farClipPlane = pair.Value;
        originalCameraDistances.Clear();
        if (rain != null) { rain.gameObject.SetActive(false); Destroy(rain.gameObject); }
        if (sky != null) Destroy(sky); if (rainMaterial != null) Destroy(rainMaterial);
        if (Current == this) Current = null;
    }
}
