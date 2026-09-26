#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Copy into the isolated validation project's Assets before running RecoveryCheck.Begin.
public sealed class RecoveryCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Recovery check").AddComponent<RecoveryCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/RecoveryValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var vitals = FindFirstObjectByType<PlayerVitals>();
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        Check(vitals != null && weapons != null, "MainScene has both independent energy systems");
        vitals.enabled = false; weapons.enabled = false;
        vitals.GetComponent<PlayerController>().enabled = false;
        var position = vitals.transform.position;
        var rotation = vitals.transform.rotation;
        float battery = weapons.Energy;
        int seeds = weapons.Seeds, metal = weapons.Metal, ammo = weapons.MetalAmmo;
        vitals.ReceiveHit(10000f);
        Check(vitals.IsRecovering && vitals.Energy == 0f && Mathf.Approximately(vitals.RecoveryRemaining, 5f), "Exhaustion starts five-second recovery");
        Check(weapons.Energy == battery && weapons.Seeds == seeds && weapons.Metal == metal && weapons.MetalAmmo == ammo, "Exhaustion preserves battery and inventory");
        vitals.SimulateRecovery(2.5f);
        Check(vitals.IsRecovering && Mathf.Approximately(vitals.Energy, vitals.maximumEnergy * 0.5f), "Energy fills progressively and controls stay locked at half capacity");
        vitals.ReceiveHit(10000f, 9f, 9f);
        Check(Mathf.Approximately(vitals.Energy, vitals.maximumEnergy * 0.5f) && Mathf.Approximately(vitals.RecoveryRemaining, 2.5f), "Incoming attacks cannot drain or restart recovery");
        Check(vitals.MovementMultiplier == 0f, "Recovery blocks movement");
        weapons.Use(0.25f);
        Check(weapons.Energy == battery && weapons.Seeds == seeds && weapons.MetalAmmo == ammo, "Recovery blocks actual weapon use even after energy becomes positive");
        var hud = FindFirstObjectByType<EcoQuestHUD>();
        typeof(EcoQuestHUD).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hud, null);
        var labels = GameObject.Find("EcoQuest HUD").GetComponentsInChildren<Text>();
        Check(labels.Any(t => t.text.StartsWith("KARAKTER ENERJİSİ")) && labels.Any(t => t.text.StartsWith("SİLAH ELEKTRİĞİ")), "HUD labels distinguish character energy from weapon electricity");
        Check(labels.Any(t => t.text.StartsWith("Toparlanıyorsun")), "HUD shows recovery countdown");
        vitals.SimulateRecovery(2.49f);
        Check(vitals.IsRecovering, "Recovery does not finish before five seconds");
        vitals.SimulateRecovery(0.02f);
        Check(!vitals.IsRecovering && vitals.Energy == vitals.maximumEnergy && vitals.MovementMultiplier == 1f, "Full control returns at five seconds");
        Check(vitals.transform.position == position && vitals.transform.rotation == rotation, "Recovery never teleports or rotates the player");
        vitals.ReceiveHit(10f);
        Check(Mathf.Approximately(vitals.Energy, vitals.maximumEnergy - 10f), "Normal hits resume after recovery");
        vitals.SimulateRecovery(20f);
        Check(Mathf.Approximately(vitals.Energy, vitals.maximumEnergy - 10f), "Partial energy does not trigger the exhaustion recovery cycle");
        float personal = vitals.Energy;
        weapons.SelectTool(0); weapons.Use(0.25f);
        Check(weapons.Energy < battery && vitals.Energy == personal, "Weapon electricity use never drains character energy");
        File.WriteAllText("recovery-check-success.txt", report);
        EditorApplication.Exit(0);
    }
    private void Check(bool ok, string label)
    {
        if (!ok) throw new Exception(label);
        report += "PASS " + label + "\n";
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;
        File.WriteAllText("recovery-check-failed.txt", report + message + "\n" + stack);
        EditorApplication.Exit(1);
    }
}
#endif
