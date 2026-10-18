#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated integration checks: no authored scene, enemy prefab, or navigation bake is required.
public sealed class EnemyEcologyCheck : MonoBehaviour
{
    private string report = "";
    private GameObject fixtures;
    private EcoWorldClock clock;

    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Enemy ecology check").AddComponent<EnemyEcologyCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/EnemyEcologyValidation.unity");
        EditorApplication.isPlaying = true;
    }

    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Test ground";
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.transform.localScale = new Vector3(40, 0.2f, 40);
        clock = new GameObject("Fixed clear noon").AddComponent<EcoWorldClock>();
        clock.hour = 12; clock.cloudCover = 0;
        clock.advanceTime = clock.varyingWeather = clock.driveLighting = false;
        Physics.SyncTransforms();
        yield return SmokeTargetAndGeneration();
        yield return SpeciesPriorities();
        yield return WindupAndStun();
        yield return SweptProjectiles();
        yield return WaterAndCrops();
        yield return FiniteFire();
        yield return RecoveryProtection();
        yield return MovingSlimeBank();
        File.WriteAllText("enemy-ecology-check-success.txt", report);
        EditorApplication.Exit(0);
    }

    private IEnumerator SmokeTargetAndGeneration()
    {
        ResetCase("Smoke targets solar");
        var wind = PowerStructure("wind", EcoPowerNode.Kind.Wind, new Vector3(2, 0.8f, 1));
        var solar = PowerStructure("solar", EcoPowerNode.Kind.Solar, new Vector3(0, 0.8f, 4));
        var power = solar.GetComponent<EcoPowerNode>();
        power.ratedPower = 10;
        float baseline = power.Generation(clock);
        Check(Near(baseline, 10), "Clear-noon solar fixture produces ten energy per second");
        var smoke = Enemy(EnemyBrain.Species.Smoke);
        smoke.damage = 0; // Match SmokeCreature: its ecological effect must not require physical damage.
        yield return WaitForAttack(smoke);
        Check(BelongsTo(smoke.EcologyTarget, solar.gameObject), "Smoke chooses a farther solar panel over a nearby wind structure");
        yield return new WaitForSeconds(smoke.windup + 0.08f);
        smoke.enabled = false;
        Check(power.SmokeRemaining > 0 && Near(power.EfficiencyMultiplier, 0.5f)
            && Near(power.Generation(clock), baseline * 0.5f), "Actual Smoke attack halves solar generation");
        Check(Near(solar.integrity, 100) && Near(power.condition, 1) && Near(wind.integrity, 100),
            "Smoke reduces output without damaging solar integrity or unrelated structures");
        power.ApplySmoke(); power.ApplySmoke(float.NaN); power.ApplySmoke(8, float.NaN);
        Check(Near(power.Generation(clock), baseline * 0.5f) && Near(power.solarExposure, 1),
            "Refreshed or invalid smoke cannot multiply the penalty or overwrite baseline solar exposure");
        power.Simulate(31);
        Check(Near(power.SmokeRemaining, 0) && Near(power.EfficiencyMultiplier, 1)
            && Near(power.Generation(clock), baseline), "Solar generation returns to its baseline after smoke expires");
    }

    private IEnumerator SpeciesPriorities()
    {
        ResetCase("Slime natural-water priority");
        var region = Region();
        var crop = Crop(new Vector3(-1.5f, 0, 1));
        var purifier = Purifier(new Vector3(2, 0.8f, 2));
        var pond = Pond(new Vector3(0, 0.35f, 4));
        var fish = pond.gameObject.AddComponent<EcoFishingSpot>();
        fish.population = 6;
        var slime = Enemy(EnemyBrain.Species.Slime);
        yield return WaitForAttack(slime);
        Check(BelongsTo(slime.EcologyTarget, pond.gameObject), "Slime prioritizes natural water over a nearer purifier and crop");
        float population = fish.population;
        yield return new WaitForSeconds(slime.windup + 0.08f);
        slime.enabled = false;
        Check(region.waterPollution > 35 && !pond.IsClean && fish.population < population - 0.8f,
            "Actual Slime attack pollutes the pond and reduces its fish population");
        Check(Near(purifier.SlimeRemaining, 0) && Near(crop.SlimeRemaining, 0),
            "The selected water attack does not also apply effects to other candidates");

        ResetCase("Acid crop priority");
        var structure = Structure("battery", new Vector3(2, 0.8f, 1));
        crop = Crop(new Vector3(0, 0, 3));
        var acid = Enemy(EnemyBrain.Species.Acid);
        yield return WaitForAttack(acid);
        Check(BelongsTo(acid.EcologyTarget, crop.gameObject), "Acid chooses a farther crop over a nearby structure");
        float health = crop.health;
        yield return new WaitForSeconds(acid.windup + 0.08f);
        acid.enabled = false;
        Check(crop.health < health && Near(structure.integrity, 100), "Actual Acid windup damages only its selected crop");
    }

    private IEnumerator WindupAndStun()
    {
        ResetCase("Tin structure attack");
        var target = Structure("battery", new Vector3(0, 0.8f, 3));
        var tin = Enemy(EnemyBrain.Species.Tin);
        yield return WaitForAttack(tin);
        Check(Near(target.integrity, 100), "A structure receives no damage at the beginning of attack windup");
        yield return new WaitForSeconds(tin.windup + 0.08f);
        tin.enabled = false;
        Check(Near(target.integrity, 100 - tin.damage), "A real Tin attack applies one structural hit after windup");

        ResetCase("Windup target remains locked");
        target = Structure("battery", new Vector3(0, 0.8f, 3));
        tin = Enemy(EnemyBrain.Species.Tin);
        yield return WaitForAttack(tin);
        target.gameObject.SetActive(false);
        var replacement = Structure("solar", new Vector3(0, 0.8f, 2));
        Physics.SyncTransforms();
        yield return new WaitForSeconds(tin.windup + 0.08f);
        tin.enabled = false;
        Check(Near(target.integrity, 100) && Near(replacement.integrity, 100),
            "Removing a target during windup cannot transfer the pending hit to a replacement");

        ResetCase("Wall inserted during windup");
        target = Structure("battery", new Vector3(0, 0.8f, 3));
        tin = Enemy(EnemyBrain.Species.Tin);
        yield return WaitForAttack(tin);
        Box("Interception wall", new Vector3(0, 1.5f, 1.5f), new Vector3(4, 3, 0.25f));
        Physics.SyncTransforms();
        yield return new WaitForSeconds(tin.windup + 0.08f);
        tin.enabled = false;
        Check(Near(target.integrity, 100), "A wall inserted during windup blocks the pending structural hit");

        ResetCase("Metal stun cancels world attack");
        target = Structure("battery", new Vector3(0, 0.8f, 3));
        tin = Enemy(EnemyBrain.Species.Tin);
        yield return WaitForAttack(tin);
        tin.StunFromMetal(2);
        yield return new WaitForSeconds(tin.windup + 0.08f);
        Check(tin.IsStunned && tin.State == EnemyBrain.Behaviour.Stunned && Near(target.integrity, 100),
            "Tin stun interrupts a structural attack before its hit is delivered");
        tin.enabled = false;
    }

    private IEnumerator SweptProjectiles()
    {
        ResetCase("Ranged Brain attacks a world target");
        var rangedTarget = Structure("solar", new Vector3(0, 0.8f, 4));
        var rangedEnemy = Enemy(EnemyBrain.Species.Tin);
        var template = New("Projectile template").AddComponent<EnemyProjectile>();
        template.transform.position = Vector3.one * 1000;
        template.Launch(rangedEnemy.transform, Vector3.forward, 0.01f, 0, 0);
        rangedEnemy.projectilePrefab = template.gameObject; rangedEnemy.projectileSpeed = 30;
        yield return WaitForAttack(rangedEnemy);
        Check(Near(rangedTarget.integrity, 100), "Ranged ecological attack also respects its windup");
        float deadline = Time.time + 1.5f;
        while (rangedTarget.integrity >= 100 && Time.time < deadline) yield return null;
        rangedEnemy.enabled = false;
        Check(Near(rangedTarget.integrity, 90), "Actual ranged Brain launches a projectile that damages its world target");

        ResetCase("Projectile nearest collision");
        var front = Structure("battery", new Vector3(0, 0.8f, 2));
        var rear = Structure("solar", new Vector3(0, 0.8f, 4));
        var owner = Enemy(EnemyBrain.Species.Tin); owner.enabled = false;
        var shot = Shoot(owner, 5000);
        yield return WaitForImpact(shot);
        Check(Near(front.integrity, 90) && Near(rear.integrity, 100),
            "Fast enemy projectile sweeps the first world collider and cannot damage a target behind it");

        ResetCase("Projectile wall interception");
        front = Structure("solar", new Vector3(0, 0.8f, 4));
        Box("Projectile blocker", new Vector3(0, 1.5f, 2), new Vector3(4, 3, 0.2f));
        owner = Enemy(EnemyBrain.Species.Tin); owner.enabled = false;
        shot = Shoot(owner, 5000);
        yield return WaitForImpact(shot);
        Check(Near(front.integrity, 100), "A non-target wall consumes the projectile before a structure behind it");

        ResetCase("Projectile keeps its launch species");
        var solar = PowerStructure("solar", EcoPowerNode.Kind.Solar, new Vector3(0, 0.8f, 3));
        owner = Enemy(EnemyBrain.Species.Smoke); owner.enabled = false;
        shot = Shoot(owner, 3);
        Destroy(owner.gameObject);
        yield return null;
        Check(owner == null && shot != null, "Projectile remains in flight after its shooter is destroyed");
        yield return WaitForImpact(shot);
        Check(solar.GetComponent<EcoPowerNode>().SmokeRemaining > 0 && Near(solar.integrity, 100),
            "The existing five-argument Launch retains Smoke impact behavior after owner destruction");
    }

    private IEnumerator WaterAndCrops()
    {
        ResetCase("Purifier throughput and electricity");
        var purifier = Purifier(new Vector3(0, 0.8f, 3));
        purifier.litresPerSecond = 4; purifier.electricityPerLitre = 2;
        purifier.dirtyWater = 20;
        var target = EnemyEcoTarget.From(purifier.GetComponent<Collider>(), EnemyBrain.Species.Slime);
        Check(target != null && target.ApplyImpact(EnemyBrain.Species.Slime, 10), "Purifier accepts an ecological Slime impact");
        purifier.ApplySlime(); purifier.ApplySlime(float.PositiveInfinity);
        var battery = New("Battery").AddComponent<EcoPowerNode>();
        battery.kind = EcoPowerNode.Kind.Battery; battery.storedEnergy = 50;
        var cable = New("Purifier power connection").AddComponent<EcoPowerCable>();
        cable.a = battery; cable.b = purifier.GetComponent<EcoPowerNode>();
        var grid = New("Manual power grid").AddComponent<EcoPowerGrid>(); grid.clock = clock; grid.enabled = false;
        Check(Near(purifier.ThroughputMultiplier, 0.5f) && Near(purifier.RequestElectricity(1), 4),
            "Slime halves purifier throughput and its corresponding electrical demand");
        grid.Simulate(1);
        Check(Near(purifier.cleanWater, 2) && Near(purifier.dirtyWater, 18) && Near(battery.storedEnergy, 46),
            "Connected battery loses exactly the electricity needed for two purified litres");
        purifier.Simulate(31); grid.Simulate(1);
        Check(Near(purifier.SlimeRemaining, 0) && Near(purifier.ThroughputMultiplier, 1)
            && Near(purifier.cleanWater, 6) && Near(purifier.dirtyWater, 14) && Near(battery.storedEnergy, 38),
            "Purifier recovers full throughput after slime expiry without creating or wasting electricity");

        ResetCase("Natural water versus stored supply");
        var region = Region();
        var barrel = Box("Clean stored water", new Vector3(2, 0.5f, 2), Vector3.one).AddComponent<WeaponWaterSource>();
        barrel.cleanWater = true; barrel.useRegionalQuality = false;
        var pond = Pond(new Vector3(0, 0.3f, 3));
        pond.Pollute(40);
        Check(!pond.IsClean && barrel.IsClean && region.waterPollution >= 40,
            "Natural-water pollution changes regional water quality without contaminating a clean supply barrel");
        float pollution = region.waterPollution;
        barrel.Pollute(40);
        Check(barrel.IsClean && Near(region.waterPollution, pollution), "Applying pollution to stored supply cannot poison its region");
        Check(EnemyEcoTarget.From(barrel.GetComponent<Collider>(), EnemyBrain.Species.Slime) == null,
            "A clean supply barrel is excluded from Slime world targets");

        ResetCase("Pollution outside authored regions");
        pond = Pond(new Vector3(0, 0.3f, 3));
        pond.Pollute(40); pond.Pollute(float.NaN); pond.Pollute(-5);
        Check(!pond.IsClean && Near(pond.LocalPollution, 40),
            "A natural source outside a region still becomes dirty and rejects invalid pollution");
        var inactiveTarget = EnemyEcoTarget.From(pond.GetComponent<Collider>(), EnemyBrain.Species.Slime);
        pond.gameObject.SetActive(false);
        Check(!inactiveTarget.ApplyImpact(EnemyBrain.Species.Slime, 10) && Near(pond.LocalPollution, 40),
            "A captured target cannot receive more pollution after deactivation");

        ResetCase("Crop exposure and harvest");
        var control = Crop(new Vector3(-2, 0, 3));
        var crop = Crop(new Vector3(2, 0, 3));
        control.growthSeconds = crop.growthSeconds = 100;
        var cropTarget = EnemyEcoTarget.From(crop.GetComponent<Collider>(), EnemyBrain.Species.Slime);
        Check(cropTarget != null && cropTarget.ApplyImpact(EnemyBrain.Species.Slime, 10), "A growing crop accepts a Slime impact");
        Check(!crop.contaminated && crop.ContaminationExposure == 1 && Near(crop.GrowthMultiplier, 0.5f),
            "First Slime exposure temporarily slows a crop without immediately contaminating the harvest");
        control.Simulate(1); crop.Simulate(1);
        Check(Near(crop.growth * 2, control.growth) && crop.health > 0, "A watered slimed crop grows at half the healthy control rate");
        crop.Simulate(31);
        Check(Near(crop.SlimeRemaining, 0) && Near(crop.GrowthMultiplier, 1), "Temporary crop slowdown expires");
        crop.WaterPlant(1, true);
        cropTarget.ApplyImpact(EnemyBrain.Species.Slime, 10);
        Check(crop.contaminated && crop.ContaminationExposure >= 2, "Repeated Slime exposure permanently contaminates the crop");
        crop.growth = 1;
        Check(crop.TryHarvest(out int seeds, out int food) && seeds == 1 && food == 0,
            "A contaminated mature crop yields only one seed and no edible food");
        float health = control.health;
        EnemyEcoTarget.From(control.GetComponent<Collider>(), EnemyBrain.Species.Acid).ApplyImpact(EnemyBrain.Species.Acid, 10);
        Check(control.health < health && control.health > 0, "Acid damages normalized crop health without instantly destroying a healthy plant");
        yield return null;
    }

    private IEnumerator FiniteFire()
    {
        ResetCase("Flame finite deduplicated fire");
        var target = Structure("battery", new Vector3(0, 0.8f, 3));
        target.gameObject.AddComponent<SphereCollider>().radius = 0.3f;
        var flame = Enemy(EnemyBrain.Species.Flame);
        yield return WaitForAttack(flame);
        yield return new WaitForSeconds(flame.windup + 0.08f);
        flame.enabled = false;
        var fires = FindObjectsByType<EcoFire>(FindObjectsSortMode.None);
        Check(fires.Length == 1 && fires[0].Burning && fires[0].fuel > 0 && fires[0].fuel <= 6,
            "An actual Flame attack creates a single fire with finite fuel");
        var fire = fires[0];
        var descriptor = EnemyEcoTarget.From(target.GetComponent<Collider>(), EnemyBrain.Species.Flame);
        float fuel = fire.fuel;
        descriptor.ApplyImpact(EnemyBrain.Species.Flame, 10);
        descriptor.ApplyImpact(EnemyBrain.Species.Flame, 10);
        Check(FindObjectsByType<EcoFire>(FindObjectsSortMode.None).Length == 1 && Near(fire.fuel, fuel),
            "Repeated Flame impacts do not duplicate a nearby fire or refresh its fuel");
        float integrity = target.integrity;
        fire.Simulate(1);
        Check(Near(integrity - target.integrity, 3 * fire.intensity),
            "One fire tick damages a structure once even when it has multiple colliders");
        fire.Simulate(100);
        Check(!fire.Burning && Near(fire.fuel, 0), "A fire burns out after exhausting its finite fuel");
        integrity = target.integrity;
        fire.Simulate(100);
        Check(Near(target.integrity, integrity), "A spent fire cannot keep damaging its surroundings");
    }

    private IEnumerator RecoveryProtection()
    {
        ResetCase("Recovery protects projectile victim");
        var playerObject = New("Recovering player"); playerObject.transform.position = new Vector3(0, 0, 3);
        var collider = playerObject.AddComponent<BoxCollider>();
        collider.center = Vector3.up * 0.65f; collider.size = new Vector3(0.6f, 1.3f, 0.6f);
        var player = playerObject.AddComponent<PlayerVitals>(); player.showLegacyHUD = false;
        player.ReceiveHit(1000); player.SimulateRecovery(1);
        float energy = player.Energy;
        var owner = Enemy(EnemyBrain.Species.Acid); owner.enabled = false;
        var shot = Shoot(owner, 20);
        yield return WaitForImpact(shot);
        Check(player.IsRecovering && player.Energy >= energy, "A real projectile cannot drain energy during five-second player recovery");
        player.SimulateRecovery(4);
        Check(!player.IsRecovering && Near(player.Energy, player.maximumEnergy) && Near(player.MovementMultiplier, 1),
            "The intercepted recovery hit does not leave a delayed slow effect after recovery");
    }

    private IEnumerator MovingSlimeBank()
    {
        // Keep navigation last so its global surface cannot change earlier, deliberately stationary fixtures.
        ResetCase("Short Slime approaches a reachable pond bank");
        var region = Region();
        var pond = Box("Wide shallow natural pond", new Vector3(0, 0.1f, 5), new Vector3(4, 0.2f, 4))
            .AddComponent<WeaponWaterSource>();
        pond.useRegionalQuality = true;
        var slime = Enemy(EnemyBrain.Species.Slime); slime.enabled = false;
        var motor = slime.GetComponent<CharacterController>();
        motor.height = 0.9f; motor.center = Vector3.up * 0.45f; motor.radius = 0.3f;
        slime.attackRange = 1.25f; slime.chaseSpeed = slime.moveSpeed = 2;
        var navigation = New("Pond navigation").AddComponent<EcoNavigation>();
        navigation.worldSize = new Vector3(24, 8, 24);
        Physics.SyncTransforms();
        float deadline = Time.time + 10;
        while (!navigation.Ready && Time.time < deadline) yield return null;
        Check(navigation.Ready, "Isolated pond test builds its navigation surface");
        Vector3 start = slime.transform.position;
        slime.enabled = true;
        deadline = Time.time + 8;
        float nextTrace = 0;
        bool enteredPond = false;
        var bounds = pond.GetComponent<Collider>().bounds;
        while (region.waterPollution < 35 && Time.time < deadline)
        {
            Vector3 position = slime.transform.position;
            enteredPond |= position.x > bounds.min.x && position.x < bounds.max.x
                && position.z > bounds.min.z && position.z < bounds.max.z;
            if (Time.time >= nextTrace)
            {
                nextTrace = Time.time + 1;
                report += "BANK " + position + " state=" + slime.State + " route="
                    + slime.GetComponent<EcoEnemyPath>().HasRoute + " target="
                    + (slime.EcologyTarget != null ? slime.EcologyTarget.name : "none") + "\n";
            }
            yield return null;
        }
        slime.enabled = false;
        Check(Vector3.Distance(start, slime.transform.position) > 1,
            "A short Slime actually travels toward the pond instead of attacking from its spawn");
        Check(region.waterPollution >= 35 && !pond.IsClean,
            "A moving short Slime reaches attack range and pollutes the pond from its reachable bank");
        Vector3 finalPosition = slime.transform.position;
        bool outside = finalPosition.x <= bounds.min.x || finalPosition.x >= bounds.max.x
            || finalPosition.z <= bounds.min.z || finalPosition.z >= bounds.max.z;
        Check(!enteredPond && outside, "Bank approach keeps the land enemy outside the pond's horizontal bounds");
    }

    private void ResetCase(string name)
    {
        if (fixtures != null) { fixtures.SetActive(false); Destroy(fixtures); }
        foreach (var fire in FindObjectsByType<EcoFire>(FindObjectsSortMode.None))
        { fire.gameObject.SetActive(false); Destroy(fire.gameObject); }
        fixtures = new GameObject(name);
        report += "CASE " + name + "\n";
        Physics.SyncTransforms();
    }

    private GameObject New(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(fixtures.transform); return go;
    }

    private GameObject Box(string name, Vector3 position, Vector3 size)
    {
        var go = New(name); go.transform.position = position;
        go.AddComponent<BoxCollider>().size = size; return go;
    }

    private EcoStructure Structure(string id, Vector3 position)
    {
        var structure = Box(id, position, new Vector3(0.8f, 1.6f, 0.8f)).AddComponent<EcoStructure>();
        structure.buildingId = id; return structure;
    }

    private EcoStructure PowerStructure(string id, EcoPowerNode.Kind kind, Vector3 position)
    {
        var go = Box(id, position, new Vector3(0.8f, 1.6f, 0.8f));
        var power = go.AddComponent<EcoPowerNode>(); power.kind = kind; power.checkShade = false;
        var structure = go.AddComponent<EcoStructure>(); structure.buildingId = id; return structure;
    }

    private EcoWaterNode Purifier(Vector3 position)
    {
        var structure = PowerStructure("purifier", EcoPowerNode.Kind.Consumer, position);
        var water = structure.gameObject.AddComponent<EcoWaterNode>(); water.kind = EcoWaterNode.Kind.Purifier;
        return water;
    }

    private EcoRegion Region()
    {
        var region = New("Clean test region").AddComponent<EcoRegion>(); region.size = new Vector2(30, 30); return region;
    }

    private WeaponWaterSource Pond(Vector3 position)
    {
        var water = Box("Natural pond", position, new Vector3(1.5f, 0.7f, 1.5f)).AddComponent<WeaponWaterSource>();
        water.useRegionalQuality = true; return water;
    }

    private PlantedSeed Crop(Vector3 position)
    {
        var go = Box("Crop", position, new Vector3(0.5f, 0.5f, 0.5f));
        go.GetComponent<BoxCollider>().center = Vector3.up * 0.25f;
        var visual = new GameObject("Crop Visual"); visual.transform.SetParent(go.transform, false);
        var crop = go.AddComponent<PlantedSeed>(); crop.moisture = 1; return crop;
    }

    private EnemyBrain Enemy(EnemyBrain.Species species)
    {
        var go = New(species.ToString()); go.transform.position = new Vector3(0, 0.03f, 0);
        var motor = go.AddComponent<CharacterController>(); motor.radius = 0.3f; motor.height = 1.6f; motor.center = Vector3.up * 0.8f;
        var enemy = go.AddComponent<EnemyBrain>(); enemy.species = species;
        enemy.moveSpeed = enemy.chaseSpeed = enemy.patrolRadius = 0;
        enemy.detectionRange = 15; enemy.leashRange = 20; enemy.attackRange = 6;
        enemy.windup = 0.35f; enemy.attackDuration = 1; enemy.cooldown = 5; enemy.damage = 10;
        Physics.SyncTransforms(); return enemy;
    }

    private EnemyProjectile Shoot(EnemyBrain owner, float speed)
    {
        var go = New("Live enemy projectile"); go.transform.position = new Vector3(0, 0.8f, 0.7f);
        var shot = go.AddComponent<EnemyProjectile>();
        shot.Launch(owner.transform, Vector3.forward, speed, 10, 2);
        Physics.SyncTransforms(); return shot;
    }

    private IEnumerator WaitForAttack(EnemyBrain enemy)
    {
        Physics.SyncTransforms();
        float deadline = Time.time + 2;
        while (enemy.State != EnemyBrain.Behaviour.Attack && Time.time < deadline) yield return null;
        Check(enemy.State == EnemyBrain.Behaviour.Attack, enemy.species + " enters its ordinary attack state for a world target");
    }

    private IEnumerator WaitForImpact(EnemyProjectile shot)
    {
        float deadline = Time.time + 1;
        while (shot != null && Time.time < deadline) yield return null;
        Check(shot == null, "Live projectile is consumed by its swept collision");
    }

    private static bool BelongsTo(Component component, GameObject go) => component != null
        && (component.gameObject == go || component.transform.IsChildOf(go.transform));
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.003f;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report += "PASS " + message + "\n";
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("enemy-ecology-check-failed.txt", report + message + "\n" + stack);
        EditorApplication.Exit(1);
    }
}
#endif
