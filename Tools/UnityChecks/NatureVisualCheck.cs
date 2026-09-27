#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class NatureVisualCheck : MonoBehaviour
{
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Nature visual check").AddComponent<NatureVisualCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/NatureVisualValidation.unity"); EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += (message, stack, type) =>
        { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("nature-visual-failed.txt", message + "\n" + stack); EditorApplication.Exit(1); };
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var player = FindFirstObjectByType<PlayerController>(); player.enabled = false;
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>(); weapons.SelectTool(4);
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        var hud = GameObject.Find("EcoQuest HUD").GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = 0.6f;
        var cabbage = PlantedSeed.Create(new Vector3(6.1f, 0.11f, -1.7f));
        var carrot = PlantedSeed.Create(new Vector3(8.4f, 0.11f, -1.7f), EcoCropCatalog.Kind.Carrot);
        var tomato = PlantedSeed.Create(new Vector3(6.1f, 0.11f, -4), EcoCropCatalog.Kind.Tomato);
        foreach (var crop in new[] { cabbage, carrot, tomato }) { crop.growth = 0.95f; crop.moisture = 0.8f; crop.pestProtectionRemaining = 120; }
        camera.transform.position = new Vector3(6, 2.4f, -7.3f); camera.transform.LookAt(new Vector3(6.9f, 0.2f, -2.6f));
        yield return new WaitForSeconds(0.2f); Capture(camera, target, "nature-crops-preview.png");
        foreach (var habitat in FindObjectsByType<EcoHabitat>(FindObjectsSortMode.None)) if (habitat.GetComponent<EcoRegion>().airPollution < 40) habitat.recovery = 1;
        camera.transform.position = new Vector3(-1, 3, 3); camera.transform.LookAt(new Vector3(-7, 1.2f, 9));
        yield return new WaitForSeconds(0.2f); Capture(camera, target, "nature-wildlife-preview.png");
        var shop = FindFirstObjectByType<EcoMarket>(); var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = shop.transform.position + Vector3.back; cc.enabled = true;
        weapons.ReturnMaterials(50, 50); weapons.Progression.Restore(EcoProgression.Threshold(4)); shop.Interact(weapons);
        var menu = GameObject.Find("Eco Market UI").GetComponent<Canvas>(); menu.renderMode = RenderMode.ScreenSpaceCamera; menu.worldCamera = camera; menu.planeDistance = 0.5f;
        yield return new WaitForSeconds(0.2f); Capture(camera, target, "nature-seed-market-preview.png");
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target);
        File.WriteAllText("nature-visual-success.txt", "Crop, wildlife and seed market views rendered."); EditorApplication.Exit(0);
    }
    private static void Capture(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
    }
}
#endif
