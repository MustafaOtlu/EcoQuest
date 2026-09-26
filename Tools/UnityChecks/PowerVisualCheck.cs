#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PowerVisualCheck : MonoBehaviour
{
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Power visual check").AddComponent<PowerVisualCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/PowerVisualValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            File.WriteAllText("power-visual-failed.txt", message + "\n" + stack); EditorApplication.Exit(1);
        };
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var player = FindFirstObjectByType<PlayerController>(); player.enabled = false;
        var camera = Camera.main;
        foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); weapon.enabled = false; weapon.holder.Unequip();
        var clock = FindFirstObjectByType<EcoWorldClock>(); clock.advanceTime = false; clock.hour = 11; clock.cloudCover = 0.15f;
        camera.transform.position = new Vector3(3.5f, 5f, 3); camera.transform.LookAt(new Vector3(-4, 1.5f, -4)); camera.fieldOfView = 58;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        var hud = GameObject.Find("EcoQuest HUD").GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = 0.5f;
        yield return null; yield return null; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes("power-starter-preview.png", image.EncodeToPNG());
        var vitals = player.GetComponent<PlayerVitals>(); vitals.enabled = false; vitals.ReceiveHit(10000); vitals.SimulateRecovery(2.5f);
        yield return new WaitForSeconds(0.15f); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply(); File.WriteAllBytes("recovery-hud-preview.png", image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(image);
        File.WriteAllText("power-visual-success.txt", "MainScene power equipment and recovery HUD rendered."); EditorApplication.Exit(0);
    }
}
#endif
