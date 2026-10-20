using UnityEngine;

public sealed class EcoNoBuildZone : MonoBehaviour
{
    public Vector2 size = new Vector2(3,12);
    public bool Contains(Vector3 position, Vector2 margin)
    { var p = transform.InverseTransformPoint(position); return Mathf.Abs(p.x) < size.x * .5f + margin.x && Mathf.Abs(p.z) < size.y * .5f + margin.y; }
}
