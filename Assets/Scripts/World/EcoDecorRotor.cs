using UnityEngine;

// Purely decorative rotor for scenery wind turbines. It follows the world wind so the
// wind farm visibly reacts to weather, but it produces no power and has no gameplay effect.
public sealed class EcoDecorRotor : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float degreesPerSecond = 160f;
    private float phase;

    private void Start() => phase = Random.value * 0.4f;

    private void Update()
    {
        var clock = EcoWorldClock.Current;
        float wind = clock != null ? clock.windStrength : 0.5f;
        transform.Rotate(axis, degreesPerSecond * (0.1f + wind) * (1f + phase) * Time.deltaTime, Space.Self);
    }
}
