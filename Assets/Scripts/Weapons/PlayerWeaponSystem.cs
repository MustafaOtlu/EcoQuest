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
    [field: SerializeField] public int CollectedOrganicWaste { get; private set; }
    [field: SerializeField] public int ProcessedOrganicRemainder { get; private set; }
    public EcoProgression Progression { get; private set; }
    public EcoBuildController Builder { get; private set; }
    [field: SerializeField] public string LastScan { get; private set; } = "";
    [field: SerializeField] public int Food { get; private set; }
    [field: SerializeField] public int Compost { get; private set; }
    public string Feedback { get; private set; }
    public float FeedbackUntil { get; private set; }
    public event Action Changed;
    public event Action<string> Scanned;
    public EcoChargingStation ChargingStation { get; private set; }
    public bool CanOperate => vitals == null || !vitals.IsRecovering;
    public bool HasDirtyWater => dirtyWater && Water > 0;
    public float ReceiveWater(float amount, bool clean)
    {
        if (!CanOperate || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return 0;
        float received = Mathf.Min(amount, Mathf.Max(0, maximumWater - Water));
        if (received <= 0) return 0;
        if (Water < 0.001f) dirtyWater = false;
        dirtyWater |= !clean; Water += received; Changed?.Invoke(); return received;
    }
    public void BeginCharging(EcoChargingStation station)
    {
        if (!CanOperate || station == null) return;
        ChargingStation = station; ResetProcessing(); Notify("Şarj bağlantısı kuruldu. Ayrılmak için E veya silahı kullan.");
    }
    public void StopCharging()
    {
        var station = ChargingStation; ChargingStation = null;
        if (station != null && station.User == this) station.Stop();
    }
    public float ReceiveElectricity(float amount)
    {
        if (!CanOperate || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return 0f;
        float received = Mathf.Min(amount, Mathf.Max(0f, maximumEnergy - Energy));
        Energy += received; Changed?.Invoke(); return received;
    }
    public void ShowFeedback(string message) => Notify(message);
    private Transform[] models = new Transform[4];
    private Transform owner;
    private PlayerVitals vitals;
    private Camera view;
    private LineRenderer beam;
    private float nextShot, nextScan;
    private float beamUntil, nextHitFeedback;
    private RecyclableResource processing;
    private float progress;
    private bool dirtyWater;
    private float jamUntil;
    [SerializeField] private string sessionKey = "EcoQuest.Session";
    private MaterialPropertyBlock tint;

    private void Start()
    {
        tint = new MaterialPropertyBlock();
        owner = GetComponentInParent<PlayerController>().transform;
        vitals = owner.GetComponent<PlayerVitals>();
        Progression = owner.GetComponent<EcoProgression>();
        if (Progression == null) Progression = owner.gameObject.AddComponent<EcoProgression>();
        Builder = owner.GetComponent<EcoBuildController>();
        if (Builder == null) Builder = owner.gameObject.AddComponent<EcoBuildController>();
        Builder.weapons = this;
        view = Camera.main;
        if (view == null) view = owner.GetComponentInChildren<Camera>(true);
        if (view == null)
        {
            Debug.LogError("PlayerWeaponSystem: sahne kamerası bulunamadı; silah kullanılamıyor.");
            enabled = false;
            return;
        }
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
        beam.positionCount = 2; beam.startWidth = 0.035f; beam.endWidth = 0.065f;
        beam.numCapVertices = 4;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.useWorldSpace = true; beam.enabled = false;
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
        if (beam != null && Time.time >= beamUntil) beam.enabled = false;
        var mouse = Mouse.current;
        if (!locked || (vitals != null && vitals.IsRecovering)) { ResetProcessing(); return; }
        if (Builder != null && Builder.IsBuilding) { ResetProcessing(); return; }
        var keyboard = Keyboard.current;
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
            if (keyboard.f5Key.wasPressedThisFrame) SaveSession();
            if (keyboard.f9Key.wasPressedThisFrame) LoadSession();
        }
        if (mouse != null && mouse.scroll.ReadValue().y != 0f)
            SelectTool(((int)Selected + (mouse.scroll.ReadValue().y > 0 ? 1 : 4)) % 5);
        // A press/release within one input update is still a shot. Never retain clicks from menus or focus loss.
        bool firing = mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame);
        if (firing) Use(Time.deltaTime);
        else { processing = null; progress = 0f; }
        Changed?.Invoke();
    }
    public void Use(float deltaTime)
    {
        if (view == null || deltaTime <= 0f || (vitals != null && vitals.IsRecovering)) return;
        if (Builder != null && Builder.IsBuilding) return;
        if (Selected == Tool.Hands) return;
        StopCharging();
        if (Selected == Tool.Scanner)
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.3f;
                if (Energy < 1f) { Notify("Elektrik yetersiz. Şarj istasyonuna bağlan."); return; }
                Energy -= 1f; Scan();
            }
            return;
        }
        if (Selected == Tool.SeedGun) { FireSeedGun(); return; }
        float cost = (Selected == Tool.Recycler ? 8f : 5f) * deltaTime;
        if (Energy < cost || Time.time < jamUntil) { ResetProcessing(); Notify(Time.time < jamUntil ? "Silah tıkandı, kısa süre bekle." : "Elektrik yetersiz. Şarj istasyonuna bağlan."); return; }
        if (Selected == Tool.Vacuum && Mode == VacuumMode.Water && Water < 10f * deltaTime) { Notify("Su bitti. Toplama moduyla su kaynağından doldur."); return; }
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
            if (dps <= 0f) Notify("Bu mod etkisiz. Su: Alev • Anti-vakum: Duman/Asit • Demir top: Teneke");
            return;
        }
        var waterStore = hit.collider.GetComponentInParent<EcoWaterNode>();
        if (waterStore != null && Selected == Tool.Vacuum && Mode == VacuumMode.Collect)
        {
            processing = null; progress = 0;
            waterStore.FillWeapon(this, true, 35 * deltaTime); return;
        }
        var source = hit.collider.GetComponentInParent<WeaponWaterSource>();
        if (source != null && Selected == Tool.Vacuum && Mode == VacuumMode.Collect)
        {
            processing = null; progress = 0f;
            ReceiveWater(35 * deltaTime, source.cleanWater); return;
        }
        var resource = hit.collider.GetComponentInParent<RecyclableResource>();
        if (resource != null && (Selected == Tool.Recycler || (Selected == Tool.Vacuum && Mode == VacuumMode.Collect)))
        {
            if (processing != resource) { processing = resource; progress = 0f; }
            progress += deltaTime;
            if (progress >= (Selected == Tool.Recycler ? resource.processingSeconds : 0.4f) && resource.TryClaim())
            {
                int recoveredMetal = Mathf.Max(0, resource.metal), recoveredPlastic = Mathf.Max(0, resource.plastic), recoveredOrganic = Mathf.Max(0, resource.organic);
                if (Selected == Tool.Recycler) AddProcessedResources(recoveredMetal, recoveredPlastic, recoveredOrganic);
                else { CollectedMetalScrap += recoveredMetal; CollectedPlasticScrap += recoveredPlastic; CollectedOrganicWaste += recoveredOrganic; }
                Notify(Selected == Tool.Recycler
                    ? "Geri dönüştürüldü • +" + recoveredMetal + " metal • +" + recoveredPlastic + " plastik • " + recoveredOrganic + " organik"
                    : "Ham kaynak toplandı • R ile işle");
                ResetProcessing(); Changed?.Invoke();
            }
        }
        else { processing = null; progress = 0; }
    }
    public void RecycleInventory()
    {
        if (!CanOperate || Selected != Tool.Recycler) return;
        int budget = Mathf.Min(8, Mathf.FloorToInt(Energy / 2f));
        int metal = Mathf.Min(CollectedMetalScrap, budget);
        int plastic = Mathf.Min(CollectedPlasticScrap, budget - metal);
        int organic = Mathf.Min(CollectedOrganicWaste, budget - metal - plastic);
        int total = metal + plastic + organic;
        if (total <= 0) { Notify(Energy < 2f ? "Elektrik yetersiz. Şarj istasyonuna bağlan." : "İşlenecek ham atık yok."); return; }
        StopCharging();
        Energy -= total * 2f; CollectedMetalScrap -= metal; CollectedPlasticScrap -= plastic; CollectedOrganicWaste -= organic;
        int reward = AddProcessedResources(metal, plastic, organic);
        Notify(total + " atık işlendi • +" + reward + " YEP" + (organic > 0 ? " • Organik birikim " + ProcessedOrganicRemainder + "/3" : ""));
        Changed?.Invoke();
    }
    private int AddProcessedResources(int metal, int plastic, int organic)
    {
        Metal += metal; Plastic += plastic;
        int organics = ProcessedOrganicRemainder + organic;
        Compost += organics / 3; ProcessedOrganicRemainder = organics % 3;
        return Progression.Award((metal + plastic + organic) * 2);
    }
    public void TakeRawResources(int capacity, out int metal, out int plastic, out int organic)
    {
        metal = plastic = organic = 0;
        if (!CanOperate || capacity <= 0) return;
        metal = Mathf.Min(CollectedMetalScrap, capacity); plastic = Mathf.Min(CollectedPlasticScrap, capacity - metal);
        organic = Mathf.Min(CollectedOrganicWaste, capacity - metal - plastic);
        CollectedMetalScrap -= metal; CollectedPlasticScrap -= plastic; CollectedOrganicWaste -= organic; Changed?.Invoke();
    }
    public void ReturnRawResources(int metal, int plastic, int organic)
    {
        if (metal < 0 || plastic < 0 || organic < 0) return;
        CollectedMetalScrap += metal; CollectedPlasticScrap += plastic; CollectedOrganicWaste += organic; Changed?.Invoke();
    }
    public void ReceiveProcessedResources(int metal, int plastic, int organic)
    {
        if (metal < 0 || plastic < 0 || organic < 0) return;
        AddProcessedResources(metal, plastic, organic); Changed?.Invoke();
    }
    public bool TrySpendMaterials(int metal, int plastic, int compost = 0)
    {
        if (!CanOperate || metal < 0 || plastic < 0 || compost < 0 || Metal < metal || Plastic < plastic || Compost < compost) return false;
        Metal -= metal; Plastic -= plastic; Compost -= compost; Changed?.Invoke(); return true;
    }
    public void ReturnMaterials(int metal, int plastic, int compost = 0)
    {
        if (metal < 0 || plastic < 0 || compost < 0) return;
        Metal += metal; Plastic += plastic; Compost += compost; Changed?.Invoke();
    }
    public static float DamageRate(Tool tool, VacuumMode mode, EnemyBrain.Species species)
    {
        if (tool == Tool.Recycler) return species == EnemyBrain.Species.Slime ? 30f : 0f;
        if (tool != Tool.Vacuum) return 0f;
        if (mode == VacuumMode.Water) return species == EnemyBrain.Species.Flame ? 40f : 0f;
        if (mode == VacuumMode.Disperse) return species == EnemyBrain.Species.Smoke || species == EnemyBrain.Species.Acid ? 32f : 0f;
        return species == EnemyBrain.Species.Slime ? 15f : 0f;
    }
    public void HitWithMetal(EnemyBrain enemy)
    {
        if (enemy == null) return;
        DamageEnemy(enemy, enemy.species == EnemyBrain.Species.Tin ? 45f : 8f);
        enemy.ApplyKnockback(enemy.transform.position - owner.position, 1.15f);
        Notify("İsabet • " + enemy.species + " savruldu");
    }
    private void DamageEnemy(EnemyBrain enemy, float amount)
    {
        if (amount <= 0 || enemy.Health <= 0) return;
        enemy.TakeDamage(amount);
        if (Time.time >= nextHitFeedback)
        { Notify("İsabet • " + enemy.species + " • Can " + Mathf.CeilToInt(enemy.Health)); nextHitFeedback = Time.time + 0.2f; }
        if (enemy.Health <= 0)
        {
            Progression.Award(enemy.species == EnemyBrain.Species.Tin ? 12 : 8);
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
        if (Time.time < nextShot) return;
        if (Energy < 2f) { Notify("Elektrik yetersiz. Şarj istasyonuna bağlan."); return; }
        if (MetalMode ? MetalAmmo <= 0 : Seeds <= 0)
        { Notify(MetalMode ? "Demir top bitti. Metal varsa R ile üret." : "Tohum bitti. Olgun bitkileri hasat et."); return; }
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
        ShowStream(Aim(35f, out var shotHit) ? shotHit.point : view.transform.position + view.transform.forward * 35f);
        Color shotColor = MetalMode ? new Color(1f, 0.75f, 0.2f) : new Color(0.45f, 1f, 0.2f);
        beam.startColor = beam.endColor = shotColor; Tint(beam, shotColor);
        Energy -= 2f; nextShot = Time.time + (MetalMode ? 0.35f : 0.5f); Changed?.Invoke();
    }
    public bool TryPlant(RaycastHit hit)
    {
        if (hit.normal.y < 0.7f || (hit.collider is not TerrainCollider && hit.collider.GetComponentInParent<PlantingSurface>() == null))
        { Notify("Tohum için düz bir toprak yüzeyi gerekiyor."); return false; }
        foreach (var nearby in Physics.OverlapSphere(hit.point + Vector3.up * 0.1f, 0.45f))
            if (nearby.GetComponentInParent<PlantedSeed>() != null) { Notify("Bitkiler arasında biraz boşluk bırak."); return false; }
        if (PlantedSeed.Create(hit.point + Vector3.up * 0.01f) == null) return false;
        Progression.Award(4);
        Notify("Tohum ekildi. Vakumun su moduyla sula."); return true;
    }
    public void Interact()
    {
        if (vitals != null && vitals.IsRecovering) return;
        if (!Aim(3f, out var hit)) return;
        var charger = hit.collider.GetComponentInParent<EcoChargingStation>();
        if (charger != null) { charger.Toggle(this); return; }
        var recycling = hit.collider.GetComponentInParent<EcoRecyclingFacility>();
        if (recycling != null) { recycling.Interact(this); return; }
        var farm = hit.collider.GetComponentInParent<EcoFarm>();
        if (farm != null) { Notify("Tohum silahıyla tarlaya ekim yap. Temiz su hattı otomatik sular."); return; }
        var waterStore = hit.collider.GetComponentInParent<EcoWaterNode>();
        if (waterStore != null)
        {
            float filled = waterStore.FillWeapon(this);
            if (filled > 0) Notify(filled.ToString("0") + " temiz su dolduruldu.");
            else if (Water >= maximumWater) Notify("Silahın su deposu dolu.");
            return;
        }
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant == null) return;
        if (plant.TryHarvest(out int seeds, out int food))
        { Seeds += seeds; Food += food; Progression.Award(food > 0 ? 6 : 2); Notify("Hasat: +" + seeds + " tohum, +" + food + " ürün"); Changed?.Invoke(); }
        else if (Selected == Tool.Recycler && Energy >= 2f && plant.TryCompost())
        { StopCharging(); Energy -= 2f; Compost++; Progression.Award(2); Notify("Kuruyan bitki gübreye dönüştürüldü."); Changed?.Invoke(); }
    }
    private bool TryFertilize()
    {
        if (Compost <= 0 || Energy < 2f || !Aim(3f, out var hit)) return false;
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant == null || !plant.Fertilize()) return false;
        StopCharging(); Compost--; Energy -= 2f; Notify("Gübre uygulandı: büyüme hızlandı."); Changed?.Invoke(); return true;
    }
    public string ContextHint()
    {
        if (view == null || !Aim(12f, out var hit)) return "";
        var recycling = hit.collider.GetComponentInParent<EcoRecyclingFacility>();
        if (recycling != null) return recycling.Describe();
        var farm = hit.collider.GetComponentInParent<EcoFarm>();
        if (farm != null) return farm.Describe();
        var waterStore = hit.collider.GetComponentInParent<EcoWaterNode>();
        if (waterStore != null) return waterStore.Describe();
        var power = hit.collider.GetComponentInParent<EcoPowerNode>();
        if (power != null)
            return power.Describe() + (power.kind == EcoPowerNode.Kind.Charger && hit.distance <= 3f
                ? (ChargingStation != null ? "\n[E] Şarjı bırak" : "\n[E] Silahı şarj et") : "");
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
            var power = hit.collider.GetComponentInParent<EcoPowerNode>();
            var waterStore = hit.collider.GetComponentInParent<EcoWaterNode>();
            var recycling = hit.collider.GetComponentInParent<EcoRecyclingFacility>();
            var farm = hit.collider.GetComponentInParent<EcoFarm>();
            LastScan = recycling != null ? recycling.Describe() : farm != null ? farm.Describe() : waterStore != null ? waterStore.Describe() : power != null ? power.Describe() : plant != null ? ContextHint() : enemy != null ? enemy.species + " | Can: " + Mathf.CeilToInt(enemy.Health) + " | " + enemy.State
                : water != null ? (water.cleanWater ? "Temiz su" : "Kirli su")
                : scrap != null ? "Geri dönüştürülebilir: " + scrap.metal + " metal, " + scrap.plastic + " plastik, " + scrap.organic + " organik"
                : hit.collider.name;
        }
        Scanned?.Invoke(LastScan); Changed?.Invoke();
    }
    private void ShowStream(Vector3 target)
    {
        if (beam == null) return;
        beam.enabled = true; beamUntil = Time.time + 0.12f;
        beam.SetPosition(0, holder.transform.position + view.transform.forward * 0.25f); beam.SetPosition(1, target);
        Color color = Selected == Tool.Recycler ? Color.green : Mode == VacuumMode.Water ? Color.cyan : Mode == VacuumMode.Disperse ? Color.white : Color.yellow;
        beam.startColor = beam.endColor = color;
        Tint(beam, color);
    }
    private void Tint(Renderer renderer, Color color)
    {
        tint.SetColor("_BaseColor", color);
        tint.SetColor("_Color", color);
        renderer.SetPropertyBlock(tint);
    }
    private void ResetProcessing() { processing = null; progress = 0; if (beam != null) beam.enabled = false; }
    private void OnDisable() { ResetProcessing(); StopCharging(); }

    [Serializable]
    private sealed class SessionData
    {
        public float energy, water;
        public bool dirtyWater;
        public int metalAmmo, seeds, metal, plastic, rawMetal, rawPlastic, food, compost;
        public int organic, organicRemainder, yep;
    }

    public void SaveSession()
    {
        var data = new SessionData
        {
            energy = Energy, water = Water, metalAmmo = MetalAmmo, seeds = Seeds,
            metal = Metal, plastic = Plastic, rawMetal = CollectedMetalScrap,
            rawPlastic = CollectedPlasticScrap, food = Food, compost = Compost, dirtyWater = dirtyWater,
            organic = CollectedOrganicWaste, organicRemainder = ProcessedOrganicRemainder, yep = Progression.Experience
        };
        PlayerPrefs.SetString(sessionKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Notify("Oturum kaydedildi (F5).");
    }

    public void LoadSession()
    {
        StopCharging();
        if (!PlayerPrefs.HasKey(sessionKey)) { Notify("Kayıt bulunamadı."); return; }
        SessionData data;
        try { data = JsonUtility.FromJson<SessionData>(PlayerPrefs.GetString(sessionKey)); }
        catch (ArgumentException) { Notify("Kayıt okunamadı."); return; }
        if (data == null) { Notify("Kayıt okunamadı."); return; }
        Energy = Mathf.Clamp(data.energy, 0f, maximumEnergy); Water = Mathf.Clamp(data.water, 0f, maximumWater);
        MetalAmmo = Mathf.Max(0, data.metalAmmo); Seeds = Mathf.Max(0, data.seeds);
        Metal = Mathf.Max(0, data.metal); Plastic = Mathf.Max(0, data.plastic);
        CollectedMetalScrap = Mathf.Max(0, data.rawMetal); CollectedPlasticScrap = Mathf.Max(0, data.rawPlastic);
        Food = Mathf.Max(0, data.food); Compost = Mathf.Max(0, data.compost);
        CollectedOrganicWaste = Mathf.Max(0, data.organic); ProcessedOrganicRemainder = Mathf.Clamp(data.organicRemainder, 0, 2);
        Progression.Restore(data.yep);
        dirtyWater = data.dirtyWater;
        ResetProcessing();
        Notify("Oturum yüklendi (F9)."); Changed?.Invoke();
    }
}
