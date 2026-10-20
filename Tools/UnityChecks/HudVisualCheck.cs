#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Copy into the isolated project's Assets folder and run HudVisualCheck.Begin with
// graphics enabled. This fixture creates its own scene; it never saves MainScene.
public sealed class HudVisualCheck : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    public PlayerVitals vitals;
    public Camera view;
    private Canvas hud;
    private string report = "";
    private static readonly string[] panelNames = { "Vitals", "Materials", "Objective", "Toolbelt", "Mode", "Context", "Feedback", "Recovery" };

    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var check = new GameObject("HUD visual validation").AddComponent<HudVisualCheck>();
        var player = new GameObject("Player");
        check.vitals = player.AddComponent<PlayerVitals>(); player.AddComponent<PlayerController>().enabled = false;
        var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform); cameraObject.transform.localPosition = new Vector3(0, 1.5f, 0);
        check.view = cameraObject.AddComponent<Camera>(); check.view.nearClipPlane = 0.1f;
        check.view.clearFlags = CameraClearFlags.SolidColor; check.view.backgroundColor = new Color(0.38f, 0.57f, 0.67f);
        var mount = new GameObject("Holder"); mount.transform.SetParent(cameraObject.transform, false);
        mount.transform.localPosition = new Vector3(0.18f, -0.22f, 0.45f);
        var holder = mount.AddComponent<FirstPersonWeaponHolder>();
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.name = "Validation weapon";
        model.transform.SetParent(mount.transform, false); model.transform.localScale = new Vector3(0.08f, 0.1f, 0.3f);
        DestroyImmediate(model.GetComponent<Collider>());
        var serialized = new SerializedObject(holder);
        serialized.FindProperty("equippedWeapon").objectReferenceValue = model.transform;
        serialized.FindProperty("viewTransform").objectReferenceValue = cameraObject.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        check.weapons = mount.AddComponent<PlayerWeaponSystem>(); check.weapons.holder = holder;
        check.weapons.maximumEnergy = 160; check.weapons.maximumWater = 120;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        check.weapons.effectMaterial = new Material(shader);
        for (int i = 1; i < 4; i++) check.weapons.modelPrefabs[i] = model;
        mount.AddComponent<EcoQuestHUD>().weapons = check.weapons;
        var clock = new GameObject("Clock").AddComponent<EcoWorldClock>();
        clock.advanceTime = clock.varyingWeather = clock.driveLighting = false; clock.hour = 14.4f; clock.day = 3; clock.cloudCover = 0.6f; clock.windStrength = 0.6f;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Validation ground";
        floor.transform.position = new Vector3(0, -0.15f, 35); floor.transform.localScale = new Vector3(120, 0.3f, 120);
        floor.GetComponent<Renderer>().sharedMaterial = new Material(shader);
        floor.GetComponent<Renderer>().sharedMaterial.color = new Color(0.25f, 0.36f, 0.25f);
        EditorSceneManager.SaveScene(player.scene, "Assets/HudVisualValidation.unity");
        EditorApplication.isPlaying = true;
    }

    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        weapons.enabled = false; weapons.Builder.enabled = false;
        vitals.ReceiveHit(28f); weapons.ReturnMaterials(128, 64, 3); weapons.ReceiveFish(true);
        weapons.ShowFeedback("Ekosistemi iyileştir · Tohum ek, sula ve yetiştir.");
        hud = GameObject.Find("EcoQuest HUD").GetComponent<Canvas>();
        Check(hud.transform.parent == null, "Overlay is independent of the player and weapon transform");
        Check(hud.transform.Find("Toolbelt").childCount == 5, "Five tool slots retain their numeric selection affordance");
        Check(!vitals.showLegacyHUD, "Legacy energy overlay is disabled while the HUD owns the display");
        Check(Label("Vitals/Meter name 0").text == "Karakter enerjisi" && Label("Vitals/Meter name 1").text == "Silah elektriği" && Label("Vitals/Meter name 2").text == "Temiz su", "Character energy, weapon electricity and water have separate names");
        Check(Label("Vitals/Meter value 0").text.Contains("100") && Label("Vitals/Meter value 1").text.Contains("160") && Label("Vitals/Meter value 2").text.Contains("120"), "Each meter uses the correct independent capacity");
        Check(Label("Vitals/Weather").text.Contains("Yağmurlu") && Label("Vitals/Weather").text.Contains("Gün 3"), "World clock and current weather are visible");
        var font = Label("Materials/Inventory").font;
        const string turkish = "ÇĞİÖŞÜçğıöşü";
        font.RequestCharactersInTexture(turkish, 32, FontStyle.Normal);
        foreach (char glyph in turkish) Check(font.HasCharacter(glyph), "Font contains Turkish glyph " + glyph);

        Vector2Int[] sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720), new Vector2Int(800, 600) };
        foreach (var size in sizes)
        {
            var target = new RenderTexture(size.x, size.y, 24); target.Create(); view.targetTexture = target;
            hud.renderMode = RenderMode.ScreenSpaceCamera; hud.worldCamera = view; hud.planeDistance = 0.4f;
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            CheckLayout(size, "normal");
            Check(Label("Toolbelt/GERİ DÖNÜŞÜM/Title").text == "GERİ DÖNÜŞÜM", size + ": Full recycler title is retained");
            Check(Label("Toolbelt/TOHUM/Count").text == weapons.SeedCount(weapons.SelectedSeed) + " tohum", size + ": Seed slot shows the selected seed inventory");
            Capture(target, "hud-preview-" + size.x + "x" + size.y + ".png");
            if (size.x == 800)
            {
                weapons.SelectTool(1); weapons.CycleMode(); yield return new WaitForSecondsRealtime(0.12f);
                Check(Label("Toolbelt/TOHUM/Count").text == weapons.MetalAmmo + " demir top", "Metal mode shows its own ammunition count");
                CheckLayout(size, "metal mode");
                weapons.CycleMode(); yield return new WaitForSecondsRealtime(0.12f);
                Check(Label("Toolbelt/TOHUM/Count").text == weapons.SeedCount(weapons.SelectedSeed) + " tohum", "Returning to seed mode restores the selected seed count");
                weapons.SelectTool(4); yield return null;
                Check(hud.transform.Find("Toolbelt/ELLER/Selection").GetComponent<Image>().enabled, "Selected hands slot has a dedicated mint marker");
                Check(hud.transform.Find("Toolbelt/ELLER").GetComponent<Image>().color.g > 0.4f, "Selected hands slot retains clear background contrast");
                weapons.Builder.SetBuildMode(true); yield return new WaitForSeconds(0.12f);
                Check(Label("Mode/Active tool").text.StartsWith("İNŞA"), "Build mode displays the selected building and page");
                for (int i = 0; i < 5; i++)
                    Check(hud.transform.Find("Toolbelt").GetChild(i).Find("Title").GetComponent<Text>().text == weapons.Builder.SlotName(i), "Build slot " + i + " uses the actual catalog name");
                CheckLayout(size, "building"); Capture(target, "hud-preview-800x600-building.png");
                weapons.Builder.SetBuildMode(false); vitals.ReceiveHit(1000);
                float noticeDeadline = Time.unscaledTime + 0.5f;
                while (!Label("Recovery/Recovery status").text.Contains("ENERJİ TOPLANIYOR") && Time.unscaledTime < noticeDeadline) yield return null;
                Check(Label("Recovery/Recovery status").text.Contains("ENERJİ TOPLANIYOR"), "Recovery is described as energy restoration rather than death");
                CheckLayout(size, "recovery"); Capture(target, "hud-preview-800x600-recovery.png");
            }
            view.targetTexture = null; RenderTexture.active = null; target.Release(); Destroy(target);
        }
        File.WriteAllText("hud-visual-success.txt", report);
        EditorApplication.Exit(0);
    }

    private void CheckLayout(Vector2Int size, string state)
    {
        Canvas.ForceUpdateCanvases(); view.Render();
        string tag = size.x + "x" + size.y + " " + state + ": ";
        foreach (string name in panelNames)
        {
            Rect bounds = ScreenBounds((RectTransform)hud.transform.Find(name));
            Check(bounds.xMin >= -1 && bounds.yMin >= -1 && bounds.xMax <= size.x + 1 && bounds.yMax <= size.y + 1, tag + name + " stays inside the output");
        }
        CheckDisjoint("Vitals", "Objective", tag); CheckDisjoint("Materials", "Objective", tag); CheckDisjoint("Vitals", "Materials", tag);
        CheckDisjoint("Objective", "Feedback", tag); CheckDisjoint("Toolbelt", "Mode", tag); CheckDisjoint("Context", "Mode", tag);
        for (int i = 0; i < 5; i++)
        {
            var slot = hud.transform.Find("Toolbelt").GetChild(i);
            Rect title = ScreenBounds((RectTransform)slot.Find("Title"));
            Rect count = ScreenBounds((RectTransform)slot.Find("Count"));
            Check(!title.Overlaps(count), tag + slot.name + " title and count do not overlap");
        }
        foreach (var label in hud.GetComponentsInChildren<Text>())
        {
            if (string.IsNullOrEmpty(label.text)) continue;
            Check(label.preferredHeight <= label.rectTransform.rect.height + 1f, tag + label.transform.parent.name + "/" + label.name + " does not clip vertically");
        }
        string[] critical = { "Vitals/Meter name 0", "Vitals/Meter name 1", "Vitals/Meter name 2", "Materials/Inventory", "Objective/Current objective", "Mode/Controls", "Toolbelt/TOHUM/Count", "Toolbelt/GERİ DÖNÜŞÜM/Title" };
        foreach (string path in critical)
        {
            var label = Label(path);
            float visibleFontSize = label.fontSize * label.rectTransform.localScale.y * hud.scaleFactor;
            Check(visibleFontSize >= 11.9f, tag + path + " remains at least 12 display pixels");
        }
        Rect aim = ScreenBounds((RectTransform)hud.transform.Find("Aim"));
        Check(!ScreenBounds((RectTransform)hud.transform.Find("Context")).Overlaps(aim), tag + "Interaction card leaves the reticle clear");
    }

    private void CheckDisjoint(string first, string second, string tag)
    {
        Check(!ScreenBounds((RectTransform)hud.transform.Find(first)).Overlaps(ScreenBounds((RectTransform)hud.transform.Find(second))), tag + first + " and " + second + " do not overlap");
    }
    private Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(view, corners[0]), max = min;
        foreach (var corner in corners) { Vector2 point = RectTransformUtility.WorldToScreenPoint(view, corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private Text Label(string path) => hud.transform.Find(path).GetComponent<Text>();
    private void Capture(RenderTexture target, string path)
    {
        Canvas.ForceUpdateCanvases(); view.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); Destroy(image); RenderTexture.active = previous;
    }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("HUD check failed: " + message);
        report += "PASS " + message + "\n";
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        File.WriteAllText("hud-visual-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
