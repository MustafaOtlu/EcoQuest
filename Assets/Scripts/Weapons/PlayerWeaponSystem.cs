using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(20)]
public sealed class PlayerWeaponSystem : MonoBehaviour
{
    public enum Tool { Vacuum, SeedGun, Recycler, Scanner, Hands }
    public enum VacuumMode { Collect, Disperse, Water }
    public FirstPersonWeaponHolder holder;
    public GameObject[] modelPrefabs = new GameObject[4];
    public Material effectMaterial;
    public float maximumEnergy = 100f, maximumWater = 100f;
    [field: SerializeField] public Tool Selected { get; private set; }
    [field: SerializeField] public VacuumMode Mode { get; private set; }
    [field: SerializeField] public bool MetalMode { get; private set; }
    [field: SerializeField] public float Energy { get; private set; }
    [field: SerializeField] public float Water { get; private set; }
    [field: SerializeField] public int MetalAmmo { get; private set; } = 40;
    [field: SerializeField] public int Seeds { get; private set; } = 20;
    [field: SerializeField] public int Metal { get; private set; }
    [field: SerializeField] public int Plastic { get; private set; }
    [field: SerializeField] public int CollectedMetalScrap { get; private set; }
    [field: SerializeField] public int CollectedPlasticScrap { get; private set; }
    [field: SerializeField] public string LastScan { get; private set; } = "";
    [field: SerializeField] public int Food { get; private set; }
    [field: SerializeField] public int Compost { get; private set; }
    public string Feedback { get; private set; }
    public float FeedbackUntil { get; private set; }
    public event Action Changed;
    public event Action<string> Scanned;
    private Transform[] models = new Transform[4];
    private Transform owner;
    private PlayerVitals vitals;
    private Camera view;
    private LineRenderer beam;
    private float nextShot, nextScan;
    private RecyclableResource processing;
    private float progress;
    private bool dirtyWater;
    private float jamUntil;
    private bool wasLocked;
    private MaterialPropertyBlock tint;

