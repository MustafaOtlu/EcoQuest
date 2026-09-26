using UnityEngine;

public sealed class EcoWindRotor : MonoBehaviour
{
    public EcoPowerNode node;
    private EcoWorldClock clock;
    private void Start() => clock = FindFirstObjectByType<EcoWorldClock>();
    private void Update()
    {
        if (clock != null && node != null && node.Operational)
            transform.Rotate(0, 0, -clock.windStrength * node.windExposure * 180f * Time.deltaTime, Space.Self);
    }
}
