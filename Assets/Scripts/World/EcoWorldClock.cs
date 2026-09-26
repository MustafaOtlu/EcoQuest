using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class EcoWorldClock : MonoBehaviour
{
    [Range(0f, 24f)] public float hour = 9f;
    [Min(60f)] public float dayLengthSeconds = 900f;
    public int day = 1;
    public bool advanceTime = true, varyingWeather = true, driveLighting = true;
    [Range(0f, 1f)] public float windStrength = 0.65f, cloudCover = 0.15f;
    public Light sun;
    public float SolarFactor => Mathf.Max(0f, Mathf.Sin((hour - 6f) * Mathf.PI / 12f)) * (1f - cloudCover * 0.8f);
    public Vector3 SunDirection => new Vector3(Mathf.Cos((hour - 6f) * Mathf.PI / 12f), Mathf.Sin((hour - 6f) * Mathf.PI / 12f), 0.25f).normalized;
    private float originalIntensity, originalAmbient;
    private Quaternion originalRotation;

    private void Start()
    {
        if (sun == null)
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { sun = light; break; }
        originalAmbient = RenderSettings.ambientIntensity;
        if (sun != null) { originalIntensity = sun.intensity; originalRotation = sun.transform.rotation; }
    }
    private void Update()
    {
        if (advanceTime) Simulate(Time.deltaTime);
        if (!driveLighting || sun == null) return;
        sun.intensity = originalIntensity * SolarFactor;
        sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
        RenderSettings.ambientIntensity = Mathf.Lerp(0.2f, originalAmbient, SolarFactor);
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
        cloudCover = Mathf.PerlinNoise(phase * 0.7f, 2.19f) * 0.75f;
    }
    private void OnDestroy()
    {
        if (!driveLighting) return;
        if (sun != null) { sun.intensity = originalIntensity; sun.transform.rotation = originalRotation; }
        RenderSettings.ambientIntensity = originalAmbient;
    }
}
