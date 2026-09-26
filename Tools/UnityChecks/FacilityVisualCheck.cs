#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FacilityVisualCheck : MonoBehaviour
{
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Facility visual check").AddComponent<FacilityVisualCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/FacilityVisualValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            File.WriteAllText("facility-visual-failed.txt", message + "\n" + stack); EditorApplication.Exit(1);
        };
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var player = FindFirstObjectByType<PlayerController>(); player.enabled = false;
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = new Vector3(22, 0.03f, 0); cc.enabled = true;
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var clock = FindFirstObjectByType<EcoWorldClock>(); clock.advanceTime = clock.varyingWeather = false; clock.hour = 11; clock.cloudCover = 0.15f;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position = new Vector3(22, -0.25f, 5); ground.transform.localScale = new Vector3(17, 0.5f, 14); ground.AddComponent<PlantingSurface>();
        var source = GameObject.CreatePrimitive(PrimitiveType.Cylinder); source.transform.position = new Vector3(16.8f, 0.6f, 2.7f); source.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f); source.AddComponent<WeaponWaterSource>().cleanWater = false;
        source.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("EcoInfrastructure/DirtyWaterTank");
        Physics.SyncTransforms();
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); weapon.enabled = false; weapon.ReturnMaterials(200, 200); weapon.Progression.Restore(EcoProgression.Threshold(4));
        var builder = weapon.Builder; builder.SetBuildMode(true);
        string[] ids = { "intake", "purifier", "tank", "recycling", "farm" };
        Vector3[] positions = { new Vector3(17, 0.03f, 4), new Vector3(20, 0.03f, 4), new Vector3(23, 0.03f, 4), new Vector3(26, 0.03f, 4), new Vector3(20, 0.03f, 7) };
        for (int i = 0; i < ids.Length; i++)
            if (!builder.TryPlace(EcoBuildingCatalog.Find(ids[i]), positions[i], Quaternion.identity)) throw new Exception("Visual fixture could not construct " + ids[i]);
        var nodes = EcoWaterNode.Active.ToArray();
        var intake = nodes.First(n => n.kind == EcoWaterNode.Kind.Intake); var purifier = nodes.First(n => n.kind == EcoWaterNode.Kind.Purifier);
        var tank = nodes.First(n => n.kind == EcoWaterNode.Kind.Tank); var farm = nodes.First(n => n.kind == EcoWaterNode.Kind.Farm);
        if (!builder.TryConnectPipe(intake, purifier, EcoWaterNode.Fluid.Dirty) || !builder.TryConnectPipe(purifier, tank, EcoWaterNode.Fluid.Clean) || !builder.TryConnectPipe(tank, farm, EcoWaterNode.Fluid.Clean)) throw new Exception("Visual water pipes failed");
        var crop = PlantedSeed.Create(farm.transform.position + new Vector3(-0.7f, 0.15f, 0)); crop.growth = 0.6f; crop.moisture = 0.5f;
        var second = PlantedSeed.Create(farm.transform.position + new Vector3(0.6f, 0.15f, 0.5f)); second.growth = 1; second.moisture = 0.5f;
        tank.cleanWater = 100; farm.cleanWater = 40;
        builder.SetBuildMode(false); weapon.holder.Unequip();
        camera.transform.position = new Vector3(22, 6, -6); camera.transform.LookAt(new Vector3(21.5f, 0.75f, 4.8f)); camera.fieldOfView = 58;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        var hud = GameObject.Find("EcoQuest HUD").GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = 0.5f;
        yield return new WaitForSeconds(0.2f);
        Capture(camera, target, "facility-overview-preview.png");
        builder.SetBuildMode(true); builder.Select(EcoBuildingCatalog.Find("purifier"));
        var preview = GameObject.Find("Build preview"); if (preview != null) preview.SetActive(false);
        yield return new WaitForSeconds(0.2f);
        Capture(camera, target, "facility-build-hud-preview.png");
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target);
        File.WriteAllText("facility-visual-success.txt", "Constructed facility models, directed water pipes, crop beds and paged HUD rendered at 1600x900."); EditorApplication.Exit(0);
    }
    private static void Capture(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
    }
}
#endif
