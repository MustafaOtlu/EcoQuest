using UnityEngine;

public sealed class EcoHydroSite : MonoBehaviour
{
    public float radius = 10f, naturalFlow = 1f, minimumFlow = 0.3f;
    public bool fishPass = true;
    private Canvas sign;
    private float nextCheck;
    public static EcoHydroSite At(Vector3 position)
    {
        foreach (var site in FindObjectsByType<EcoHydroSite>(FindObjectsSortMode.None))
            if (site.isActiveAndEnabled && Vector3.Distance(position, site.transform.position) <= site.radius) return site;
        return null;
    }
    // The "build here" sign would sit inside a finished plant; hide it while one stands on the site.
    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        if (sign == null) sign = GetComponentInChildren<Canvas>(true);
        if (sign == null) return;
        bool built = false;
        foreach (var plant in FindObjectsByType<EcoHydroPlant>(FindObjectsSortMode.None))
            if (plant.isActiveAndEnabled && At(plant.transform.position) == this) { built = true; break; }
        if (sign.enabled == built) sign.enabled = !built;
    }
}
