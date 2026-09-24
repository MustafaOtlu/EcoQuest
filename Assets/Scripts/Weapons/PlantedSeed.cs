using UnityEngine;

public sealed class PlantedSeed : MonoBehaviour
{
    [Min(1f)] public float growthSeconds = 60f;
    [Range(0f, 1f)] public float moisture = 0.12f;
    [Range(0f, 1f)] public float growth;
    [Range(0f, 1f)] public float health = 1f;
    public bool contaminated;
    public bool IsMature => growth >= 1f && !IsWithered;
    public bool IsWithered => health <= 0f;
    public string Status => IsWithered ? "Kurumuş bitki" : IsMature ? "Hasada hazır"
        : moisture < 0.2f ? "Su gerekiyor" : "Büyüyor";
    private float dryTime;
    private bool harvested;
    private Vector3 baseScale, basePosition;
    private Renderer plantRenderer;
    private MaterialPropertyBlock tint;
    private void Awake()
    {
        baseScale = transform.localScale; basePosition = transform.position;
        plantRenderer = GetComponent<Renderer>();
        tint = new MaterialPropertyBlock();
    }
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (harvested || IsWithered || seconds <= 0f) return;
        moisture = Mathf.Max(0f, moisture - seconds * 0.008f);
        if (moisture >= 0.2f)
        {
            dryTime = 0f;
            growth = Mathf.Min(1f, growth + seconds / Mathf.Max(1f, growthSeconds) * (contaminated ? 0.5f : 1f));
            health = Mathf.Min(1f, health + seconds * 0.015f);
        }
        else
        {
            float previous = dryTime; dryTime += seconds;
            health = Mathf.Max(0f, health - (Mathf.Max(0f, dryTime - 15f) - Mathf.Max(0f, previous - 15f)) * 0.02f);
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
    }
    public bool TryHarvest(out int seeds, out int food)
    {
        seeds = food = 0;
        if (!IsMature || harvested) return false;
        harvested = true;
        seeds = contaminated ? 1 : 3; food = contaminated ? 0 : 1;
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
        growth = Mathf.Min(1f, growth + 0.2f); health = Mathf.Min(1f, health + 0.25f); return true;
    }
}
