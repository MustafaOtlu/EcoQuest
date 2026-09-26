using System.Linq;
using UnityEngine;

[RequireComponent(typeof(EcoWaterNode), typeof(PlantingSurface))]
[DefaultExecutionOrder(50)]
public sealed class EcoFarm : MonoBehaviour
{
    public Vector2 growingArea = new Vector2(2.5f, 2.5f);
    public float moisturePerLitre = 0.04f;
    public float irrigationPerSecond = 4;
    private EcoWaterNode water;
    private void Awake() => water = GetComponent<EcoWaterNode>();
    private void Update() => Simulate(Time.deltaTime);
    public bool Contains(PlantedSeed plant)
    {
        if (plant == null) return false;
        Vector3 point = transform.InverseTransformPoint(plant.transform.position);
        return Mathf.Abs(point.x) < growingArea.x * 0.5f && Mathf.Abs(point.z) < growingArea.y * 0.5f && point.y >= -0.05f && point.y < 0.6f;
    }
    public bool HasCrops => FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).Any(Contains);
    public void Simulate(float seconds)
    {
        if (water == null || !water.Operational || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || moisturePerLitre <= 0) return;
        var crops = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).Where(p => Contains(p) && !p.IsWithered && p.moisture < 0.8f).ToArray();
        if (crops.Length == 0) return;
        // Healthy irrigation uses the clean chamber only; dirty water must be treated first.
        float requested = crops.Sum(p => (0.8f - p.moisture) / moisturePerLitre);
        float supplied = water.Take(EcoWaterNode.Fluid.Clean, Mathf.Min(requested, irrigationPerSecond * seconds));
        foreach (var crop in crops) crop.WaterPlant(supplied * ((0.8f - crop.moisture) / moisturePerLitre) / requested * moisturePerLitre, true);
    }
    public string Describe() => "Tarla • Tohum silahıyla toprağa ekim yap\nTemiz su " + water.cleanWater.ToString("0.0") + "/" + water.capacity.ToString("0") + " • Depoya temiz su borusu bağla";
}
