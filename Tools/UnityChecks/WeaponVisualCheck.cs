#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
<<<<<<< HEAD
=======
using System.Linq;
>>>>>>> 745f61ed57cb575d070a9391ad0cb1c1d5851527
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class WeaponVisualCheck : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    private string report = "";
    private Camera view;

    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var check = new GameObject("Weapon visual check").AddComponent<WeaponVisualCheck>();
        var player = new GameObject("Player"); player.AddComponent<PlayerVitals>(); player.AddComponent<PlayerController>().enabled = false;
        var camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform); camera.transform.localPosition = Vector3.up * 0.7f;
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.16f, 0.2f, 0.25f);
        var mount = new GameObject("Holder"); mount.transform.SetParent(camera.transform); mount.transform.localPosition = new Vector3(0.13f, -0.2f, 0.35f);
        var holder = mount.AddComponent<FirstPersonWeaponHolder>();
        var model = GameObject.CreatePrimitive(PrimitiveType.Cube); model.transform.SetParent(mount.transform, false);
        model.transform.localScale = Vector3.one * 0.1f; DestroyImmediate(model.GetComponent<Collider>());
        var holderData = new SerializedObject(holder);
        holderData.FindProperty("equippedWeapon").objectReferenceValue = model.transform;
        holderData.FindProperty("viewTransform").objectReferenceValue = camera.transform; holderData.ApplyModifiedPropertiesWithoutUndo();
        var gun = mount.AddComponent<PlayerWeaponSystem>(); gun.holder = holder;
        gun.effectMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WeaponEffect.mat");
        for (int i = 1; i < 4; i++) gun.modelPrefabs[i] = model;
        check.weapons = gun;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = Vector3.down * 0.5f;
        floor.transform.localScale = new Vector3(30, 1, 40); floor.AddComponent<PlantingSurface>();
        EditorSceneManager.SaveScene(check.gameObject.scene, "Assets/WeaponVisualValidation.unity"); EditorApplication.isPlaying = true;
    }

    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; weapons.enabled = false; yield return null;
        view = Camera.main;
        var effects = weapons.GetComponentInChildren<WeaponVisualEffects>();
        Check(effects != null && weapons.GetComponentInParent<PlayerController>().GetComponentsInChildren<LineRenderer>(true).Length == 0,
            "Player weapons create moving particles and no rigid aim-ray renderer");
        var renderer = effects.Stream.GetComponent<ParticleSystemRenderer>();
        Check(renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null && renderer.sharedMaterial.renderQueue >= 3000,
            "Droplets use the transparent round-particle material rather than an opaque rectangle");
        Check(effects.Stream.main.simulationSpace == ParticleSystemSimulationSpace.World && effects.Stream.emission.enabled == false
            && effects.Stream.collision.enabled == false && effects.GetComponentsInChildren<Collider>().Length == 0,
            "Cosmetic particles have bounded world-space simulation and cannot become hitboxes");
        weapons.SelectTool(0); weapons.CycleMode(); weapons.CycleMode();
<<<<<<< HEAD
        var flame = Enemy(EnemyBrain.Species.Flame, new Vector3(0, 0.03f, 5)); Physics.SyncTransforms();
        float health = flame.Health, energy = weapons.Energy, water = weapons.Water;
        weapons.Use(0.1f);
=======
        var flame = Enemy(EnemyBrain.Species.Flame, new Vector3(0, 0.03f, 5));
        // A new CharacterController joins the physics scene on its first fixed step.
        yield return new WaitForFixedUpdate(); Physics.SyncTransforms();
        float health = flame.Health, energy = weapons.Energy, water = weapons.Water;
        report += "INFO water fixture before: " + WaterDiagnostic(flame) + "\n";
        weapons.Use(0.1f);
        report += "INFO water fixture after: " + WaterDiagnostic(flame) + "\n";
