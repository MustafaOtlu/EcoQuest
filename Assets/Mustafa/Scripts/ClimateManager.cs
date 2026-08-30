using UnityEngine;
using System.Collections;

/// <summary>
/// Iklim Degisikligi ve YEP Kaybi Yonetimi (Mustafa - Asama 4)
/// GDD: Havadaki karbon artarsa sicaklik artar, buzullar erir, asit yagmurlari baslar.
/// YEP Sistemi: Cevresel durum YEP'i dusurur. Eksiye duserse oyun biter.
/// </summary>
public class ClimateManager : MonoBehaviour
{
    public static ClimateManager Instance { get; private set; }

    [Header("Ayarlar")]
    public float updateInterval = 5f;
    
    [Header("Asit Yagmuru")]
    public bool isAcidRainActive = false;
    public float acidRainDamagePerTick = 2f;
    public ParticleSystem acidRainEffect;

    [Header("YEP Kaybi")]
    public float baseYepDrain = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(ClimateLoop());
    }

    IEnumerator ClimateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            ProcessClimateChains();
            ProcessYEPDrain();
        }
    }

    void ProcessClimateChains()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 1. Karbon -> Sicaklik
        if (gm.carbonFootprint > 70f)
        {
            gm.UpdateIndicator(IndicatorType.Temperature, 0.5f);
        }
        else if (gm.carbonFootprint < 30f)
        {
            gm.UpdateIndicator(IndicatorType.Temperature, -0.2f);
        }

        // 2. Hava Kirliligi -> Asit Yagmuru
        if (gm.airQuality < 20f && !isAcidRainActive)
        {
            StartAcidRain();
        }
        else if (gm.airQuality >= 40f && isAcidRainActive)
        {
            StopAcidRain();
        }

        // 3. Sicaklik -> Buzullar erir (Sembolik olarak su seviyesi/kalitesi etkilenir)
        if (gm.temperature > 30f)
        {
            gm.UpdateIndicator(IndicatorType.WaterQuality, -0.5f);
            // TODO: Biyom alanlarini kucultme (GDD'de var)
        }
    }

    void StartAcidRain()
    {
        isAcidRainActive = true;
        if (acidRainEffect != null) acidRainEffect.Play();
        Debug.Log("Asit yagmurlari basladi!");
    }

    void StopAcidRain()
    {
        isAcidRainActive = false;
        if (acidRainEffect != null) acidRainEffect.Stop();
        Debug.Log("Asit yagmurlari durdu.");
    }

    void ProcessYEPDrain()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Kotu gostergeler YEP'i dusurur
        float drain = 0f;
        if (gm.airQuality < 30f) drain += baseYepDrain;
        if (gm.waterQuality < 30f) drain += baseYepDrain;
        if (gm.carbonFootprint > 70f) drain += baseYepDrain;
        if (gm.ecosystemHealth < 30f) drain += baseYepDrain;

        if (drain > 0f)
        {
            gm.AddYEP(-drain);
        }

        // Oyun Bitti kontrolu
        if (gm.yepPoints < 0f)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        Debug.LogError("YEP EKSIYE DUSTU! OYUN BITTI!");
        // TODO: UI goster
        Time.timeScale = 0f; // Oyunu durdur
    }
}
