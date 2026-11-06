using UnityEngine;

// Authored site data, not playthrough progress. Original props stay referenced for legacy save restoration.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class EcoEnergyTutorialSite : MonoBehaviour
{
    public Transform[] legacyObjects = new Transform[0];
    public Vector3 solarPosition, batteryPosition, chargerPosition;
    public bool configured;

    public Vector3 Anchor => (solarPosition + batteryPosition + chargerPosition) / 3f;
    public Vector3 SolarPosition => solarPosition;
    public Vector3 BatteryPosition => batteryPosition;
    public Vector3 ChargerPosition => chargerPosition;

    private void Awake() => HideLegacy();

    public void HideLegacy()
    {
        foreach (var item in legacyObjects)
            if (item != null && item != transform && item.gameObject.activeSelf) item.gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!configured) return;
        Gizmos.color = new Color(0.35f, 0.8f, 0.55f, 0.8f);
        foreach (var point in new[] { solarPosition, batteryPosition, chargerPosition })
            Gizmos.DrawWireCube(point + Vector3.up * 0.03f, new Vector3(2f, 0.05f, 2f));
        Gizmos.DrawLine(solarPosition, batteryPosition);
        Gizmos.DrawLine(batteryPosition, chargerPosition);
    }
}
