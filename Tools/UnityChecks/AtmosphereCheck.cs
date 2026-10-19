#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class AtmosphereCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Atmosphere check").AddComponent<AtmosphereCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/AtmosphereValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var cameraObject = new GameObject("Main camera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 2, -7); camera.farClipPlane = 1200;
        var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, -30, 0);
        var clock = new GameObject("Clock").AddComponent<EcoWorldClock>(); clock.advanceTime = clock.varyingWeather = clock.driveLighting = false; clock.sun = light;
        clock.hour = 12; clock.cloudCover = 0.15f;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Ground"; ground.transform.localScale = Vector3.one * 100;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = new Color(0.26f, 0.42f, 0.23f); ground.GetComponent<Renderer>().sharedMaterial = material;
        for (int i = 0; i < 5; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Distance landmark";
            cube.transform.position = new Vector3((i - 2) * 40, 10, 100 + i * 110); cube.transform.localScale = new Vector3(12, 20, 12);
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
        var originalSky = RenderSettings.skybox; bool originalFog = RenderSettings.fog; float originalFogEnd = RenderSettings.fogEndDistance;
        var atmosphere = new GameObject("Atmosphere").AddComponent<EcoAtmosphere>(); atmosphere.clock = clock; atmosphere.viewCamera = camera; atmosphere.automatic = false;
        atmosphere.ApplyView();
        Check(atmosphere.SkyMaterial != null && atmosphere.SkyMaterial.shader.isSupported, "Custom cloud sky exists and supports the rendering pipeline");
        Check(RenderSettings.skybox == atmosphere.SkyMaterial && atmosphere.SkyMaterial.shader.name == "EcoQuest/Cloud sky", "Gameplay uses the EcoQuest sky instead of Unity's default sky");
        Check(RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear && Near(RenderSettings.fogStartDistance, 220) && Near(RenderSettings.fogEndDistance, 480), "Clear-weather fog fades the horizon before the render cutoff");
        Check(Near(camera.farClipPlane, 500), "World camera really clips geometry beyond 500 units");
        Check(clock.WeatherName == "Açık" && Near(clock.RainIntensity, 0), "Low cloud cover means clear weather without rain");
        Directory.CreateDirectory("atmosphere-images");
        camera.transform.rotation = Quaternion.Euler(-12, 0, 0); Capture(camera, "clear");
        clock.cloudCover = 0.75f; clock.windStrength = 0.9f; atmosphere.ApplyView();
        Check(clock.WeatherName == "Fırtına" && Near(atmosphere.Rain, 1), "Dense clouds and strong wind produce a storm");
        Check(RenderSettings.fogEndDistance < 480 && RenderSettings.fogStartDistance < 220, "Rain reduces visibility within the same 500-unit render limit");
        var emission = atmosphere.RainParticles.emission;
        Check(emission.rateOverTime.constant > 800 && atmosphere.RainParticles.main.maxParticles <= 1800, "Local rain emits a bounded pool of droplets");
        var highRoof = GameObject.CreatePrimitive(PrimitiveType.Cube); highRoof.transform.position = camera.transform.position + Vector3.up * 14;
        highRoof.transform.localScale = new Vector3(10, 0.3f, 10); Physics.SyncTransforms(); atmosphere.ApplyView();
        Check(Near(atmosphere.RainParticles.emission.rateOverTime.constant, 0) && atmosphere.RainParticles.particleCount == 0, "Tall roofs also suppress visible rain below the local emitter");
        highRoof.SetActive(false); atmosphere.ApplyView();
        var solar = new GameObject("Panel").AddComponent<EcoPowerNode>(); solar.kind = EcoPowerNode.Kind.Solar; solar.checkShade = false;
        float wetOutput = solar.Generation(clock); clock.cloudCover = 0; float dryOutput = solar.Generation(clock);
        Check(wetOutput < dryOutput, "Clouds reduce actual solar energy production");
        clock.cloudCover = 0.75f;
        var crop = PlantedSeed.Create(new Vector3(-2, 0.02f, 2)); crop.moisture = 0.1f;
        var covered = PlantedSeed.Create(new Vector3(2, 0.02f, 2)); covered.moisture = 0.1f;
        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube); roof.name = "Roof"; roof.transform.position = new Vector3(2, 2, 2); roof.transform.localScale = new Vector3(2, 0.2f, 2);
        var fire = new GameObject("Outdoor fire").AddComponent<EcoFire>(); fire.transform.position = new Vector3(-4, 0.1f, 2); fire.intensity = 1;
        var regionObject = new GameObject("Rain test region"); var region = regionObject.AddComponent<EcoRegion>(); region.size = new Vector2(100, 100); region.enabled = false;
        Physics.SyncTransforms();
        atmosphere.Simulate(1);
        Check(crop.moisture > 0.12f && Near(covered.moisture, 0.1f), "Outdoor plants receive rain while a roof protects covered plants");
        atmosphere.Simulate(1);
        Check(fire.intensity < 1, "Outdoor rain reduces actual fire intensity");
        region.enabled = true; region.airPollution = 80;
        float health = crop.health; atmosphere.Simulate(1);
        Check(crop.contaminated && crop.health < health, "Rain in heavily polluted air harms and contaminates outdoor crops");
        atmosphere.Simulate(float.NaN); atmosphere.Simulate(-1);
        Check(atmosphere.RainParticles.main.maxParticles <= 1800, "Invalid simulation steps do not create extra particle systems");
        atmosphere.RainParticles.Simulate(0.8f, true, false); Capture(camera, "rain");
        clock.hour = 0; atmosphere.ApplyView(); Capture(camera, "night");
        Check(atmosphere.SkyMaterial.GetFloat("_Daylight") < 0.01f && clock.SolarFactor == 0, "Night darkens the custom sky and stops solar generation");
        clock.driveLighting = true; yield return null;
        Check(light.intensity < 0.001f && RenderSettings.ambientIntensity <= 0.21f, "The actual clock update also dims sunlight and ambient lighting at night");
        atmosphere.enabled = false;
        Check(EcoAtmosphere.Current == null && Near(camera.farClipPlane, 1200), "Disabling the atmosphere restores camera distance and releases global ownership");
        Check(RenderSettings.skybox == originalSky && RenderSettings.fog == originalFog && Near(RenderSettings.fogEndDistance, originalFogEnd), "Scene sky and fog settings are restored on disable");
        File.WriteAllText("atmosphere-check-success.txt", report); yield return null; EditorApplication.Exit(0);
    }
    private static void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes("atmosphere-images/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous; Destroy(image); target.Release(); Destroy(target);
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.005f;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("atmosphere-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