    private void Start()
    {
        tint = new MaterialPropertyBlock();
        owner = GetComponentInParent<PlayerController>().transform;
        vitals = owner.GetComponent<PlayerVitals>();
        view = Camera.main;
        Energy = maximumEnergy; Water = maximumWater;
        models[0] = holder.EquippedWeapon;
        for (int i = 1; i < models.Length; i++)
        {
            if (modelPrefabs[i] == null) continue;
            var model = Instantiate(modelPrefabs[i], holder.transform).transform;
            model.localRotation = Quaternion.Euler(-90f, -90f, 0f);
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;
            FitModel(model, i == 3 ? 0.25f : i == 2 ? 0.4f : 0.46f);
            models[i] = model; model.gameObject.SetActive(false);
        }
        var fx = new GameObject("WeaponStream"); fx.transform.SetParent(owner, false);
        beam = fx.AddComponent<LineRenderer>(); beam.sharedMaterial = effectMaterial;
        beam.positionCount = 2; beam.startWidth = 0.018f; beam.endWidth = 0.045f;
        beam.useWorldSpace = true; beam.enabled = false;
        wasLocked = Cursor.lockState == CursorLockMode.Locked;
    }
    private void FitModel(Transform model, float length)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = new Bounds(); bool first = true;
        foreach (var renderer in renderers)
        {
            var matrix = holder.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var b = renderer.localBounds;
            for (int k = 0; k < 8; k++)
            {
                Vector3 p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        }
        float scale = length / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
        model.localScale *= scale;
        model.localPosition = new Vector3(0, 0, 0.08f) - bounds.center * scale;
    }
    public void SelectTool(int index)
    {
        if (index == 4)
        {
            Selected = Tool.Hands; holder.Unequip(); ResetProcessing(); Changed?.Invoke(); return;
        }
        if (index < 0 || index >= models.Length || models[index] == null) return;
        Selected = (Tool)index; holder.Equip(models[index]); ResetProcessing(); Changed?.Invoke();
    }
    public void CycleMode()
    {
        if (Selected == Tool.Vacuum) Mode = (VacuumMode)(((int)Mode + 1) % 3);
        if (Selected == Tool.SeedGun) MetalMode = !MetalMode;
        ResetProcessing(); Changed?.Invoke();
    }
    private void Update()
    {
        bool locked = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
        if (beam != null) beam.enabled = false;
        if (!locked || (vitals != null && vitals.IsDead)) { ResetProcessing(); wasLocked = false; return; }
        var keyboard = Keyboard.current; var mouse = Mouse.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectTool(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectTool(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectTool(2);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectTool(3);
            if (keyboard.digit5Key.wasPressedThisFrame) SelectTool(4);
            if (keyboard.eKey.wasPressedThisFrame) Interact();
            if (keyboard.qKey.wasPressedThisFrame) CycleMode();
            if (keyboard.rKey.wasPressedThisFrame && Selected == Tool.SeedGun && MetalMode && Metal > 0)
            { Metal--; MetalAmmo += 5; Changed?.Invoke(); }
            if (keyboard.rKey.wasPressedThisFrame && Selected == Tool.Recycler)
            {
                if (!TryFertilize()) RecycleInventory();
            }
        }
        if (mouse != null && mouse.scroll.ReadValue().y != 0f)
            SelectTool(((int)Selected + (mouse.scroll.ReadValue().y > 0 ? 1 : 4)) % 5);
        // The click used to re-lock the cursor must not also fire.
        bool firing = wasLocked && mouse != null && mouse.leftButton.isPressed;
        wasLocked = locked;
        if (firing) Use(Time.deltaTime);
        else { ResetProcessing(); Energy = Mathf.Min(maximumEnergy, Energy + 8f * Time.deltaTime); }
        Changed?.Invoke();
    }
    public void Use(float deltaTime)
    {
        if (view == null || deltaTime <= 0f || (vitals != null && vitals.IsDead)) return;
        if (Selected == Tool.Hands) return;
        if (Selected == Tool.Scanner)
        {
            if (Time.time >= nextScan) { Scan(); nextScan = Time.time + 0.3f; }
            return;
        }
        if (Selected == Tool.SeedGun) { FireSeedGun(); return; }
        float cost = (Selected == Tool.Recycler ? 8f : 5f) * deltaTime;
        if (Energy < cost || Time.time < jamUntil) { ResetProcessing(); return; }
        if (Selected == Tool.Vacuum && Mode == VacuumMode.Water && Water < 10f * deltaTime) return;
        Energy -= cost;
        if (Selected == Tool.Vacuum && Mode == VacuumMode.Water)
        {
            Water = Mathf.Max(0, Water - 10f * deltaTime);
            if (dirtyWater && UnityEngine.Random.value < deltaTime * 0.08f) jamUntil = Time.time + 1.2f;
        }
        float range = Selected == Tool.Recycler ? 5f : Mode == VacuumMode.Water ? 12f : 8f;
        bool found = Aim(range, out var hit);
        ShowStream(found ? hit.point : view.transform.position + view.transform.forward * range);
        if (!found) { processing = null; progress = 0; return; }
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant != null && Selected == Tool.Vacuum && Mode == VacuumMode.Water)
        {
            plant.WaterPlant(deltaTime * 0.4f, !dirtyWater); processing = null; progress = 0f; return;
        }
        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        if (enemy != null)
        {
            processing = null; progress = 0f;
            float dps = DamageRate(Selected, Mode, enemy.species);
            DamageEnemy(enemy, dps * deltaTime);
            return;
        }
        var source = hit.collider.GetComponentInParent<WeaponWaterSource>();
        if (source != null && Selected == Tool.Vacuum && Mode == VacuumMode.Collect)
        {
            processing = null; progress = 0f;
            if (Water < 0.1f) dirtyWater = false;
            if (Water < maximumWater) dirtyWater |= !source.cleanWater;
            Water = Mathf.Min(maximumWater, Water + 35f * deltaTime); Changed?.Invoke(); return;
        }
        var resource = hit.collider.GetComponentInParent<RecyclableResource>();
        if (resource != null && (Selected == Tool.Recycler || (Selected == Tool.Vacuum && Mode == VacuumMode.Collect)))
        {
            if (processing != resource) { processing = resource; progress = 0f; }
            progress += deltaTime;
            if (progress >= (Selected == Tool.Recycler ? resource.processingSeconds : 0.4f) && resource.TryClaim())
            {
                if (Selected == Tool.Recycler) { Metal += resource.metal; Plastic += resource.plastic; }
                else { CollectedMetalScrap += resource.metal; CollectedPlasticScrap += resource.plastic; }
                ResetProcessing(); Changed?.Invoke();
            }
        }
        else { processing = null; progress = 0; }
    }
    public void RecycleInventory()
    {
        if (Selected != Tool.Recycler || Energy < 10f || (CollectedMetalScrap + CollectedPlasticScrap) == 0) return;
        Energy -= 10f; Metal += CollectedMetalScrap; Plastic += CollectedPlasticScrap;
        CollectedMetalScrap = CollectedPlasticScrap = 0; Changed?.Invoke();
    }
    public static float DamageRate(Tool tool, VacuumMode mode, EnemyBrain.Species species)
    {
        if (tool == Tool.Recycler) return species == EnemyBrain.Species.Slime ? 30f : 0f;
        if (tool != Tool.Vacuum) return 0f;
        if (mode == VacuumMode.Water) return species == EnemyBrain.Species.Flame ? 40f : 0f;
        if (mode == VacuumMode.Disperse) return species == EnemyBrain.Species.Smoke || species == EnemyBrain.Species.Acid ? 32f : 0f;
        return species == EnemyBrain.Species.Slime ? 15f : 0f;
    }
    public void HitWithMetal(EnemyBrain enemy) => DamageEnemy(enemy, enemy.species == EnemyBrain.Species.Tin ? 45f : 8f);
    private void DamageEnemy(EnemyBrain enemy, float amount)
    {
        if (amount <= 0 || enemy.Health <= 0) return;
        enemy.TakeDamage(amount);
        if (enemy.Health <= 0)
        {
            if (enemy.species == EnemyBrain.Species.Tin) Metal += 6;
            if (enemy.species == EnemyBrain.Species.Slime) Plastic += 3;
            Changed?.Invoke();
        }
    }
    private bool Aim(float range, out RaycastHit nearest)
    {
        nearest = default; float closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(view.transform.position, view.transform.forward, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(owner) || hit.distance >= closest) continue;
            closest = hit.distance; nearest = hit;
        }
        return closest < float.PositiveInfinity;
    }
    private void FireSeedGun()
    {
        if (Time.time < nextShot || Energy < 2f || (MetalMode ? MetalAmmo <= 0 : Seeds <= 0)) return;
        if (!MetalMode)
        {
            var seed = GameObject.CreatePrimitive(PrimitiveType.Sphere); seed.name = "Flying Seed";
            seed.transform.position = view.transform.position; seed.transform.localScale = Vector3.one * 0.05f;
            Destroy(seed.GetComponent<Collider>()); seed.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            Tint(seed.GetComponent<Renderer>(), new Color(0.3f, 0.65f, 0.15f));
            seed.AddComponent<PlayerSeedProjectile>().Launch(owner, this, view.transform.forward);
            Seeds--;
        }
        else
        {
            // Spawn at the camera ray to avoid firing through a wall next to the weapon barrel.
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "Player Metal Ball";
            ball.transform.position = view.transform.position; ball.transform.localScale = Vector3.one * 0.08f;
            Destroy(ball.GetComponent<Collider>()); ball.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            ball.AddComponent<PlayerMetalProjectile>().Launch(owner, this, view.transform.forward); MetalAmmo--;
        }
        Energy -= 2f; nextShot = Time.time + (MetalMode ? 0.35f : 0.5f); Changed?.Invoke();
    }
    public bool TryPlant(RaycastHit hit)
    {
        if (hit.normal.y < 0.7f || (hit.collider is not TerrainCollider && hit.collider.GetComponentInParent<PlantingSurface>() == null))
        { Notify("Tohum için düz bir toprak yüzeyi gerekiyor."); return false; }
        foreach (var nearby in Physics.OverlapSphere(hit.point + Vector3.up * 0.1f, 0.45f))
            if (nearby.GetComponentInParent<PlantedSeed>() != null) { Notify("Bitkiler arasında biraz boşluk bırak."); return false; }
        var plant = GameObject.CreatePrimitive(PrimitiveType.Capsule); plant.name = "Eco Crop";
        plant.transform.position = hit.point + Vector3.up * 0.1f;
        plant.transform.localScale = new Vector3(0.08f, 0.1f, 0.08f);
        plant.GetComponent<Renderer>().sharedMaterial = effectMaterial;
        plant.AddComponent<PlantedSeed>();
        Notify("Tohum ekildi. Vakumun su moduyla sula."); return true;
    }
    public void Interact()
    {
        if (vitals != null && vitals.IsDead) return;
        if (!Aim(3f, out var hit)) return;
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant == null) return;
        if (plant.TryHarvest(out int seeds, out int food))
        { Seeds += seeds; Food += food; Notify("Hasat: +" + seeds + " tohum, +" + food + " ürün"); Changed?.Invoke(); }
        else if (Selected == Tool.Recycler && plant.TryCompost())
        { Compost++; Notify("Kuruyan bitki gübreye dönüştürüldü."); Changed?.Invoke(); }
    }
    private bool TryFertilize()
    {
        if (Compost <= 0 || !Aim(3f, out var hit)) return false;
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant == null || !plant.Fertilize()) return false;
        Compost--; Notify("Gübre uygulandı: büyüme hızlandı."); Changed?.Invoke(); return true;
    }
    public string ContextHint()
    {
        if (view == null || !Aim(12f, out var hit)) return "";
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant != null)
            return plant.Status + " • Su %" + Mathf.RoundToInt(plant.moisture * 100) + " • Büyüme %" + Mathf.RoundToInt(plant.growth * 100)
                + (hit.distance <= 3f && plant.IsMature ? "\n[E] Hasat et" : "")
                + (hit.distance <= 3f && plant.IsWithered && Selected == Tool.Recycler ? "\n[E] Gübreye dönüştür" : "")
                + (hit.distance <= 3f && !plant.IsWithered && !plant.IsMature && Selected == Tool.Recycler && Compost > 0 ? "\n[R] Gübrele" : "");
        if (hit.collider.GetComponentInParent<WeaponWaterSource>() != null) return "Su kaynağı • Vakum / Toplama ile doldur";
        if (hit.collider.GetComponentInParent<RecyclableResource>() != null) return "Atık • Vakumla topla veya geri dönüştür";
        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        return enemy == null ? "" : enemy.species + " • Can " + Mathf.CeilToInt(enemy.Health);
    }
    private void Notify(string text) { Feedback = text; FeedbackUntil = Time.time + 3f; }
    private void Scan()
    {
        if (!Aim(40f, out var hit)) LastScan = "Hedef yok";
        else
        {
            var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
            var water = hit.collider.GetComponentInParent<WeaponWaterSource>();
            var scrap = hit.collider.GetComponentInParent<RecyclableResource>();
            var plant = hit.collider.GetComponentInParent<PlantedSeed>();
            LastScan = plant != null ? ContextHint() : enemy != null ? enemy.species + " | Can: " + Mathf.CeilToInt(enemy.Health) + " | " + enemy.State
                : water != null ? (water.cleanWater ? "Temiz su" : "Kirli su")
                : scrap != null ? "Geri donusturulebilir: " + scrap.metal + " metal, " + scrap.plastic + " plastik"
                : hit.collider.name;
        }
        Scanned?.Invoke(LastScan); Changed?.Invoke();
    }
    private void ShowStream(Vector3 target)
    {
        if (beam == null) return;
        beam.enabled = true;
        beam.SetPosition(0, holder.transform.position + view.transform.forward * 0.25f); beam.SetPosition(1, target);
        Color color = Selected == Tool.Recycler ? Color.green : Mode == VacuumMode.Water ? Color.cyan : Mode == VacuumMode.Disperse ? Color.white : Color.yellow;
        beam.startColor = beam.endColor = color;
        Tint(beam, color);
    }
    private void Tint(Renderer renderer, Color color)
    {
        tint.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(tint);
    }
    private void ResetProcessing() { processing = null; progress = 0; if (beam != null) beam.enabled = false; }
    private void OnDisable() => ResetProcessing();
}
