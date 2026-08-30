using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DayNightCycle : MonoBehaviour
{
    [SerializeField] Light2D globalLight;
    [SerializeField] Gradient lightColorGradient;
    [SerializeField] AnimationCurve lightIntensityCurve;

    float currentTime;
    TimeOfDay currentPeriod;
    int dayCount = 1;

    public float NormalizedTime => currentTime;
    public TimeOfDay CurrentPeriod => currentPeriod;
    public int DayCount => dayCount;
    public bool IsDaytime => currentTime >= GameConstants.DAY_START && currentTime < GameConstants.DUSK_START;

    void Start()
    {
        currentTime = GameConstants.DAY_START;
        if (lightColorGradient == null || lightColorGradient.colorKeys.Length == 0)
            SetupDefaultGradient();
        if (lightIntensityCurve == null || lightIntensityCurve.length == 0)
            SetupDefaultCurve();
    }

    void Update()
    {
        currentTime += Time.deltaTime / GameConstants.DAY_DURATION_SECONDS;

        if (currentTime >= 1f)
        {
            currentTime -= 1f;
            dayCount++;
            GameEvents.DayStarted();
        }

        UpdatePeriod();
        UpdateLighting();
        GameEvents.TimeOfDayChanged(currentTime);
    }

    void UpdatePeriod()
    {
        TimeOfDay newPeriod;

        if (currentTime < GameConstants.DAWN_START)
            newPeriod = TimeOfDay.Night;
        else if (currentTime < GameConstants.DAY_START)
            newPeriod = TimeOfDay.Dawn;
        else if (currentTime < GameConstants.DUSK_START)
            newPeriod = TimeOfDay.Day;
        else if (currentTime < GameConstants.NIGHT_START)
            newPeriod = TimeOfDay.Dusk;
        else
            newPeriod = TimeOfDay.Night;

        if (newPeriod != currentPeriod)
        {
            currentPeriod = newPeriod;
            if (newPeriod == TimeOfDay.Night)
                GameEvents.NightStarted();
            else if (newPeriod == TimeOfDay.Day)
                GameEvents.DayStarted();
        }
    }

    void UpdateLighting()
    {
        if (globalLight == null) return;

        globalLight.color = lightColorGradient.Evaluate(currentTime);
        globalLight.intensity = lightIntensityCurve.Evaluate(currentTime);
    }

    void SetupDefaultGradient()
    {
        lightColorGradient = new Gradient();
        GradientColorKey[] colors = new GradientColorKey[5];
        colors[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f);
        colors[1] = new GradientColorKey(new Color(1f, 0.6f, 0.3f), GameConstants.DAWN_START);
        colors[2] = new GradientColorKey(Color.white, GameConstants.DAY_START + 0.1f);
        colors[3] = new GradientColorKey(new Color(1f, 0.5f, 0.2f), GameConstants.DUSK_START);
        colors[4] = new GradientColorKey(new Color(0.1f, 0.1f, 0.3f), GameConstants.NIGHT_START);
        GradientAlphaKey[] alphas = { new(1f, 0f), new(1f, 1f) };
        lightColorGradient.SetKeys(colors, alphas);
    }

    void SetupDefaultCurve()
    {
        lightIntensityCurve = new AnimationCurve();
        lightIntensityCurve.AddKey(0f, 0.15f);
        lightIntensityCurve.AddKey(GameConstants.DAWN_START, 0.4f);
        lightIntensityCurve.AddKey(GameConstants.DAY_START, 0.9f);
        lightIntensityCurve.AddKey(0.5f, 1f);
        lightIntensityCurve.AddKey(GameConstants.DUSK_START, 0.7f);
        lightIntensityCurve.AddKey(GameConstants.NIGHT_START, 0.15f);
        lightIntensityCurve.AddKey(1f, 0.15f);
    }

    public float GetSolarEfficiency()
    {
        if (!IsDaytime) return 0f;
        float midDay = (GameConstants.DAY_START + GameConstants.DUSK_START) / 2f;
        float distFromMid = Mathf.Abs(currentTime - midDay);
        float halfDay = (GameConstants.DUSK_START - GameConstants.DAY_START) / 2f;
        return Mathf.Clamp01(1f - (distFromMid / halfDay));
    }
}
