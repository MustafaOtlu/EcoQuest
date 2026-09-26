#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class WaterInfrastructureCheck : MonoBehaviour
{
    private string report = "";
    private GameObject fixture;
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Water infrastructure check").AddComponent<WaterInfrastructureCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/WaterValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var power = FindFirstObjectByType<EcoPowerGrid>(); power.enabled = false; power.clock.enabled = false;
        var waterGrid = FindFirstObjectByType<EcoWaterGrid>();
        Check(waterGrid != null, "MainScene includes the water simulation alongside electricity"); waterGrid.enabled = false;
        fixture = new GameObject("Water calculation fixtures");
        var a = Water(EcoWaterNode.Kind.Tank, 100); var b = Water(EcoWaterNode.Kind.Tank, 10); var c = Water(EcoWaterNode.Kind.Tank, 100);
        a.dirtyWater = 20; a.cleanWater = 30; b.dirtyWater = 8;
        var ab = Pipe(a, b, EcoWaterNode.Fluid.Clean); var ac = Pipe(a, c, EcoWaterNode.Fluid.Clean); ac.litresPerSecond = 100;
        var ca = Pipe(c, a, EcoWaterNode.Fluid.Clean); ca.litresPerSecond = 100;
        ac.connected = ca.connected = false; waterGrid.Simulate(1);
        Check(Near(b.cleanWater, 2) && Near(b.dirtyWater, 8), "Shared tank capacity reserves headroom without contaminating the separate clean chamber");
        ac.connected = ca.connected = true; waterGrid.Simulate(1);
        Check(Near(a.cleanWater + b.cleanWater + c.cleanWater, 30) && Near(a.dirtyWater + b.dirtyWater + c.dirtyWater, 28), "Branched and cyclic pipes conserve both clean and dirty water");
        Check(ca.LastFlow == 0, "Water received this frame cannot travel around a cycle immediately");
        Check(a.cleanWater >= 0 && c.cleanWater >= 0 && b.Space == 0, "No pipe creates negative volume or tank overflow");
        ca.connected = false; a.cleanWater = 4; b.cleanWater = b.dirtyWater = c.cleanWater = 0; ac.litresPerSecond = ab.litresPerSecond = 8;
        waterGrid.Simulate(1);
        Check(Near(b.cleanWater, 2) && Near(c.cleanWater, 2), "Scarce water is shared between equally demanding branches regardless of object order");
        ab.connected = ac.connected = ca.connected = false;
        float stored = c.cleanWater; waterGrid.Simulate(4); Check(c.cleanWater == stored && ac.LastFlow == 0, "Disconnected pipes stop flow immediately");
        Check(a.Add(EcoWaterNode.Fluid.Clean, float.NaN) == 0 && a.Take(EcoWaterNode.Fluid.Dirty, -1) == 0, "Invalid transfers cannot corrupt water inventory");
        var raw = Water(EcoWaterNode.Kind.Intake, 30); var purifier = Water(EcoWaterNode.Kind.Purifier, 20);
        purifier.litresPerSecond = 3; purifier.electricityPerLitre = 2;
        Check(!purifier.CanSend(EcoWaterNode.Fluid.Dirty) && !purifier.CanReceive(EcoWaterNode.Fluid.Clean) && !raw.CanReceive(EcoWaterNode.Fluid.Dirty), "Purifier and intake ports enforce fluid direction");
        var consumer = purifier.gameObject.AddComponent<EcoPowerNode>(); consumer.kind = EcoPowerNode.Kind.Consumer;
        var battery = new GameObject("Test battery").AddComponent<EcoPowerNode>(); battery.transform.SetParent(fixture.transform); battery.kind = EcoPowerNode.Kind.Battery; battery.storedEnergy = 5;
        var cable = new GameObject("Test power connection").AddComponent<EcoPowerCable>(); cable.transform.SetParent(fixture.transform); cable.a = battery; cable.b = consumer;
        purifier.dirtyWater = 10; power.Simulate(1);
        Check(Near(purifier.dirtyWater, 7.5f) && Near(purifier.cleanWater, 2.5f) && Near(battery.storedEnergy, 0), "Partial power converts exactly one litre per two electricity units");
        power.Simulate(1); Check(Near(purifier.cleanWater, 2.5f), "An unpowered purifier produces no clean water");
        battery.storedEnergy = 30; cable.connected = false; power.Simulate(1);
        Check(Near(battery.storedEnergy, 30) && Near(purifier.cleanWater, 2.5f), "Disconnected purifier cannot spend a nearby battery");
        cable.connected = true; power.Simulate(10);
        Check(Near(purifier.cleanWater, 10) && purifier.dirtyWater == 0 && Near(battery.storedEnergy, 15), "Processing ends at available raw water without wasting electricity");
        power.Simulate(1); Check(Near(battery.storedEnergy, 15), "Empty purifier requests no idle electricity");
        purifier.capacity = 10; purifier.cleanWater = 0; purifier.dirtyWater = 10; power.Simulate(1);
        Check(Near(purifier.cleanWater, 3) && Near(purifier.dirtyWater, 7) && purifier.Space == 0, "Purification preserves total volume even when the chamber is full");
        var intakePower = raw.gameObject.AddComponent<EcoPowerNode>(); intakePower.kind = EcoPowerNode.Kind.Consumer;
        var sourceObject = GameObject.CreatePrimitive(PrimitiveType.Cube); sourceObject.transform.SetParent(fixture.transform); sourceObject.transform.position = raw.transform.position + Vector3.forward;
        raw.source = sourceObject.AddComponent<WeaponWaterSource>(); raw.source.cleanWater = false; raw.electricityPerLitre = 0.5f;
        cable.b = intakePower; battery.storedEnergy = 20; power.Simulate(1);
        Check(Near(raw.dirtyWater, 4) && Near(battery.storedEnergy, 18), "Powered intake draws dirty source water at its metered pump cost");
        raw.dirtyWater = 29; power.Simulate(1);
        Check(Near(raw.dirtyWater, 30) && Near(battery.storedEnergy, 17.5f), "Full intake buffer stops the pump without overcharging");
        raw.dirtyWater = 0; raw.source.cleanWater = true; power.Simulate(1);
        Check(Near(raw.cleanWater, 4) && raw.dirtyWater == 0, "A clean source remains clean through the intake");
        raw.gameObject.AddComponent<EcoStructure>().ReceiveDamage(100); power.Simulate(1);
        Check(Near(raw.cleanWater, 4) && intakePower.DeliveredPower == 0, "Destroyed intake cannot draw water or electricity");
        DestroyImmediate(fixture);

        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); var builder = weapon.Builder;
        var player = weapon.GetComponentInParent<PlayerController>(); player.enabled = false;
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = new Vector3(20, 0.03f, 0); cc.enabled = true;
        fixture = new GameObject("Water placement fixtures");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.SetParent(fixture.transform); ground.transform.position = new Vector3(23, -0.25f, 3); ground.transform.localScale = new Vector3(14, 0.5f, 12); ground.AddComponent<PlantingSurface>();
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube); source.transform.SetParent(fixture.transform); source.transform.position = new Vector3(20, 0.4f, 2); source.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f); source.AddComponent<WeaponWaterSource>().cleanWater = false;
        Physics.SyncTransforms(); weapon.ReturnMaterials(300, 300); builder.SetBuildMode(true);
        int intakeIndex = EcoBuildingCatalog.Find("intake"), purifierIndex = EcoBuildingCatalog.Find("purifier"), tankIndex = EcoBuildingCatalog.Find("tank");
        Check(!builder.TryPlace(purifierIndex, new Vector3(23, 0.03f, 3.5f), Quaternion.identity), "Water infrastructure respects the GDD level-four gate");
        weapon.Progression.Restore(EcoProgression.Threshold(4));
        builder.NextPage(); Check(builder.Page == 1 && builder.SlotName(0) == "Su arıtma" && builder.SlotName(1) == "Su deposu", "Second construction page exposes water buildings");
        builder.Select(builder.PipeIndex); Check(builder.SlotName(builder.Selection % 5) == "Su borusu", "Pipe remains reachable on its construction page");
        builder.Select(builder.PipeIndex); builder.TogglePipeFluid(); Check(builder.PipeFluid == EcoWaterNode.Fluid.Clean, "Pipe selection can switch to the clean-water line"); builder.TogglePipeFluid();
        Check(!builder.TryPlace(intakeIndex, new Vector3(25, 0.03f, 0), Quaternion.identity), "A pump cannot be built away from a source");
        Check(builder.TryPlace(intakeIndex, new Vector3(20, 0.03f, 3.5f), Quaternion.identity), "Intake can be built beside an actual source");
        Check(builder.TryPlace(purifierIndex, new Vector3(23, 0.03f, 3.5f), Quaternion.identity), "Arıtma prefab places with its full foundation and collider");
        Check(builder.TryPlace(tankIndex, new Vector3(25, 0.03f, 3.5f), Quaternion.identity), "Water tank prefab can be built separately");
        var pump = EcoWaterNode.Active.First(n => n.kind == EcoWaterNode.Kind.Intake);
        var treatment = EcoWaterNode.Active.First(n => n.kind == EcoWaterNode.Kind.Purifier);
        var tank = EcoWaterNode.Active.First(n => n.kind == EcoWaterNode.Kind.Tank);
        Check(pump.source != null && pump.dirtyWater == 0 && treatment.cleanWater == 0 && tank.cleanWater == 0, "New buildings resolve their source and contain no free water");
        int plastic = weapon.Plastic;
        Check(builder.TryConnectPipe(pump, treatment, EcoWaterNode.Fluid.Dirty) && weapon.Plastic == plastic - 6, "Directed dirty pipe spends exactly two plastic per rounded-up metre");
        plastic = weapon.Plastic;
        Check(!builder.TryConnectPipe(pump, treatment, EcoWaterNode.Fluid.Dirty) && weapon.Plastic == plastic, "Duplicate pipe cannot spend materials twice");
        Check(!builder.TryConnectPipe(treatment, pump, EcoWaterNode.Fluid.Clean) && weapon.Plastic == plastic, "Invalid return connection is rejected atomically");
        Check(builder.TryConnectPipe(treatment, tank, EcoWaterNode.Fluid.Clean), "Clean output can be piped to the tank");
        var electrical = new GameObject("Water test battery").AddComponent<EcoPowerNode>(); electrical.transform.SetParent(fixture.transform); electrical.kind = EcoPowerNode.Kind.Battery; electrical.storedEnergy = 100;
        foreach (var node in new[] { pump, treatment }) { var link = new GameObject("Facility electricity").AddComponent<EcoPowerCable>(); link.transform.SetParent(fixture.transform); link.a = electrical; link.b = node.GetComponent<EcoPowerNode>(); }
        for (int i = 0; i < 5; i++) { power.Simulate(1); waterGrid.Simulate(1); }
        Check(tank.cleanWater > 0 && treatment.dirtyWater > 0 && electrical.storedEnergy < 100, "Built pump-to-purifier-to-tank chain produces stored clean water using actual electricity");
        var pipes = EcoWaterPipe.Active.ToArray();
        plastic = weapon.Plastic;
        Check(builder.RemovePipe(pump, treatment, EcoWaterNode.Fluid.Dirty) && weapon.Plastic == plastic + 3, "Removing a player pipe refunds half its plastic");
        Check(!builder.RemovePipe(pump, treatment, EcoWaterNode.Fluid.Dirty) && weapon.Plastic == plastic + 3, "Repeated pipe removal cannot duplicate a refund");
        builder.SetBuildMode(false);
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        cc.enabled = false; player.transform.position = tank.transform.position + Vector3.back * 2; cc.enabled = true;
        camera.transform.position = player.transform.position + Vector3.up * 1.2f; camera.transform.rotation = Quaternion.LookRotation(Vector3.up);
        weapon.SelectTool(0); weapon.CycleMode(); weapon.CycleMode(); weapon.Use(5);
        Check(Near(weapon.Water, 50), "Existing water weapon still consumes its reservoir");
        tank.cleanWater = 20; tank.dirtyWater = 30; camera.transform.LookAt(tank.GetComponent<Collider>().bounds.center); Physics.SyncTransforms();
        weapon.Interact(); Check(Near(weapon.Water, 70) && tank.cleanWater == 0 && Near(tank.dirtyWater, 30) && !weapon.HasDirtyWater, "Actual E interaction transfers only available clean tank water into the weapon");
        weapon.Interact(); Check(Near(weapon.Water, 70) && Near(tank.dirtyWater, 30), "Clean-water interaction never silently substitutes dirty water");
        weapon.CycleMode(); weapon.Use(0.2f);
        Check(Near(weapon.Water, 77) && Near(tank.dirtyWater, 23) && weapon.HasDirtyWater, "Vacuum collection deliberately draws dirty water and preserves the contamination flag");
        Check(weapon.ContextHint().Contains("Su deposu") && weapon.ContextHint().Contains("Kirli"), "Water context exposes both reservoir levels to the player");
        var vitals = player.GetComponent<PlayerVitals>(); vitals.enabled = false; vitals.ReceiveHit(10000);
        tank.cleanWater = 10; weapon.Interact(); Check(Near(tank.cleanWater, 10) && Near(weapon.Water, 77), "Recovery blocks water interactions without draining the tank");
        vitals.SimulateRecovery(5);
        var structure = treatment.GetComponent<EcoStructure>(); Check(structure.Dismantle(weapon), "Player-built treatment facility can be dismantled");
        yield return null; yield return null;
        Check(pipes.All(p => p == null), "Dismantling a facility removes its attached water pipes");
        File.WriteAllText("water-check-success.txt", report); EditorApplication.Exit(0);
    }
    private EcoWaterNode Water(EcoWaterNode.Kind kind, float capacity)
    {
        var go = new GameObject(kind.ToString()); go.transform.SetParent(fixture.transform); go.transform.position = new Vector3(-30, 0, -30);
        var node = go.AddComponent<EcoWaterNode>(); node.kind = kind; node.capacity = capacity; return node;
    }
    private EcoWaterPipe Pipe(EcoWaterNode from, EcoWaterNode to, EcoWaterNode.Fluid fluid)
    {
        var go = new GameObject("Pipe fixture"); go.transform.SetParent(fixture.transform); var pipe = go.AddComponent<EcoWaterPipe>(); pipe.from = from; pipe.to = to; pipe.fluid = fluid; return pipe;
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("water-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
