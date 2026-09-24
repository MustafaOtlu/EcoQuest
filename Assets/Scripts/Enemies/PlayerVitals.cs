using UnityEngine;

public sealed class PlayerVitals : MonoBehaviour
{
    public float maximumHealth = 100f;
    public bool showLegacyHUD = true;
    public float Health { get; private set; }
    public bool IsDead => Health <= 0f;
    public float MovementMultiplier => Time.time < slowedUntil ? 0.65f : 1f;
    private float slowedUntil, smokeUntil, hitUntil, respawnAt;
    private Vector3 spawn;
    private Quaternion facing;
    public Vector3 AimPoint => transform.position + Vector3.up * 0.65f;

    private void Awake() { Health = maximumHealth; spawn = transform.position; facing = transform.rotation; }
    public void ReceiveHit(float damage, float slowSeconds = 0f, float smokeSeconds = 0f)
    {
        if (IsDead) return;
        Health = Mathf.Max(0f, Health - Mathf.Max(0f, damage));
        slowedUntil = Mathf.Max(slowedUntil, Time.time + slowSeconds);
        smokeUntil = Mathf.Max(smokeUntil, Time.time + smokeSeconds);
        if (damage > 0f) hitUntil = Time.time + 0.25f;
        if (IsDead) respawnAt = Time.time + 3f;
    }
    private void Update()
    {
        if (!IsDead || Time.time < respawnAt) return;
        var capsule = GetComponent<CharacterController>();
        if (capsule != null) capsule.enabled = false;
        transform.SetPositionAndRotation(spawn, facing);
        if (capsule != null) capsule.enabled = true;
        Health = maximumHealth;
        slowedUntil = smokeUntil = hitUntil = 0f;
    }
    private void OnGUI()
    {
        Color saved = GUI.color;
        if (Time.time < smokeUntil)
        {
            GUI.color = new Color(0.12f, 0.13f, 0.14f, 0.4f * Mathf.Clamp01(smokeUntil - Time.time));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        if (Time.time < hitUntil)
        {
            GUI.color = new Color(0.8f, 0f, 0f, 0.15f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        if (showLegacyHUD)
        {
            GUI.Box(new Rect(16, 16, 140, 28), "Can: " + Mathf.CeilToInt(Health) + " / " + maximumHealth);
            if (IsDead) GUI.Box(new Rect(Screen.width / 2f - 150, Screen.height / 2f - 20, 300, 40), "Yeniden basliyorsun...");
        }
        GUI.color = saved;
    }
}
