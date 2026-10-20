using UnityEngine;

public sealed class EcoHydroSite : MonoBehaviour
{
    public float radius = 10f, naturalFlow = 1f, minimumFlow = 0.3f;
    public bool fishPass = true;
    public static EcoHydroSite At(Vector3 position)
    {
        foreach (var site in FindObjectsByType<EcoHydroSite>(FindObjectsSortMode.None))
            if (site.isActiveAndEnabled && Vector3.Distance(position, site.transform.position) <= site.radius) return site;
        return null;
    }
}
