using UnityEngine;

// Used when an enemy asset has no supplied death animation clip.
public sealed class EnemyDefeatMotion : MonoBehaviour
{
    private float elapsed;
    private Vector3 scale;
    private Quaternion rotation;
    private EcoEnemyAppearance appearance;
    private void Awake()
    {
        scale = transform.localScale; rotation = transform.rotation;
        appearance = GetComponent<EcoEnemyAppearance>();
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
    }
    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / 0.65f);
        if (appearance != null) appearance.Defeat(t);
        else
        {
            transform.rotation = rotation * Quaternion.Euler(0, 0, Mathf.SmoothStep(0, 65, t));
            transform.localScale = scale * Mathf.Lerp(1, 0.05f, t * t);
        }
        if (t >= 1) Destroy(gameObject);
    }
}
