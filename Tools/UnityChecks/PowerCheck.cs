#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PowerCheck : MonoBehaviour
{
    private string report = "";
    private readonly List<GameObject> fixtures = new List<GameObject>();
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Power check").AddComponent<PowerCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/PowerValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var grid = FindFirstObjectByType<EcoPowerGrid>();
        Check(grid != null, "MainScene has a connected starter power installation");
        var clock = grid.clock; grid.enabled = false; clock.enabled = false;
        clock.hour = 12; clock.cloudCover = 0; clock.windStrength = 1;
        var solar = Node(EcoPowerNode.Kind.Solar); solar.ratedPower = 10;
        var wind = Node(EcoPowerNode.Kind.Wind); wind.ratedPower = 8;
        Check(Near(solar.Generation(clock), 10), "Unshaded solar produces its rated power at clear noon");
        clock.hour = 0; Check(solar.Generation(clock) == 0, "Solar produces nothing at night");
        clock.hour = 12; clock.cloudCover = 1;
        Check(Near(solar.Generation(clock), 2), "Cloud cover reduces solar generation");
        clock.cloudCover = 0; clock.windStrength = 0;
        Check(wind.Generation(clock) == 0, "Turbine produces nothing without wind");
        clock.windStrength = 1; Check(Near(wind.Generation(clock), 8), "Strong wind drives turbine generation");
        solar.checkShade = true;
        var shadow = GameObject.CreatePrimitive(PrimitiveType.Cube); fixtures.Add(shadow);
        shadow.transform.position = solar.SocketPosition + Vector3.up * 0.1f + clock.SunDirection * 3;
        Physics.SyncTransforms();
        Check(solar.Generation(clock) == 0, "Physical overhead obstruction shades a panel");
        DestroyImmediate(shadow); solar.checkShade = false;
        var battery = Node(EcoPowerNode.Kind.Battery); battery.capacity = 50;
        var load = Node(EcoPowerNode.Kind.Consumer); load.requestedPower = 5;
        var connection = Link(solar, battery); var loadCable = Link(battery, load);
        load.enabled = false; grid.Simulate(2);
        Check(Near(battery.storedEnergy, 20), "Surplus generation charges only connected storage");
        load.enabled = true; clock.hour = 0; grid.Simulate(2);
        Check(Near(load.DeliveredPower, 5) && Near(battery.storedEnergy, 10), "Stored energy supplies a load at night with conserved amounts");
        loadCable.connected = false; grid.Simulate(2);
        Check(load.DeliveredPower == 0 && Near(battery.storedEnergy, 10), "Disconnected consumers cannot drain storage");
        loadCable.connected = true; clock.hour = 12; var loop = Link(load, solar); grid.Simulate(1);
        Check(Near(battery.storedEnergy, 15), "Cyclic cable network counts production and demand only once");
        load.enabled = false; grid.Simulate(20);
        Check(Near(battery.storedEnergy, 50), "Surplus never overfills a battery");
        load.enabled = true; load.requestedPower = 10; var other = Node(EcoPowerNode.Kind.Consumer); other.requestedPower = 10; Link(battery, other);
        clock.hour = 0; battery.storedEnergy = 5; grid.Simulate(1);
        Check(Near(load.DeliveredPower, 2.5f) && Near(other.DeliveredPower, 2.5f) && battery.storedEnergy == 0,
            "Limited electricity is shared without creating energy or negative storage");
        grid.Simulate(1); Check(load.DeliveredPower == 0 && other.DeliveredPower == 0, "Empty isolated network cannot provide free electricity");
        battery.storedEnergy = 20; battery.condition = 0; grid.Simulate(1);
        Check(load.DeliveredPower == 0 && battery.storedEnergy == 20, "Disabled battery retains its charge but cannot supply the circuit");
        foreach (var fixture in fixtures) if (fixture != null) DestroyImmediate(fixture);
        fixtures.Clear();

        var charger = FindFirstObjectByType<EcoChargingStation>();
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>();
        var vitals = weapon.GetComponentInParent<PlayerVitals>();
        Check(charger != null && charger.GetComponent<Collider>() != null, "Authored charging terminal has an interaction collider");
        vitals.GetComponent<PlayerController>().enabled = false; vitals.enabled = false;
        var cc = vitals.GetComponent<CharacterController>(); cc.enabled = false;
        vitals.transform.position = charger.transform.position + Vector3.forward * 1.5f; cc.enabled = true;
        var camera = Camera.main;
        foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        camera.transform.position = vitals.transform.position + Vector3.up * 1.5f;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.up);
        weapon.SelectTool(0); weapon.Use(6);
        Check(Near(weapon.Energy, 70), "Actual weapon use drains thirty units of electricity");
        float characterEnergy = vitals.Energy;
        float idleBattery = weapon.Energy;
        yield return new WaitForSeconds(0.2f);
        Check(Near(weapon.Energy, idleBattery), "Idle weapons do not recharge for free");
        var mainBattery = FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None).First(n => n.kind == EcoPowerNode.Kind.Battery);
        mainBattery.storedEnergy = 40; clock.hour = 0; clock.windStrength = 0;
        camera.transform.LookAt(charger.GetComponent<Collider>().bounds.center); Physics.SyncTransforms();
        weapon.Interact();
        Check(weapon.ChargingStation == charger, "Actual interaction ray connects the player to the terminal");
        grid.Simulate(1);
        Check(Near(weapon.Energy, 85) && Near(mainBattery.storedEnergy, 25), "Charging transfers energy from the wired battery to the weapon");
        Check(vitals.Energy == characterEnergy, "Charging leaves character energy unchanged");
        grid.Simulate(1); grid.Simulate(1);
        Check(Near(weapon.Energy, 100) && Near(mainBattery.storedEnergy, 10) && weapon.ChargingStation == null, "Full weapon battery stops charging without wasting electricity");
        weapon.Use(4); weapon.Interact();
        var terminalCable = FindObjectsByType<EcoPowerCable>(FindObjectsSortMode.None).First(c => c.a == charger.GetComponent<EcoPowerNode>() || c.b == charger.GetComponent<EcoPowerNode>());
        terminalCable.connected = false; float emptyLinkCharge = weapon.Energy; grid.Simulate(1);
        Check(weapon.Energy == emptyLinkCharge && Near(mainBattery.storedEnergy, 10), "Unplugging the actual terminal prevents charging");
        terminalCable.connected = true; grid.Simulate(1);
        Check(Near(weapon.Energy, emptyLinkCharge + 10) && mainBattery.storedEnergy == 0, "Reconnected terminal delivers only the remaining stored charge");
        vitals.ReceiveHit(10000); grid.Simulate(1);
        Check(weapon.ChargingStation == null, "Exhaustion disconnects charging safely");
        vitals.SimulateRecovery(5); weapon.Interact();
        cc.enabled = false; vitals.transform.position += Vector3.right * 5; cc.enabled = true; grid.Simulate(0.1f);
        Check(weapon.ChargingStation == null, "Walking away from the terminal disconnects charging");
        File.WriteAllText("power-check-success.txt", report); EditorApplication.Exit(0);
    }
    private EcoPowerNode Node(EcoPowerNode.Kind kind)
    {
        var go = new GameObject("Power test " + kind); go.transform.position = Vector3.one * 1000 + Vector3.right * fixtures.Count * 5;
        fixtures.Add(go); var node = go.AddComponent<EcoPowerNode>(); node.kind = kind; node.checkShade = false; return node;
    }
    private EcoPowerCable Link(EcoPowerNode a, EcoPowerNode b)
    {
        var go = new GameObject("Test cable"); fixtures.Add(go); var link = go.AddComponent<EcoPowerCable>(); link.a = a; link.b = b; return link;
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.002f;
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        report += "PASS " + label + "\n";
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("power-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
