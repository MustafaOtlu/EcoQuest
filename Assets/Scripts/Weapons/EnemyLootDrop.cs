using UnityEngine;

/// <summary>Small physical resource drop left by a defeated enemy.</summary>
public sealed class EnemyLootDrop : MonoBehaviour
{
    private Vector3 basePosition;
    private float phase;

    public void Initialize(Vector3 position, int metalAmount, int plasticAmount)
    {
        transform.position = position;
        basePosition = position;
        phase = Random.Range(0f, Mathf.PI * 2f);
        var resource = GetComponent<RecyclableResource>();
        if (resource == null) resource = gameObject.AddComponent<RecyclableResource>();
        resource.metal = metalAmount;
        resource.plastic = plasticAmount;
        resource.processingSeconds = 1.1f;
    }

    private void Update()
    {
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = basePosition + Vector3.up * (0.06f + Mathf.Sin(Time.time * 2.4f + phase) * 0.035f);
    }
}
