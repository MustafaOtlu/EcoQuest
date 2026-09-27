#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class EnvironmentVisualCheck : MonoBehaviour
{
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Environment visual check").AddComponent<EnvironmentVisualCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/EnvironmentVisualValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            File.WriteAllText("environment-visual-failed.txt", message + "\n" + stack); EditorApplication.Exit(1);
        };
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var player = FindFirstObjectByType<PlayerController>(); player.enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); weapon.SelectTool(4);
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        GameObject.Find("EcoQuest HUD").SetActive(false);
        camera.transform.position = new Vector3(15, 5.3f, -9); camera.transform.LookAt(new Vector3(26, 3, 7));
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        yield return new WaitForSeconds(2); Capture(camera, target, "environment-factory-before.png");
        var shop = FindFirstObjectByType<EcoMarket>(); weapon.ReturnMaterials(100, 100); weapon.Progression.Restore(EcoProgression.Threshold(4));
        Move(player, shop.transform.position + Vector3.back); shop.Trade(weapon, EcoMarket.Offer.LargeFilter);
        var factory = FindFirstObjectByType<EcoFactory>(); Move(player, factory.GetComponent<Collider>().bounds.center + Vector3.back * 3.8f); factory.InstallFilter(weapon);
        yield return new WaitForSeconds(2); Capture(camera, target, "environment-factory-filtered.png");
        camera.transform.position = new Vector3(17, 2.1f, -7); camera.transform.LookAt(new Vector3(22, 1, -3));
        yield return new WaitForSeconds(0.5f); Capture(camera, target, "environment-fire.png");
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target);
        File.WriteAllText("environment-visual-success.txt", "Factory before/after filter and fire rendered at 1600x900."); EditorApplication.Exit(0);
    }
    private static void Move(PlayerController player, Vector3 point)
    { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = point; cc.enabled = true; Physics.SyncTransforms(); }
    private static void Capture(Camera camera, RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image);
    }
}
#endif
