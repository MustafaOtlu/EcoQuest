using UnityEngine;

/// <summary>Small physical resource drop left by a defeated enemy.</summary>
public sealed class EnemyLootDrop : MonoBehaviour
{
    private Vector3 basePosition;
    private float phase;
    // Pops out of the defeated enemy on an arc, bounces once, then bobs where it landed.
    private Vector3 launchFrom;
    private float flight = -1f;
    private const float FlightSeconds = 0.55f, BounceSeconds = 0.3f;

    public void Initialize(Vector3 position, int metalAmount, int plasticAmount) => Initialize(position, metalAmount, plasticAmount, position);

    public void Initialize(Vector3 position, int metalAmount, int plasticAmount, Vector3 from)
    {
        transform.position = from;
        basePosition = position;
        launchFrom = from;
        flight = (from - position).sqrMagnitude > 0.01f ? 0f : -1f;
        if (flight < 0f) transform.position = position;
        phase = Random.Range(0f, Mathf.PI * 2f);
        var resource = GetComponent<RecyclableResource>();
        if (resource == null) resource = gameObject.AddComponent<RecyclableResource>();
        resource.metal = metalAmount;
        resource.plastic = plasticAmount;
        resource.processingSeconds = 1.1f;
    }

    private void Update()
    {
        if (flight >= 0f)
        {
            flight += Time.deltaTime;
            if (flight < FlightSeconds)
            {
                float t = flight / FlightSeconds;
                transform.position = Vector3.Lerp(launchFrom, basePosition, t) + Vector3.up * (1.6f * 4f * t * (1f - t));
                transform.Rotate(new Vector3(1f, 0.6f, 0.3f), 720f * Time.deltaTime, Space.World);
                return;
            }
            float b = (flight - FlightSeconds) / BounceSeconds;
            if (b < 1f) { transform.position = basePosition + Vector3.up * (0.3f * 4f * b * (1f - b)); return; }
            flight = -1f;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            EcoLitterToss.Burst(basePosition, new Color32(255, 236, 120, 230), 12, 1.4f, 0.12f, 0.35f);
        }
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = basePosition + Vector3.up * (0.06f + Mathf.Sin(Time.time * 2.4f + phase) * 0.035f);
    }
}
