using System.Linq;
using UnityEngine;

[RequireComponent(typeof(EcoPowerNode))]
public sealed class EcoHydroPlant : MonoBehaviour
{
    public EcoHydroSite site;
    private void Start() { site = EcoHydroSite.At(transform.position); }
    public float Generation(EcoWorldClock clock)
    {
        if (site == null) site = EcoHydroSite.At(transform.position);
        if (site == null || !site.fishPass || clock == null) return 0;
        float flow = site.naturalFlow * Mathf.Lerp(0.45f, 1f, clock.RainIntensity);
        if (flow < site.minimumFlow) return 0;
        int users = FindObjectsByType<EcoHydroPlant>(FindObjectsSortMode.None).Count(p => p.isActiveAndEnabled && (p.site == site || EcoHydroSite.At(p.transform.position) == site));
        return 6f * Mathf.Clamp01((flow - site.minimumFlow) / Mathf.Max(0.01f, 1f - site.minimumFlow)) / Mathf.Max(1, users);
    }
}