>>>>>>> 745f61ed57cb575d070a9391ad0cb1c1d5851527
        Check(flame.Health < health && Near(weapons.Energy, energy - 0.5f) && Near(weapons.Water, water - 1),
            "Replacing the stream preserves actual water damage and exact electricity/water costs");
        Check(effects.CurrentFlow == WeaponVisualEffects.Flow.Water && effects.Stream.particleCount > 0 && effects.Impacts.particleCount > 0,
            "Water emits individual moving droplets and a contact splash");
        var particles = new ParticleSystem.Particle[300];
        int count = effects.Stream.GetParticles(particles); bool outward = false;
        for (int i = 0; i < count; i++) outward |= Vector3.Dot(particles[i].velocity, view.transform.forward) > 15;
        Check(outward, "Water particles travel outward along the shot instead of forming a stationary bar");
        for (int i = 0; i < 12; i++) { weapons.Use(1f / 60); yield return null; }
        Capture("weapon-water-particles-preview.png");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 0.7f, 2.5f);
        wall.transform.localScale = new Vector3(2, 2, 0.1f); Physics.SyncTransforms();
        health = flame.Health; weapons.Use(0.1f);
        Check(Near(health, flame.Health), "The real water attack still stops at a wall");
        Destroy(wall); Destroy(flame.gameObject); yield return null;

        weapons.CycleMode(); // Water -> Collect
        var scrap = GameObject.CreatePrimitive(PrimitiveType.Cube); scrap.transform.position = new Vector3(0, 0.7f, 5);
        scrap.AddComponent<RecyclableResource>(); Physics.SyncTransforms(); weapons.Use(0.01f);
        count = effects.Stream.GetParticles(particles); bool inward = false;
        for (int i = 0; i < count; i++) inward |= Vector3.Dot(particles[i].velocity, view.transform.forward) < -5;
        Check(effects.CurrentFlow == WeaponVisualEffects.Flow.Collect && inward, "Vacuum dust visibly moves from the target toward the muzzle");
        var state = UnityEngine.Random.state; float expected = UnityEngine.Random.value; UnityEngine.Random.state = state;
        effects.Show(WeaponVisualEffects.Flow.Collect, Vector3.zero, Vector3.forward * 5, false, Vector3.back, 0.05f);
        Check(UnityEngine.Random.value == expected, "Cosmetic scatter does not consume gameplay randomness"); UnityEngine.Random.state = state;
        for (int i = 0; i < 10; i++) { weapons.Use(0.01f); yield return null; }
        Capture("weapon-vacuum-particles-preview.png"); Destroy(scrap); yield return null;
        weapons.CycleMode(); weapons.Use(0.05f);
        Check(effects.CurrentFlow == WeaponVisualEffects.Flow.Disperse && effects.Stream.particleCount > 0, "Disperse uses outward airflow particles");
        weapons.SelectTool(2); weapons.Use(0.05f);
        Check(effects.CurrentFlow == WeaponVisualEffects.Flow.Recycle && effects.Stream.particleCount > 0, "Recycler has a distinct green collection effect");
        weapons.SelectTool(4);
        Check(effects.Stream.particleCount == 0 && effects.Impacts.particleCount == 0, "Switching to empty hands clears the previous weapon effect");

        weapons.SelectTool(1); int seeds = weapons.Seeds; energy = weapons.Energy; weapons.Use(0.01f);
        var seed = FindFirstObjectByType<PlayerSeedProjectile>();
        Check(seed != null && seed.GetComponent<Renderer>() != null && seed.GetComponent<WeaponProjectileVisual>() != null
            && seed.GetComponentsInChildren<LineRenderer>().Length == 0 && weapons.Seeds == seeds - 1 && Near(weapons.Energy, energy - 2),
            "Seed shot is a visible travelling seed with a short particle wake and unchanged cost");
        Vector3 start = seed.transform.position; yield return new WaitForSeconds(0.07f);
        Check(seed != null && Vector3.Distance(start, seed.transform.position) > 0.5f && seed.GetComponentInChildren<ParticleSystem>().particleCount > 0,
            "Seed body and wake move with the real projectile");
        Destroy(seed.gameObject); yield return new WaitForSeconds(0.5f);
        var tin = Enemy(EnemyBrain.Species.Tin, new Vector3(0, 0.03f, 5)); Physics.SyncTransforms();
        weapons.CycleMode(); int ammo = weapons.MetalAmmo; health = tin.Health; weapons.Use(0.01f);
        var metal = FindFirstObjectByType<PlayerMetalProjectile>();
        Check(metal != null && metal.GetComponent<WeaponProjectileVisual>() != null && metal.GetComponentsInChildren<LineRenderer>().Length == 0,
            "Metal shot is a visible ball rather than a full-length tracer");
        yield return new WaitForSeconds(0.3f);
        Check(tin.Health < health && weapons.MetalAmmo == ammo - 1, "The decorated metal ball still sweeps into Tin and consumes one round");
        Destroy(tin.gameObject);
        weapons.SelectTool(0); var vitals = weapons.GetComponentInParent<PlayerVitals>(); vitals.ReceiveHit(1000);
        energy = weapons.Energy; water = weapons.Water; weapons.Use(0.1f);
        Check(Near(weapons.Energy, energy) && Near(weapons.Water, water) && effects.Stream.particleCount == 0,
            "Recovery blocks both actual attacks and new particle emission");
        effects.Show(WeaponVisualEffects.Flow.Water, Vector3.zero, Vector3.forward * 12, false, Vector3.back, 100000);
        Check(effects.Stream.particleCount <= 20, "Large simulation steps cannot emit an unbounded particle burst");
        effects.Stop(); yield return new WaitForSeconds(0.7f);
        Check(effects.Stream.particleCount == 0 && effects.Impacts.particleCount == 0, "Stopped particles expire and leave no persistent aiming object");
        File.WriteAllText("weapon-visual-check-success.txt", report); EditorApplication.Exit(0);
    }

    private static EnemyBrain Enemy(EnemyBrain.Species species, Vector3 position)
    {
        var root = new GameObject(species.ToString()); root.transform.position = position;
        var motor = root.AddComponent<CharacterController>(); motor.height = 1.4f; motor.radius = 0.3f; motor.center = Vector3.up * 0.7f;
        var enemy = root.AddComponent<EnemyBrain>(); enemy.species = species; enemy.enabled = false;
        motor.Move(Vector3.zero); return enemy;
    }
    private void Capture(string path)
    {
        var target = new RenderTexture(1280, 720, 24); target.Create(); view.targetTexture = target; view.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG()); RenderTexture.active = null; view.targetTexture = null;
        target.Release(); Destroy(target); Destroy(image);
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.003f;
<<<<<<< HEAD
=======
    private string WaterDiagnostic(EnemyBrain target)
    {
        var motor = target.GetComponent<CharacterController>();
        var hits = Physics.RaycastAll(view.transform.position, view.transform.forward, 12, ~0, QueryTriggerInteraction.Ignore)
            .OrderBy(hit => hit.distance).Select(hit => hit.collider.name + "@" + hit.distance.ToString("F3")
                + " enemy=" + (hit.collider.GetComponentInParent<EnemyBrain>() != null));
        return "health=" + target.Health + " energy=" + weapons.Energy + " water=" + weapons.Water
            + " tool=" + weapons.Selected + " mode=" + weapons.Mode + " camera=" + view.transform.position
            + " forward=" + view.transform.forward + " target=" + target.transform.position + " bounds=" + motor.bounds
            + " blocked=" + EcoMarketUI.BlocksGameplay + " build=" + weapons.Builder.IsBuilding
            + " recovering=" + weapons.GetComponentInParent<PlayerVitals>().IsRecovering + " hits=[" + string.Join(", ", hits) + "]";
    }
>>>>>>> 745f61ed57cb575d070a9391ad0cb1c1d5851527
    private void Check(bool success, string label) { if (!success) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("weapon-visual-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
