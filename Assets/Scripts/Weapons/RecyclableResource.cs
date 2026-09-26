using UnityEngine;

public sealed class RecyclableResource : MonoBehaviour
{
    [Min(0)] public int metal = 2;
    [Min(0)] public int plastic = 1;
    [Min(0)] public int organic;
    [Min(0.1f)] public float processingSeconds = 1.5f;
    public bool Claimed { get; private set; }
    public bool TryClaim() { if (Claimed) return false; Claimed = true; gameObject.SetActive(false); Destroy(gameObject); return true; }
}
