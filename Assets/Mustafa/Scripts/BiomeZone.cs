using UnityEngine;

public class BiomeZone : MonoBehaviour
{
    [SerializeField] BiomeType biomeType;
    [SerializeField] Color gizmoColor = Color.green;

    BoxCollider2D zoneCollider;

    public BiomeType BiomeType => biomeType;

    void Awake()
    {
        zoneCollider = GetComponent<BoxCollider2D>();
        if (zoneCollider == null)
        {
            zoneCollider = gameObject.AddComponent<BoxCollider2D>();
            zoneCollider.isTrigger = true;
        }
    }

    public bool ContainsPoint(Vector2 point)
    {
        return zoneCollider.OverlapPoint(point);
    }

    public float GetWindPotential()
    {
        return biomeType == BiomeType.Windy ? 1f : 0.2f;
    }

    public float GetSunPotential()
    {
        return biomeType == BiomeType.Sunny ? 1f : 0.5f;
    }

    public float GetWaterAvailability()
    {
        return biomeType == BiomeType.FreshWater ? 1f : 0.1f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.offset, col.size);
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(col.offset, col.size);
        }
    }
}
