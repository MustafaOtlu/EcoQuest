#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MarketVisualCheck : MonoBehaviour
{
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Market visual check").AddComponent<MarketVisualCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/MarketVisualValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            File.WriteAllText("market-visual-failed.txt", message + "\n" + stack); EditorApplication.Exit(1);
        };
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var player = FindFirstObjectByType<PlayerController>(); player.enabled = false;
        var shop = FindFirstObjectByType<EcoMarket>(); var weapon = FindFirstObjectByType<PlayerWeaponSystem>();
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = shop.transform.position + Vector3.back * 2; cc.enabled = true;
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        camera.transform.position = player.transform.position + Vector3.up * 1.4f; camera.transform.LookAt(shop.GetComponent<Collider>().bounds.center); Physics.SyncTransforms();
        weapon.ReturnMaterials(32, 18); weapon.Progression.Restore(EcoProgression.Threshold(2));
        weapon.Interact(); yield return null;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        var hud = GameObject.Find("EcoQuest HUD").GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = camera; hud.planeDistance = 0.6f;
        var marketCanvas = GameObject.Find("Eco Market UI").GetComponent<Canvas>(); marketCanvas.renderMode = RenderMode.ScreenSpaceCamera; marketCanvas.worldCamera = camera; marketCanvas.planeDistance = 0.5f;
        yield return new WaitForSeconds(0.15f); Capture(camera, target, "market-shopping-preview.png");
        FindFirstObjectByType<EcoMarketUI>().ShowPage(true);
        yield return new WaitForSeconds(0.15f); Capture(camera, target, "market-upgrades-preview.png");
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target);
        File.WriteAllText("market-visual-success.txt", "MainScene market shopping and nine-track upgrade UI rendered at 1600x900."); EditorApplication.Exit(0);
    }
    private static void Capture(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
    }
}
#endif
