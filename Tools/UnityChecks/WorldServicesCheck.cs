#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Copy into an isolated validation project's Assets folder before running Begin.
public sealed class WorldServicesCheck : MonoBehaviour
{
    private string report = "";
    private bool finished;

    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("World services check").AddComponent<WorldServicesCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/WorldServicesValidation.unity");
        EditorApplication.isPlaying = true;
    }

    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var anchor = new Vector3(328, 100, 190);
        var playerObject = new GameObject("Active relocated player");
        playerObject.transform.position = anchor;
        playerObject.AddComponent<PlayerController>().enabled = false;
        playerObject.GetComponent<CharacterController>().enabled = false;

        var sun = new GameObject("Shared sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 2;
        var legacyObject = Services("Origin services", Vector3.zero, sun, 0);
        var primaryObject = Services("Relocated services", anchor, sun, 12);
        var legacy = legacyObject.GetComponent<EcoWorldClock>();
        var primary = primaryObject.GetComponent<EcoWorldClock>();
        var legacyPower = legacyObject.GetComponent<EcoPowerGrid>();
        var primaryPower = primaryObject.GetComponent<EcoPowerGrid>();
        var legacyWater = legacyObject.GetComponent<EcoWaterGrid>();
        var primaryWater = primaryObject.GetComponent<EcoWaterGrid>();
        legacyObject.SetActive(true); primaryObject.SetActive(true);
        Check(EcoWorldClock.Current == primary, "Nearest active player selects the relocated clock despite origin services being created first");

        var solar = Node<EcoPowerNode>("Solar", anchor + Vector3.right * 3);
        solar.kind = EcoPowerNode.Kind.Solar; solar.ratedPower = 12; solar.checkShade = false;
        var battery = Node<EcoPowerNode>("Battery", anchor + Vector3.right * 4);
        battery.kind = EcoPowerNode.Kind.Battery; battery.capacity = 1000;
        var cable = new GameObject("Connected cable").AddComponent<EcoPowerCable>();
        cable.a = solar; cable.b = battery;
        var source = Node<EcoWaterNode>("Finite source tank", anchor + Vector3.right * 5);
        var destination = Node<EcoWaterNode>("Destination tank", anchor + Vector3.right * 6);
        source.kind = destination.kind = EcoWaterNode.Kind.Tank;
        source.capacity = destination.capacity = 100;
        var pipe = new GameObject("Rate limited clean pipe").AddComponent<EcoWaterPipe>();
        pipe.from = source; pipe.to = destination; pipe.fluid = EcoWaterNode.Fluid.Clean; pipe.litresPerSecond = 5;
        // Let all Start methods initialize sunlight before measuring real Update ticks.
        yield return null; yield return null;
        var atmosphere = EcoAtmosphere.Current;
        Check(Near(sun.intensity, 2, 0.01f) && atmosphere != null && atmosphere.clock == primary,
            "Noon primary owns both sunlight and atmosphere despite the midnight duplicate");

        primary.hour = 12; primary.advanceTime = true; legacy.hour = 0; legacy.advanceTime = true;
        ResetResources(battery, source, destination);
        float started = Time.time;
        yield return new WaitForSeconds(0.4f);
        float elapsed = Time.time - started;
        Pause(legacy, primary, legacyPower, primaryPower, legacyWater, primaryWater);
        Check(elapsed >= 0.35f, "Rate assertions measure elapsed Unity game time across actual Update frames");
        Check(Near(primary.hour, 12 + elapsed * 24 / 600, 0.003f), "Primary clock advances exactly once for elapsed game time");
        Check(Near(legacy.hour, 0, 0.0001f), "Duplicate clock does not advance while the primary exists");
        Check(Near(battery.storedEnergy, 12 * elapsed, 0.025f), "Real power Updates store one solar generation budget, not two: " + battery.storedEnergy + " / expected " + 12 * elapsed);
        Check(Near(destination.cleanWater, 5 * elapsed, 0.02f), "Real water Updates move one pipe rate, not two: " + destination.cleanWater + " / expected " + 5 * elapsed);
        Check(Near(source.cleanWater + destination.cleanWater, 60, 0.01f) && Near(source.dirtyWater, 30, 0.001f) && Near(destination.dirtyWater, 0, 0.001f), "Pipe transfer conserves finite clean water and leaves dirty water untouched");
        Check(primaryPower.clock == primary && Near(battery.NetworkGeneration, solar.Generation(primary), 0.01f), "Active power manager uses the selected primary clock for actual generation");

        // Keep both managers enabled at night: a noon duplicate must not generate free energy.
        primary.hour = 0; legacy.hour = 12;
        float storedBeforeNight = battery.storedEnergy;
        ResumeGrids(legacyPower, primaryPower, legacyWater, primaryWater);
        yield return new WaitForSeconds(0.25f);
        Pause(legacy, primary, legacyPower, primaryPower, legacyWater, primaryWater);
        Check(Near(battery.storedEnergy, storedBeforeNight, 0.001f), "Night primary prevents solar charging even when duplicate clock says noon");
        Check(sun.intensity < 0.001f, "Duplicate noon clock cannot relight the primary clock's night");

        // Disable only the primary clock, leaving both copies of the network managers present.
        primary.enabled = false;
        Check(EcoWorldClock.Current == legacy, "Disabling primary clock immediately selects a remaining active clock");
        legacy.hour = 12; legacy.advanceTime = true;
        ResetResources(battery, source, destination);
        ResumeGrids(legacyPower, primaryPower, legacyWater, primaryWater);
        started = Time.time;
        yield return new WaitForSeconds(0.4f);
        elapsed = Time.time - started;
        Pause(legacy, primary, legacyPower, primaryPower, legacyWater, primaryWater);
        Check(Near(legacy.hour, 12 + elapsed * 24 / 600, 0.003f) && legacyPower.clock == legacy
            && atmosphere.clock == legacy && Near(sun.intensity, 2, 0.01f)
            && atmosphere.SkyMaterial != null && atmosphere.SkyMaterial.GetFloat("_Daylight") > 0.99f,
            "Fallback clock advances and power, sunlight, and sky all follow its noon time");
        Check(Near(battery.storedEnergy, 12 * elapsed, 0.025f) && Near(destination.cleanWater, 5 * elapsed, 0.02f), "Fallback still runs power and water only once despite two manager copies");
        primary.enabled = true;
        yield return null; yield return null;
        Check(EcoWorldClock.Current == primary && atmosphere.clock == primary && sun.intensity < 0.001f
            && atmosphere.SkyMaterial.GetFloat("_Daylight") < 0.001f,
            "Re-enabling the closer midnight clock restores primary selection, sunlight, and sky together");
        atmosphere.automatic = false; atmosphere.clock = legacy; atmosphere.ApplyView();
        bool manualClockRetained = atmosphere.clock == legacy && atmosphere.SkyMaterial.GetFloat("_Daylight") > 0.99f;
        primary.enabled = false; legacy.enabled = false;
        Check(EcoWorldClock.Current == null && manualClockRetained,
            "Manual atmosphere retains its explicitly assigned clock and disabling all clocks clears primary selection");

        // Public simulation APIs remain useful to deterministic fixtures with managers disabled.
        primary.hour = 23.9f; primary.day = 7; primary.Simulate(5);
        Check(primary.day == 8 && Near(primary.hour, 0.1f, 0.001f), "Explicit clock simulation remains available while disabled and rolls over the day");
        primary.hour = 12; primaryPower.clock = primary;
        ResetResources(battery, source, destination);
        primaryPower.Simulate(2);
        Check(Near(battery.storedEnergy, 24, 0.001f), "Explicit disabled power manager simulation still applies exactly its requested interval");
        primaryWater.Simulate(2);
        Check(Near(destination.cleanWater, 10, 0.001f) && Near(source.cleanWater, 50, 0.001f), "Explicit disabled water manager simulation preserves finite transfer accounting");
        cable.connected = false; pipe.connected = false;
        primaryPower.Simulate(2); primaryWater.Simulate(2);
        Check(Near(battery.storedEnergy, 24, 0.001f) && Near(destination.cleanWater, 10, 0.001f), "Disconnected networks cannot create battery energy or transfer additional water");

        finished = true;
        File.WriteAllText("world-services-check-success.txt", report);
        Application.logMessageReceived -= OnLog;
        EditorApplication.Exit(0);
    }

    private static GameObject Services(string name, Vector3 position, Light sun, float hour)
    {
        var instance = new GameObject(name); instance.SetActive(false); instance.transform.position = position;
        var clock = instance.AddComponent<EcoWorldClock>();
        clock.hour = hour; clock.dayLengthSeconds = 600; clock.cloudCover = 0; clock.sun = sun;
        clock.advanceTime = clock.varyingWeather = false; clock.driveLighting = true;
        instance.AddComponent<EcoPowerGrid>(); instance.AddComponent<EcoWaterGrid>();
        return instance;
    }
    private static T Node<T>(string name, Vector3 position) where T : Component
    {
        var instance = new GameObject(name); instance.transform.position = position; return instance.AddComponent<T>();
    }
    private static void ResetResources(EcoPowerNode battery, EcoWaterNode source, EcoWaterNode destination)
    {
        battery.storedEnergy = 0; source.cleanWater = 60; source.dirtyWater = 30;
        destination.cleanWater = destination.dirtyWater = 0;
    }
    private static void Pause(EcoWorldClock a, EcoWorldClock b, params Behaviour[] grids)
    {
        a.advanceTime = b.advanceTime = false; foreach (var grid in grids) grid.enabled = false;
    }
    private static void ResumeGrids(params Behaviour[] grids) { foreach (var grid in grids) grid.enabled = true; }
    private static bool Near(float actual, float expected, float tolerance) => Mathf.Abs(actual - expected) <= tolerance;
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label); report += "PASS " + label + "\n";
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (finished || (type != LogType.Error && type != LogType.Exception)) return;
        finished = true;
        File.WriteAllText("world-services-check-failed.txt", report + message + "\n" + stack);
        EditorApplication.Exit(1);
    }
}
#endif
