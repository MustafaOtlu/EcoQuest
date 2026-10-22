using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class EcoWorldClock : MonoBehaviour
{
    private static EcoWorldClock current;
    public static EcoWorldClock Current
    {
        get
        {
            if (current != null && current.isActiveAndEnabled) return current;
            var player = FindFirstObjectByType<PlayerController>();
            Vector3 anchor = player != null ? player.transform.position : Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<EcoWorldClock>(FindObjectsSortMode.InstanceID))
            {
                if (!candidate.isActiveAndEnabled) continue;
                float distance = (candidate.transform.position - anchor).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; current = candidate;
            }
            return current;
        }
    }
    [Range(0f, 24f)] public float hour = 9f;
    [Min(60f)] public float dayLengthSeconds = 900f;
    public int day = 1;
    public bool advanceTime = true, varyingWeather = true, driveLighting = true;
    [Range(0f, 1f)] public float windStrength = 0.65f, cloudCover = 0.15f;
    public Light sun;
    public float RainIntensity => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.52f, 0.75f, cloudCover));
    public string WeatherName => RainIntensity > 0.65f && windStrength > 0.7f ? "Fırtına" : RainIntensity > 0.15f ? "Yağmurlu" : cloudCover > 0.3f ? "Bulutlu" : "Açık";
    /// <summary>GDD §10: the weather is a deterministic function of game time, so the HUD can warn ahead of a change.</summary>
    public string ForecastName(float seconds)
    {
        if (!varyingWeather) return WeatherName;
        float time = day * 24f + hour + Mathf.Max(0f, seconds) * 24f / Mathf.Max(60f, dayLengthSeconds);
        float phase = time * 0.08f;
        float wind = Mathf.Lerp(0.05f, 1f, Mathf.PerlinNoise(phase, 7.31f));
        float cloud = Mathf.SmoothStep(0.05f, 0.95f, Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(phase * 0.7f, 2.19f)));
        float rain = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.52f, 0.75f, cloud));
        return rain > 0.65f && wind > 0.7f ? "Fırtına" : rain > 0.15f ? "Yağmurlu" : cloud > 0.3f ? "Bulutlu" : "Açık";
    }
    public float SolarFactor => Mathf.Max(0f, Mathf.Sin((hour - 6f) * Mathf.PI / 12f)) * (1f - cloudCover * 0.8f);
    public Vector3 SunDirection => new Vector3(Mathf.Cos((hour - 6f) * Mathf.PI / 12f), Mathf.Sin((hour - 6f) * Mathf.PI / 12f), 0.25f).normalized;
    private float originalIntensity, originalAmbient;
    private Quaternion originalRotation;
    private Color originalColor = Color.white;
    private const float MoonLight = 0.28f;
    private static readonly Color MoonColor = new Color(0.62f, 0.72f, 0.95f);
    private static readonly Quaternion MoonRotation = Quaternion.Euler(58f, -35f, 0f);
    private EcoEveningLighting evening;
    private void OnEnable() => current = null;
    private void OnDisable() { if (current == this) current = null; }

    private void Start()
    {
        if (sun == null)
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { sun = light; break; }
        originalAmbient = RenderSettings.ambientIntensity;
        if (sun != null) { originalIntensity = sun.intensity; originalRotation = sun.transform.rotation; originalColor = sun.color; }
        // Enable the atmosphere in gameplay scenes; isolated simulation fixtures stay independent.
        if (Current == this && EcoAtmosphere.Current == null && FindFirstObjectByType<PlayerController>() != null)
        { var atmosphere = new GameObject("EcoQuest atmosphere").AddComponent<EcoAtmosphere>(); atmosphere.clock = this; }
        if (Current == this) EnsureEveningLighting();
    }
    private void Update()
    {
        if (Current != this) return;
        EnsureEveningLighting();
        if (advanceTime) Simulate(Time.deltaTime);
        if (!driveLighting || sun == null) return;
        // Night keeps a cool, high moonlight so the town stays readable; the flashlight and street lamps add to it, not replace it.
        float sunHeight = Mathf.Max(0f, Mathf.Sin((hour - 6f) * Mathf.PI / 12f));
        float daylight = Mathf.SmoothStep(0f, 1f, sunHeight * 4f); // clouds dim the sun but do not turn noon into moonlight
        sun.intensity = originalIntensity * Mathf.Max(SolarFactor, MoonLight);
        sun.color = Color.Lerp(MoonColor, originalColor, daylight);
        Quaternion sunRotation = sunHeight > 0f ? Quaternion.LookRotation(-SunDirection) : MoonRotation;
        sun.transform.rotation = Quaternion.Slerp(MoonRotation, sunRotation, daylight);
        RenderSettings.ambientIntensity = Mathf.Lerp(0.45f, originalAmbient, SolarFactor);
    }
    private void EnsureEveningLighting()
    {
        if (evening != null || FindFirstObjectByType<PlayerController>() == null) return;
        evening = GetComponent<EcoEveningLighting>();
        if (evening == null) evening = gameObject.AddComponent<EcoEveningLighting>();
        evening.clock = this;
    }
    public void Simulate(float seconds)
    {
        if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        float next = hour + seconds * 24f / Mathf.Max(60f, dayLengthSeconds);
        day += Mathf.FloorToInt(next / 24f);
        hour = Mathf.Repeat(next, 24f);
        if (!varyingWeather) return;
        float phase = (day * 24f + hour) * 0.08f;
        windStrength = Mathf.Lerp(0.05f, 1f, Mathf.PerlinNoise(phase, 7.31f));
        float humidity = Mathf.PerlinNoise(phase * 0.7f, 2.19f);
        cloudCover = Mathf.SmoothStep(0.05f, 0.95f, Mathf.InverseLerp(0.25f, 0.75f, humidity));
    }
    private void OnDestroy()
    {
        if (!driveLighting || (current != null && current != this)) return;
        if (sun != null) { sun.intensity = originalIntensity; sun.transform.rotation = originalRotation; sun.color = originalColor; }
        RenderSettings.ambientIntensity = originalAmbient;
    }
}
