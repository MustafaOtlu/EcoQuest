#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

// Runs only in the isolated project: source MainScene is opened byte-for-byte and saved as a validation copy.
public sealed class MainSceneReviewCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var check = new GameObject("MainScene review").AddComponent<MainSceneReviewCheck>();
        EditorSceneManager.SaveScene(check.gameObject.scene, "Assets/MainSceneReviewValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Check(players.Length == 1, "Actual MainScene has one active player");
        var player = players[0]; Cursor.lockState = CursorLockMode.None;
        var camera = Camera.main;
        Check(camera != null && camera.isActiveAndEnabled, "MainScene has an active world camera");
        report += "PLAYER " + player.transform.position + " CAMERA " + camera.transform.position + "\n";
        foreach (var behaviour in camera.GetComponents<Behaviour>()) if (behaviour is not Camera) behaviour.enabled = false;
        var terrain = FindFirstObjectByType<Terrain>();
        Check(terrain != null && terrain.terrainData != null, "The user's actual terrain is loaded");
        report += "TERRAIN " + terrain.transform.position + " size=" + terrain.terrainData.size + "\n";
        var atmosphere = EcoAtmosphere.Current;
        Check(atmosphere != null && atmosphere.clock != null, "MainScene starts the atmosphere through its real world clock");
        Check(atmosphere.clock == EcoWorldClock.Current && Vector3.Distance(atmosphere.clock.transform.position, player.transform.position) < 50,
            "The atmosphere chooses the world clock beside the active gameplay area");
        atmosphere.clock.advanceTime = false; atmosphere.clock.varyingWeather = false;
        atmosphere.clock.hour = 10; atmosphere.clock.cloudCover = 0.2f;
        atmosphere.ApplyView();
        Check(RenderSettings.skybox == atmosphere.SkyMaterial && atmosphere.SkyMaterial != null, "MainScene uses the custom cloud sky");
        Check(camera.farClipPlane <= 500.1f && RenderSettings.fog, "MainScene applies fog and the actual 500-unit far plane");
        var weapons = player.GetComponentInChildren<PlayerWeaponSystem>();
        Check(weapons != null && weapons.holder != null && weapons.GetComponentInChildren<WeaponVisualEffects>() != null, "The real equipped weapon uses particle effects");
        var hud = GameObject.Find("EcoQuest HUD")?.GetComponent<Canvas>();
        Check(hud != null && hud.transform.Find("Vitals/Weather") != null, "MainScene starts the refreshed HUD and weather readout");
        Check(FindObjectsByType<EcoEnemySpawnZone>(FindObjectsSortMode.None).Length > 0, "Regional spawn rules are connected to MainScene");
        float deadline = Time.time + 15;
        while ((EcoNavigation.Current == null || !EcoNavigation.Current.Ready) && Time.time < deadline) yield return null;
        Check(EcoNavigation.Current != null && EcoNavigation.Current.Ready && NavMesh.SamplePosition(player.transform.position, out _, 3, NavMesh.AllAreas),
            "Runtime navigation covers the actual player on the translated terrain plateau");
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
        Check(enemies.Length >= 5, "All five opening enemy types are instantiated in the authored scene");
        foreach (var enemy in enemies)
        {
            enemy.enabled = false;
            Check(Vector3.Distance(enemy.transform.position, player.transform.position) < 45,
                enemy.species + " opening position follows the translated gameplay group");
        }
        foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (renderer.enabled && renderer.bounds.SqrDistance(camera.transform.position) < 4)
                report += "NEAR_RENDERER " + Hierarchy(renderer.transform) + " bounds=" + renderer.bounds + " shadow=" + renderer.shadowCastingMode + "\n";
        Directory.CreateDirectory("main-scene-images");
        // Capture the authored first-person view, then a review overview of this exact terrain and buildings.
        yield return new WaitForSeconds(0.15f); yield return Capture(camera, "first-person", hud);
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        camera.transform.position = player.transform.position + new Vector3(40, 45, -55);
        camera.transform.LookAt(player.transform.position + Vector3.up * 2);
        atmosphere.ApplyView(); yield return Capture(camera, "map-overview", hud);
        camera.transform.position = position; camera.transform.rotation = rotation;
        atmosphere.clock.cloudCover = 0.75f; atmosphere.clock.windStrength = 0.9f; atmosphere.ApplyView();
        atmosphere.RainParticles.Simulate(0.7f, true, false); yield return Capture(camera, "rain", hud);
        File.WriteAllText("main-scene-review-success.txt", report); EditorApplication.Exit(0);
    }
    private static IEnumerator Capture(Camera camera, string name, Canvas hud)
    {
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        var prior = RenderTexture.active;
        var mode = hud.renderMode; var uiCamera = hud.worldCamera; float plane = hud.planeDistance; int mask = camera.cullingMask;
        camera.cullingMask |= 1 << 5;
        hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = camera.nearClipPlane + 0.001f;
        // Allow Canvas geometry and its screen-size layout to rebuild after changing output and render mode.
        yield return null; yield return null;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes("main-scene-images/" + name + ".png", image.EncodeToPNG());
        hud.renderMode = mode; hud.worldCamera = uiCamera; hud.planeDistance = plane;
        camera.cullingMask = mask; camera.targetTexture = null; RenderTexture.active = prior;
        Destroy(image); target.Release(); Destroy(target);
    }
    private static string Hierarchy(Transform transform) => transform.parent == null ? transform.name : Hierarchy(transform.parent) + "/" + transform.name;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("main-scene-review-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
