using UnityEngine;

// Turns the hydro wheel with the plant's real output and lights the roof lamp: green producing, amber idle.
public sealed class EcoHydroWheel : MonoBehaviour
{
    public Transform wheel;
    public Renderer lamp;
    private EcoPowerNode node;
    private float output, nextSample;
    private static readonly Color On = new Color(.35f, .95f, .45f), Idle = new Color(.95f, .65f, .2f);

    private void Awake() => node = GetComponent<EcoPowerNode>();

    private void Update()
    {
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + .5f;
            output = node != null && node.Operational ? node.Generation(EcoWorldClock.Current) : 0f;
            if (lamp != null) lamp.sharedMaterial = EcoHydroArt.MaterialFor(output > .01f ? On : Idle);
        }
        if (wheel != null) wheel.Rotate(Vector3.right, output * 30f * Time.deltaTime, Space.Self);
    }
}
