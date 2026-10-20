using UnityEngine;
using UnityEngine.Serialization;

public sealed class PlayerVitals : MonoBehaviour
{
    [FormerlySerializedAs("maximumHealth"), Min(1f)] public float maximumEnergy = 100f;
    public bool showLegacyHUD = true;
    public const float RecoveryDuration = 5f;
    // GDD §15.5: recovering costs a part of the tool electricity, then grants brief invulnerability.
    public const float RecoveryElectricityCost = 0.2f;
    public const float RecoveryShieldSeconds = 3f;
    public const float RecoveryPushRadius = 9f;
    public bool IsShielded => Time.time < shieldUntil;
    public float ShieldRemaining => IsShielded ? shieldUntil - Time.time : 0f;
    private float shieldUntil, lastDamageAt = -100f;
    public float Energy { get; private set; }
    public bool IsRecovering { get; private set; }
    public float RecoveryRemaining => IsRecovering ? Mathf.Max(0f, RecoveryDuration - recoveryElapsed) : 0f;
    public float MovementMultiplier => IsRecovering ? 0f : Time.time < slowedUntil ? 0.65f : 1f;
    private float slowedUntil, smokeUntil, hitUntil, recoveryElapsed;
    public Vector3 AimPoint => transform.position + Vector3.up * 0.65f;

    private void Awake() => Energy = Mathf.Max(1f, maximumEnergy);
    public void ReceiveHit(float damage, float slowSeconds = 0f, float smokeSeconds = 0f)
    {
        if (IsRecovering || IsShielded) return;
        Energy = Mathf.Max(0f, Energy - Mathf.Max(0f, damage));
        if (damage > 0) lastDamageAt = Time.time;
        slowedUntil = Mathf.Max(slowedUntil, Time.time + Mathf.Max(0f, slowSeconds));
        smokeUntil = Mathf.Max(smokeUntil, Time.time + Mathf.Max(0f, smokeSeconds));
        if (damage > 0f) hitUntil = Time.time + 0.25f;
        if (Energy > 0f) return;
        IsRecovering = true;
        recoveryElapsed = 0f;
        slowedUntil = smokeUntil = hitUntil = 0f;
        var weapons = GetComponentInChildren<PlayerWeaponSystem>(true);
        if (weapons != null) weapons.SpendFraction(RecoveryElectricityCost);
    }

    private void FinishRecovery()
    {
        shieldUntil = Time.time + RecoveryShieldSeconds;
        // Nearby enemies back off so the player is not knocked down again at once.
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        {
            Vector3 away = enemy.transform.position - transform.position; away.y = 0f;
            if (away.sqrMagnitude > RecoveryPushRadius * RecoveryPushRadius) continue;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            enemy.ApplyKnockback(away.normalized, 2.5f);
        }
    }
    private void Update()
    {
        SimulateRecovery(Time.deltaTime);
        if (!IsRecovering && Time.time >= lastDamageAt + 8f) Energy = Mathf.MoveTowards(Energy, maximumEnergy, 5f * Time.deltaTime);
    }

    public void SimulateRecovery(float seconds)
    {
        if (!IsRecovering || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        recoveryElapsed = Mathf.Min(RecoveryDuration, recoveryElapsed + seconds);
        Energy = Mathf.Max(1f, maximumEnergy) * (recoveryElapsed / RecoveryDuration);
        if (recoveryElapsed >= RecoveryDuration) { IsRecovering = false; FinishRecovery(); }
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
            GUI.color = new Color(1f, 0.65f, 0.1f, 0.15f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        if (showLegacyHUD)
        {
            GUI.Box(new Rect(16, 16, 240, 28), "Karakter enerjisi: " + Mathf.CeilToInt(Energy) + " / " + maximumEnergy);
            if (IsRecovering) GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height / 2f - 20, 360, 40),
                "Toparlanıyorsun… " + RecoveryRemaining.ToString("0.0") + " sn");
        }
        GUI.color = saved;
    }
}
