using UnityEngine;

public sealed class EcoWindRotor : MonoBehaviour
{
    public EcoPowerNode node;
    private void Update()
    {
        var clock = EcoWorldClock.Current;
        if (clock != null && node != null && node.Operational)
            transform.Rotate(0, 0, -clock.windStrength * node.windExposure * 180f * Time.deltaTime, Space.Self);
    }
}
