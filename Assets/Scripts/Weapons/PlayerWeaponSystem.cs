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
    [SerializeField] private int[] otherSeeds = new int[3];
    [field: SerializeField] public EcoCropCatalog.Kind SelectedSeed { get; private set; }
    public int TotalSeeds => Seeds + otherSeeds[0] + otherSeeds[1] + otherSeeds[2];
    public int SeedCount(EcoCropCatalog.Kind kind) => !EcoCropCatalog.Valid(kind) ? 0 : kind == EcoCropCatalog.Kind.Cabbage ? Seeds : otherSeeds[(int)kind - 1];
    public void AddSeeds(EcoCropCatalog.Kind kind, int amount)
    { if (kind == EcoCropCatalog.Kind.Cabbage) Seeds += amount; else otherSeeds[(int)kind - 1] += amount; }
    public void CycleSeed()
    {
        for (int i = 1; i <= EcoCropCatalog.Count; i++)
        {
            var next = (EcoCropCatalog.Kind)(((int)SelectedSeed + i) % EcoCropCatalog.Count);
            if (!EcoDiscoveries.CanPlant(next)) continue;
            SelectedSeed = next; Notify(EcoCropCatalog.Get(next).Name + " • İklim " + EcoCropCatalog.Get(next).Climate); Changed?.Invoke(); return;
        }
    }
    [field: SerializeField] public int Metal { get; private set; }
    [field: SerializeField] public int Plastic { get; private set; }
    [field: SerializeField] public int CollectedMetalScrap { get; private set; }
    [field: SerializeField] public int CollectedPlasticScrap { get; private set; }
    [field: SerializeField] public int CollectedOrganicWaste { get; private set; }
    [field: SerializeField] public int ProcessedOrganicRemainder { get; private set; }
    public EcoProgression Progression { get; private set; }
    public EcoBuildController Builder { get; private set; }
    public EcoEquipmentUpgrades Upgrades { get; private set; }
    [field: SerializeField] public int SmallFilters { get; private set; }
    [field: SerializeField] public int LargeFilters { get; private set; }
    private float baseWaterCapacity;
    public void ApplyEquipmentCapacity()
    {
        if (baseWaterCapacity > 0 && Upgrades != null)
            maximumWater = baseWaterCapacity + 50 * Upgrades.Rank(EcoEquipmentUpgrades.Upgrade.WaterTank);
        Changed?.Invoke();
    }
    [field: SerializeField] public string LastScan { get; private set; } = "";
    [field: SerializeField] public int Food { get; private set; }
    [SerializeField] private int[] cleanProduce = new int[3];
    public int CountProduce(EcoCropCatalog.Kind kind) => (int)kind >= 0 && (int)kind < 3 ? cleanProduce[(int)kind] : 0;
    public bool HasCareFood => Food >= 2 || CleanFish >= 1;
    public bool HasFinaleTools => holder != null && modelPrefabs.Length > 1 && modelPrefabs[1] != null;
    public bool TrySpendElectricity(float amount)
    { if (!CanOperate || !EcoRegion.Valid(amount) || Energy < amount) return false; Energy -= amount; Changed?.Invoke(); return true; }
    public bool TryTakeCleanWater(float amount)
    { if (!CanOperate || !EcoRegion.Valid(amount) || HasDirtyWater || Water < amount) return false; Water -= amount; Changed?.Invoke(); return true; }
    public bool TrySpendMetalAmmo(int amount)
    { if (!CanOperate || amount <= 0 || MetalAmmo < amount) return false; MetalAmmo -= amount; Changed?.Invoke(); return true; }
    public bool TryTakeProduce(EcoCropCatalog.Kind kind, int amount)
    { if (!CanOperate || amount <= 0 || CountProduce(kind) < amount) return false; cleanProduce[(int)kind] -= amount; Food -= amount; Changed?.Invoke(); return true; }
    public bool TryTakeCleanProduce(int amount)
    {
        if (!CanOperate || amount <= 0 || Food < amount) return false;
        int remaining = amount;
        for (int i = 0; i < 3; i++) { int taken = Mathf.Min(remaining, cleanProduce[i]); cleanProduce[i] -= taken; remaining -= taken; }
        Food -= amount; Changed?.Invoke(); return true;
    }
    public bool TryTakeCleanFish(int amount)
    { if (!CanOperate || amount <= 0 || CleanFish < amount) return false; CleanFish -= amount; Changed?.Invoke(); return true; }
    public bool TryConsumeCareFood() => Food >= 2 ? TryTakeCleanProduce(2) : TryTakeCleanFish(1);
    public void EmergencySupplies()
    { if (TotalSeeds == 0 && Food == 0) AddSeeds(EcoCropCatalog.Kind.Cabbage, 1); if (Water < 2) { dirtyWater = false; Water = 2; } Changed?.Invoke(); }
    public void HandSort()
    {
        TakeRawResources(1, out int m, out int p, out int o);
        if (m + p + o > 0) ReceiveProcessedResources(m, p, o);
    }
    [field: SerializeField] public int CleanFish { get; private set; }
    [field: SerializeField] public int DirtyFish { get; private set; }
    public bool ReceiveFish(bool clean)
    {
        if (!CanOperate || CleanFish + DirtyFish >= 20) return false;
        if (clean) CleanFish++; else DirtyFish++;
        // The catch message first, so a field milestone granted by the same catch (SAHA BAŞARISI +20 YEP) stays visible.
        Notify(clean ? "Temiz balık yakalandı." : "Kirli balık yakalandı • Yemek olarak kullanma.");
        if (clean) EcoDiscoveries.NotifyCleanFish(EcoRegion.At(owner.position));
        Changed?.Invoke(); return true;
    }
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
    public void SpendFraction(float fraction) { Energy = Mathf.Max(0f, Energy - Energy * Mathf.Clamp01(fraction)); Changed?.Invoke(); }
    private Transform[] models = new Transform[4];
    private Transform owner;
    private PlayerVitals vitals;
    private Camera view;
    private int aimFrame = -1;
    private Vector3 aimDirection = Vector3.forward;
    // Isometric aiming: shots start at the character's chest and travel toward the point under the cursor.
    private Vector3 AimOrigin => owner != null ? owner.position + Vector3.up * 1.15f : transform.position;
    private Vector3 AimDir { get { RefreshAim(); return aimDirection; } }
    private Vector3 AimRight
    {
        get
        {
            Vector3 right = Vector3.Cross(Vector3.up, AimDir);
            return right.sqrMagnitude > 0.0001f ? right.normalized : (owner != null ? owner.right : Vector3.right);
        }
    }
    private void RefreshAim()
    {
        if (aimFrame == Time.frameCount) return;
        aimFrame = Time.frameCount;
        Vector3 direction = owner != null ? owner.forward : Vector3.forward;
        if (owner != null && EcoIsoCamera.TryCursorPoint(owner, out var point))
        {
            Vector3 delta = point - AimOrigin;
            if (delta.sqrMagnitude > 0.04f) direction = delta.normalized;
        }
        aimDirection = direction;
    }
    private WeaponVisualEffects streamEffects;
    private float nextShot, nextScan;
    private float nextHitFeedback;
    private RecyclableResource processing;
    private float progress;
    private bool dirtyWater;
    private float jamUntil, clog;
    [SerializeField] private string sessionKey = "EcoQuest.Session";

    private void Start()
    {
        owner = GetComponentInParent<PlayerController>().transform;
        vitals = owner.GetComponent<PlayerVitals>();
        Progression = owner.GetComponent<EcoProgression>();
        if (Progression == null) Progression = owner.gameObject.AddComponent<EcoProgression>();
        Builder = owner.GetComponent<EcoBuildController>();
        if (Builder == null) Builder = owner.gameObject.AddComponent<EcoBuildController>();
        Builder.weapons = this;
        baseWaterCapacity = maximumWater;
        Upgrades = owner.GetComponent<EcoEquipmentUpgrades>();
        if (Upgrades == null) Upgrades = owner.gameObject.AddComponent<EcoEquipmentUpgrades>();
        Upgrades.weapons = this;
        ApplyEquipmentCapacity();
        view = Camera.main;
        if (view == null) view = owner.GetComponentInChildren<Camera>(true);
        if (view == null)
        {
            Debug.LogError("PlayerWeaponSystem: sahne kamerası bulunamadı; silah kullanılamıyor.");
            enabled = false;
            return;
        }
        maximumWater = 50f; baseWaterCapacity = maximumWater; ApplyEquipmentCapacity();
        Energy = maximumEnergy; Water = 20f; Seeds = 5;
        Metal = 50; Plastic = 40;
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
        var fx = new GameObject("Weapon particles"); fx.transform.SetParent(transform, false);
        streamEffects = fx.AddComponent<WeaponVisualEffects>();
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
        bool locked = Application.isFocused && !PlayerController.Paused;
        var mouse = Mouse.current;
        if (!locked || EcoUIFlow.InputBlocked || EcoMarketUI.BlocksGameplay || (vitals != null && vitals.IsRecovering)) { ResetProcessing(); return; }
        if (Builder != null && Builder.IsBuilding) { ResetProcessing(); return; }
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectTool(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectTool(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectTool(2);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectTool(3);
            if (keyboard.digit5Key.wasPressedThisFrame) SelectTool(4);
            if (EcoKeys.Pressed(EcoKeys.Action.Interact)) Interact();
            if (EcoMarketUI.IsOpen) return;
            if (EcoKeys.Pressed(EcoKeys.Action.Mode)) CycleMode();
            if (EcoKeys.Pressed(EcoKeys.Action.Rotate) && Selected == Tool.SeedGun && !MetalMode) CycleSeed();
            if (EcoKeys.Pressed(EcoKeys.Action.Rotate) && Selected == Tool.Vacuum && HasDirtyWater) { clog = 0; jamUntil = Time.time + 2f; Water = Mathf.Max(0, Water - 2f); Notify("Kirli su hattı temizleniyor…"); }
            if (EcoKeys.Pressed(EcoKeys.Action.Rotate) && Selected == Tool.Recycler)
            {
                if (!TryFertilize()) RecycleInventory();
            }
            // World saves own F5/F9; inventory is captured in the same atomic snapshot.
        }
        // Mouse wheel zooms the isometric camera (GDD §3); it no longer cycles tools.
        // A press/release within one input update is still a shot. Never retain clicks from menus or focus loss.
        bool firing = mouse != null && (EcoKeys.FireHeld(mouse) || mouse.leftButton.wasPressedThisFrame) && !EcoIsoCamera.PointerOverUI();
        if (firing) Use(Time.deltaTime);
        else { processing = null; progress = 0f; if (streamEffects != null) streamEffects.Stop(false); }
        Changed?.Invoke();
    }
    public void Use(float deltaTime)
    {
        if (view == null || deltaTime <= 0f || EcoUIFlow.InputBlocked || EcoMarketUI.BlocksGameplay || (vitals != null && vitals.IsRecovering)) return;
        if (Builder != null && Builder.IsBuilding) return;
        if (Selected == Tool.Hands) return;
        StopCharging();
        if (Selected == Tool.Scanner)
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.3f;
                Scan();
            }
            return;
        }
        if (Selected == Tool.SeedGun) { FireSeedGun(); return; }
        float cost = (Selected == Tool.Recycler ? 1f * Upgrades.RecyclerElectricity : (Mode == VacuumMode.Disperse ? 3f : Mode == VacuumMode.Water ? 1f : 2f) * Upgrades.VacuumElectricity) * deltaTime;
        if (Energy < cost || Time.time < jamUntil) { ResetProcessing(); Notify(Time.time < jamUntil ? "Silah tıkandı, kısa süre bekle." : "Elektrik yetersiz. Şarj istasyonuna bağlan."); return; }
        if (Selected == Tool.Vacuum && Mode == VacuumMode.Water && Water < 2f * deltaTime) { Notify("Su bitti. Toplama moduyla su kaynağından doldur."); return; }
        Energy -= cost;
        if (Selected == Tool.Vacuum && Mode == VacuumMode.Water)
        {
            Water = Mathf.Max(0, Water - 2f * deltaTime);
            if (dirtyWater) { clog += deltaTime * 0.1f; if (clog >= 1f) jamUntil = float.PositiveInfinity; }
        }
        float range = Selected == Tool.Recycler ? 5f : (Mode == VacuumMode.Water ? 6f : 5f) + Upgrades.VacuumRangeBonus;
        bool found = Aim(range, out var hit, Selected == Tool.Vacuum && Mode == VacuumMode.Collect ? Upgrades.CollectionRadius : 0);
        // A spray is a cone, not a laser: if the ray meets nothing the spray acts on (ground, water surface, a wall), the water/anti-vacuum stream bends to the nearest
        // enemy this mode affects within range and 25° of the cursor direction.
        if (Selected == Tool.Vacuum && Mode != VacuumMode.Collect && (!found || !SprayActsOn(hit.collider)))
        {
            var assisted = SprayTarget(range);
            if (assisted != null)
            {
                Vector3 at = assisted.transform.position + Vector3.up * 0.7f;
                ShowStream(at, true, (AimOrigin - at).normalized, deltaTime);
                processing = null; progress = 0f;
                DamageEnemy(assisted, DamageRate(Selected, Mode, assisted.species) * deltaTime * Upgrades.VacuumStrength);
                return;
            }
        }
        ShowStream(found ? hit.point : AimOrigin + AimDir * range, found, found ? hit.normal : -AimDir, deltaTime);
        if (!found) { processing = null; progress = 0; return; }
        var focus = hit.collider.GetComponentInParent<EcoFinaleFocus>();
        if (focus != null && Selected == Tool.Vacuum)
        {
            if (Mode == VacuumMode.Water && !dirtyWater) focus.TryUseWater(2f * deltaTime);
            if (Mode == VacuumMode.Disperse) focus.TryUseAir(deltaTime);
            return;
        }
        var fire = hit.collider.GetComponentInParent<EcoFire>();
        if (fire != null && Selected == Tool.Vacuum && Mode == VacuumMode.Water)
        {
            fire.Extinguish(10 * deltaTime * Upgrades.VacuumStrength); processing = null; progress = 0;
            Notify(fire.Burning ? "Yangın söndürülüyor…" : "Yangın söndürüldü."); return;
        }
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant != null && Selected == Tool.Vacuum && Mode == VacuumMode.Water)
        {
            plant.WaterPlant(deltaTime * 0.4f * Upgrades.VacuumStrength, !dirtyWater); processing = null; progress = 0f; return;
        }
        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        if (enemy != null)
        {
            processing = null; progress = 0f;
            float dps = DamageRate(Selected, Mode, enemy.species);
            DamageEnemy(enemy, dps * deltaTime * (Selected == Tool.Vacuum ? Upgrades.VacuumStrength : 1));
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
            ReceiveWater(35 * deltaTime, source.IsClean); return;
        }
        var resource = hit.collider.GetComponentInParent<RecyclableResource>();
        if (resource != null && (Selected == Tool.Recycler || (Selected == Tool.Vacuum && Mode == VacuumMode.Collect)))
        {
            if (resource.requiredRecyclerRank > 0 && (Selected != Tool.Recycler || Upgrades.Rank(EcoEquipmentUpgrades.Upgrade.HeavyRecycling) < resource.requiredRecyclerRank))
            { ResetProcessing(); Notify("Büyük atık • Geri dönüşüm cihazında işleme kademesi " + resource.requiredRecyclerRank + " gerekiyor."); return; }
            if (processing != resource) { processing = resource; progress = 0f; }
            progress += deltaTime * (Selected == Tool.Recycler ? Upgrades.RecyclingSpeed : 1);
            if (progress >= (Selected == Tool.Recycler ? resource.processingSeconds : 0.4f) && resource.TryClaim())
            {
                int recoveredMetal = Mathf.Max(0, resource.metal), recoveredPlastic = Mathf.Max(0, resource.plastic), recoveredOrganic = Mathf.Max(0, resource.organic);
                EcoRegion.At(resource.transform.position)?.AddPollution(0, 0, -(recoveredMetal + recoveredPlastic) * 0.3f, 0);
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
        if (!CanOperate || EcoMarketUI.BlocksGameplay || Selected != Tool.Recycler) return;
        float unitCost = 2f * Upgrades.RecyclerElectricity;
        int budget = Mathf.Min(Upgrades.RecyclingBatch, Mathf.FloorToInt(Energy / unitCost));
        int metal = Mathf.Min(CollectedMetalScrap, budget);
        int plastic = Mathf.Min(CollectedPlasticScrap, budget - metal);
        int organic = Mathf.Min(CollectedOrganicWaste, budget - metal - plastic);
        int total = metal + plastic + organic;
        if (total <= 0) { Notify(Energy < unitCost ? "Elektrik yetersiz. Şarj istasyonuna bağlan." : "İşlenecek ham atık yok."); return; }
        StopCharging();
        Energy -= total * unitCost; CollectedMetalScrap -= metal; CollectedPlasticScrap -= plastic; CollectedOrganicWaste -= organic;
        int reward = AddProcessedResources(metal, plastic, organic);
        Notify(total + " atık işlendi" + (organic > 0 ? " • Organik birikim " + ProcessedOrganicRemainder + "/3" : ""));
        Changed?.Invoke();
    }
    private int AddProcessedResources(int metal, int plastic, int organic)
    {
        Metal += metal; Plastic += plastic;
        int organics = ProcessedOrganicRemainder + organic;
        Compost += organics / 3; ProcessedOrganicRemainder = organics % 3;
        return metal + plastic + organic;
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
    public bool CanMarketTrade(EcoMarket.Offer offer, out string reason)
    {
        reason = "İşlem kullanılamıyor";
        if ((int)offer < 0 || (int)offer >= EcoMarket.Titles.Length || !CanOperate || Upgrades == null) return false;
        if (EcoMarket.IsSeedOffer(offer))
        {
            var seed = offer == EcoMarket.Offer.CarrotSeeds ? EcoCropCatalog.Kind.Carrot : offer == EcoMarket.Offer.TomatoSeeds ? EcoCropCatalog.Kind.Tomato : offer == EcoMarket.Offer.TreeSeeds ? EcoCropCatalog.Kind.Tree : EcoCropCatalog.Kind.Cabbage;
            if (!EcoDiscoveries.CanPlant(seed)) { reason = "Önce ilgili bölgeyi tara"; return false; }
        }
        if (offer == EcoMarket.Offer.SellProduce) { reason = CountProduce(SelectedSeed) > 0 ? "Sat" : "Seçili türden temiz ürün yok"; return CountProduce(SelectedSeed) > 0; }
        if (offer == EcoMarket.Offer.SellFish) { reason = CleanFish > 0 ? "Sat" : "Temiz balık yok"; return CleanFish > 0; }
        if (offer == EcoMarket.Offer.SellSeeds) { reason = SeedCount(SelectedSeed) >= 5 ? "Sat" : "Seçili türden en az 5 tohum gerekli"; return SeedCount(SelectedSeed) >= 5; }
        if ((offer == EcoMarket.Offer.IronAmmo && MetalAmmo > Upgrades.AmmoCapacity - 5) || (EcoMarket.IsSeedOffer(offer) && TotalSeeds > Upgrades.SeedCapacity - 5)
            || (offer == EcoMarket.Offer.SmallFilter && SmallFilters >= 20) || (offer == EcoMarket.Offer.LargeFilter && LargeFilters >= 10))
        { reason = "Depoda yeterli yer yok"; return false; }
        if (Metal < EcoMarket.MetalPrice(offer) || Plastic < EcoMarket.PlasticPrice(offer)) { reason = "Malzeme yetersiz"; return false; }
        reason = "Satın al"; return true;
    }
    /// <summary>Hands over produce for an order reward (EcoOrders). Returns false when the produce is missing.</summary>
    public bool DeliverTypedOrder(int cropKind, bool fish, int amount, int metal, int plastic)
    {
        bool taken = fish ? TryTakeCleanFish(amount) : TryTakeProduce((EcoCropCatalog.Kind)cropKind, amount);
        if (!taken) return false;
        ReturnMaterials(metal, plastic); return true;
    }
    public bool DeliverOrder(int food, int metal, int plastic)
    {
        if (food < 0 || Food < food) return false;
        if (food > 0 && !TryTakeCleanProduce(food)) return false;
        Metal += Mathf.Max(0, metal); Plastic += Mathf.Max(0, plastic);
        Changed?.Invoke();
        return true;
    }
    internal bool ExecuteMarketTrade(EcoMarket.Offer offer)
    {
        if (!CanMarketTrade(offer, out string reason)) { Notify(reason); return false; }
        if (offer == EcoMarket.Offer.SellProduce)
        { var price = EcoMarket.ProduceValue(SelectedSeed); if (!TryTakeProduce(SelectedSeed, 1)) return false; Metal += price.x; Plastic += price.y; }
        else if (offer == EcoMarket.Offer.SellFish) { if (!TryTakeCleanFish(1)) return false; Metal += 4; Plastic += 2; }
        else if (offer == EcoMarket.Offer.SellSeeds) { AddSeeds(SelectedSeed, -5); Plastic++; }
        else
        {
            if (!TrySpendMaterials(EcoMarket.MetalPrice(offer), EcoMarket.PlasticPrice(offer))) return false;
            switch (offer)
            {
                case EcoMarket.Offer.IronAmmo: MetalAmmo += 5; break;
                case EcoMarket.Offer.Seeds: AddSeeds(EcoCropCatalog.Kind.Cabbage, 5); break;
                case EcoMarket.Offer.CarrotSeeds: AddSeeds(EcoCropCatalog.Kind.Carrot, 5); break;
                case EcoMarket.Offer.TomatoSeeds: AddSeeds(EcoCropCatalog.Kind.Tomato, 5); break;
                case EcoMarket.Offer.TreeSeeds: AddSeeds(EcoCropCatalog.Kind.Tree, 5); break;
                case EcoMarket.Offer.SmallFilter: SmallFilters++; break;
                case EcoMarket.Offer.LargeFilter: LargeFilters++; break;
            }
        }
        Notify(EcoMarket.Titles[(int)offer] + " • İşlem tamamlandı"); Changed?.Invoke(); return true;
    }
    public bool UseAirFilter(bool large)
    {
        if (!CanOperate || (large ? LargeFilters <= 0 : SmallFilters <= 0)) return false;
        if (large) LargeFilters--; else SmallFilters--; Changed?.Invoke(); return true;
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
        enemy.StunFromMetal();
        enemy.ApplyKnockback(enemy.transform.position - owner.position, 1.15f);
        Notify(enemy.IsStunned ? "Teneke sersemledi • Yaklaş ve " + EcoKeys.Tag(EcoKeys.Action.Interact) + " ile küçük filtre tak" : "İsabet • " + enemy.DisplayName + " savruldu");
    }
    private void DamageEnemy(EnemyBrain enemy, float amount)
    {
        if (amount <= 0 || enemy.Health <= 0) return;
        enemy.TakeDamage(amount);
        if (Time.time >= nextHitFeedback)
        { Notify("İsabet • " + enemy.DisplayName + " • Can " + Mathf.CeilToInt(enemy.Health)); nextHitFeedback = Time.time + 0.2f; }
        if (enemy.Health <= 0) Changed?.Invoke();
    }
    private static bool SprayActsOn(Collider c) => c.GetComponentInParent<EnemyBrain>() != null || c.GetComponentInParent<EcoFinaleFocus>() != null
        || c.GetComponentInParent<EcoFire>() != null || c.GetComponentInParent<PlantedSeed>() != null;
    private EnemyBrain SprayTarget(float range)
    {
        EnemyBrain best = null; float bestAngle = 25f;
        Vector3 flat = new Vector3(AimDir.x, 0f, AimDir.z);
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.State == EnemyBrain.Behaviour.Dead || DamageRate(Selected, Mode, enemy.species) <= 0f) continue;
            Vector3 to = enemy.transform.position - owner.position; to.y = 0f;
            if (to.magnitude > range) continue;
            float angle = to.magnitude < 2.5f ? 0f : Vector3.Angle(flat, to); // point-blank: inside the spray whatever the cursor
            if (angle <= bestAngle) { bestAngle = angle; best = enemy; }
        }
        return best;
    }
    private bool Aim(float range, out RaycastHit nearest, float radius = 0)
    {
        nearest = default; float closest = float.PositiveInfinity;
        var hits = radius > 0 ? Physics.SphereCastAll(AimOrigin, radius, AimDir, range, ~0, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(AimOrigin, AimDir, range, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(owner) || hit.distance >= closest) continue;
            closest = hit.distance; nearest = hit;
        }
        return closest < float.PositiveInfinity;
    }
    private void FireSeedGun()
    {
        if (Time.time < nextShot) return;
        float unitCost = (MetalMode ? 3f : 2f) * Upgrades.SeedElectricity;
        if (Energy < unitCost) { Notify("Elektrik yetersiz. Şarj istasyonuna bağlan."); return; }
        if (MetalMode ? MetalAmmo <= 0 : SeedCount(SelectedSeed) <= 0)
        { Notify(MetalMode ? "Demir top bitti. Marketten metal karşılığında al." : "Tohum bitti. Hasat yap veya markete uğra."); return; }
        int count = 1;
        if (!MetalMode)
        {
            bool aimed = Aim(35, out var ground);
            for (int i = 0; i < count; i++)
            {
            var seed = GameObject.CreatePrimitive(PrimitiveType.Sphere); seed.name = "Flying Seed";
            seed.transform.position = AimOrigin; seed.transform.localScale = new Vector3(0.075f, 0.075f, 0.105f);
            Destroy(seed.GetComponent<Collider>()); seed.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            float offset = i - (count - 1) * 0.5f;
            Vector3 direction = count == 1 ? AimDir : aimed
                ? (ground.point + AimRight * offset * 0.9f - AimOrigin).normalized
                : Quaternion.AngleAxis(offset * 8, Vector3.up) * AimDir;
            seed.AddComponent<PlayerSeedProjectile>().Launch(owner, this, direction);
            AddSeeds(SelectedSeed, -1);
            }
        }
        else
        {
            // Spawn at the camera ray to avoid firing through a wall next to the weapon barrel.
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "Player Metal Ball";
            ball.transform.position = AimOrigin; ball.transform.localScale = Vector3.one * 0.12f;
            Destroy(ball.GetComponent<Collider>()); ball.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            ball.AddComponent<PlayerMetalProjectile>().Launch(owner, this, AimDir); MetalAmmo--;
        }
        if (streamEffects != null) streamEffects.Shot(holder.transform.position + AimDir * 0.25f, AimDir, MetalMode);
        Energy -= unitCost * count; nextShot = Time.time + 0.5f; // GDD §11: at most two shots per second Changed?.Invoke();
    }
    public bool TryPlant(RaycastHit hit) => TryPlant(hit, SelectedSeed);
    public bool TryPlant(RaycastHit hit, EcoCropCatalog.Kind kind)
    {
        if (!EcoCropCatalog.Valid(kind) || !EcoDiscoveries.CanPlant(kind)) return false;
        if (hit.normal.y < 0.7f || (!EcoGround.IsSoil(hit.collider) && hit.collider.GetComponentInParent<PlantingSurface>() == null))
        {
            string surfaceName = EcoGround.SurfaceName(hit.collider);
            Notify(surfaceName.Length > 0 ? surfaceName : "Tohum için düz bir toprak yüzeyi gerekiyor."); return false;
        }
        bool tree = kind == EcoCropCatalog.Kind.Tree;
        var surface = hit.collider.GetComponentInParent<PlantingSurface>();
        if (tree && (hit.collider.GetComponentInParent<EcoFarm>() != null || (surface != null && !surface.allowTrees)))
        { Notify("Ağacı ekim yatağına değil, açık toprağa dik."); return false; }
        foreach (var nearby in Physics.OverlapSphere(hit.point + Vector3.up * 0.1f, tree ? 1.8f : 0.45f))
            if (nearby.GetComponentInParent<PlantedSeed>() != null || nearby.GetComponentInParent<EcoVegetation>() != null
                || (tree && nearby.GetComponentInParent<EcoStructure>() != null)) { Notify("Bitkiler ve yapılar arasında biraz boşluk bırak."); return false; }
        var planted = PlantedSeed.Create(hit.point + Vector3.up * 0.01f, kind);
        if (planted == null) return false;
        Notify("Tohum ekildi. Vakumun su moduyla sula.");
        EcoDiscoveries.NotifyPlanting(planted); // may grant a field milestone; its message must not be overwritten
        return true;
    }
    public void Interact()
    {
        if (EcoUIFlow.InputBlocked || EcoMarketUI.BlocksGameplay || (vitals != null && vitals.IsRecovering)) return;
        if (EcoFinale.Current != null && EcoFinale.Current.TryInteractAtTerminal(this)) return;
        if (EcoStory.Current != null && EcoStory.Current.TryInteract(this)) return;
        if (EcoGlacierStation.Current != null && EcoGlacierStation.Current.TryInteract(this)) return;
        if (EcoPollutionInlet.TryInteract(this)) return;
        // GDD §11: the 4 s stun window is for closing in, not for pixel aiming; E fits the filter on a stunned Tin nearby.
        foreach (var stunned in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            if (stunned.IsStunned && (stunned.transform.position - transform.position).sqrMagnitude <= 3.5f * 3.5f) { stunned.InstallFilter(this); return; }
        // At the bank with empty hands, E fishes without aiming down at the water.
        if (Selected == Tool.Hands)
            foreach (var spot in FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None))
                if (spot.Fisher == this || spot.CanFish(this)) { spot.Interact(this); return; }
        if (!Aim(3f, out var hit)) { NearbyMarket()?.Interact(this); return; }
        var tin = hit.collider.GetComponentInParent<EnemyBrain>();
        if (tin != null && tin.species == EnemyBrain.Species.Tin) { tin.InstallFilter(this); return; }
        var fishing = hit.collider.GetComponentInParent<EcoFishingSpot>();
        if (fishing != null) { fishing.Interact(this); return; }
        var factory = hit.collider.GetComponentInParent<EcoFactory>();
        if (factory != null) { if (!factory.Filtered) factory.InstallFilter(this); else factory.ConvertToCleanPower(this); return; }
        var market = hit.collider.GetComponentInParent<EcoMarket>();
        if (market != null) { market.Interact(this); return; }
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
        if (plant == null) { NearbyMarket()?.Interact(this); return; }
        if (plant.IsMature && TotalSeeds + 1 > Upgrades.SeedCapacity)
        { Notify("Tohum depon dolu. Marketten kapasite geliştir veya fazla tohumları sat."); return; }
        if (plant.TryHarvest(out int seeds, out int food))
        { AddSeeds(plant.kind, seeds); Food += food; Notify("Hasat: +" + seeds + " " + EcoCropCatalog.Get(plant.kind).Name + " tohumu, +" + food + " ürün"); if (food > 0) { cleanProduce[(int)plant.kind] += food; EcoDiscoveries.NotifyCleanHarvest(plant); } else CollectedOrganicWaste++; Changed?.Invoke(); }
        else if (Selected == Tool.Recycler && Energy >= 2f && plant.TryCompost())
        { StopCharging(); Energy -= 2f; Compost++; Notify("Kuruyan bitki gübreye dönüştürüldü."); Changed?.Invoke(); }
    }
    /// <summary>[E] next to the market opens it even when the cursor points elsewhere.</summary>
    private EcoMarket NearbyMarket()
    {
        foreach (var shop in FindObjectsByType<EcoMarket>(FindObjectsSortMode.None)) if (shop.CanReach(this)) return shop;
        return null;
    }
    private bool TryFertilize()
    {
        if (Compost <= 0 || Energy < 2f || !Aim(3f, out var hit)) return false;
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant == null || !plant.Fertilize()) return false;
        StopCharging(); Compost--; Energy -= 2f; Notify("Organik gübre: büyüme desteği ve 120 saniye böcek koruması."); Changed?.Invoke(); return true;
    }
    public string ContextHint()
    {
        // Things used by standing next to them get a prompt even when the cursor points elsewhere.
        string near = EcoStory.Current != null ? EcoStory.Current.Prompt(this) : "";
        if (near.Length > 0) return near;
        var shop = NearbyMarket();
        if (shop != null) return shop.Describe();
        if (view == null || !Aim(12f, out var hit)) return "";
        var fishing = hit.collider.GetComponentInParent<EcoFishingSpot>();
        if (fishing != null) return fishing.Describe();
        var factory = hit.collider.GetComponentInParent<EcoFactory>();
        if (factory != null) return factory.Describe();
        var fire = hit.collider.GetComponentInParent<EcoFire>();
        if (fire != null) return fire.Describe();
        var market = hit.collider.GetComponentInParent<EcoMarket>();
        if (market != null) return market.Describe();
        var recycling = hit.collider.GetComponentInParent<EcoRecyclingFacility>();
        if (recycling != null) return recycling.Describe();
        var farm = hit.collider.GetComponentInParent<EcoFarm>();
        if (farm != null) return farm.Describe();
        var waterStore = hit.collider.GetComponentInParent<EcoWaterNode>();
        if (waterStore != null) return waterStore.Describe();
        var power = hit.collider.GetComponentInParent<EcoPowerNode>();
        if (power != null)
            return power.Describe() + (power.kind == EcoPowerNode.Kind.Charger && hit.distance <= 3f
                ? (ChargingStation != null ? "\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Şarjı bırak" : "\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Silahı şarj et") : "");
        var plant = hit.collider.GetComponentInParent<PlantedSeed>();
        if (plant != null)
            return plant.Status + " • Su %" + Mathf.RoundToInt(plant.moisture * 100) + " • Büyüme %" + Mathf.RoundToInt(plant.growth * 100)
                + (hit.distance <= 3f && plant.IsMature ? "\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Hasat et" : "")
                + (hit.distance <= 3f && plant.IsWithered && Selected == Tool.Recycler ? "\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Gübreye dönüştür" : "")
                + (hit.distance <= 3f && !plant.IsWithered && !plant.IsMature && Selected == Tool.Recycler && Compost > 0 ? "\n" + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Gübrele" : "");
        if (hit.collider.GetComponentInParent<WeaponWaterSource>() != null) return "Su kaynağı • Vakum / Toplama ile doldur";
        var waste = hit.collider.GetComponentInParent<RecyclableResource>();
        if (waste != null) return waste.requiredRecyclerRank > 0 ? "Büyük atık • Geri dönüşüm işleme kademesi " + waste.requiredRecyclerRank : "Atık • Vakumla topla veya geri dönüştür";
        var tree = hit.collider.GetComponentInParent<EcoVegetation>();
        if (tree != null) return "Ağaç • Sağlık %" + Mathf.RoundToInt(tree.health * 100) + "\nHavayı ve toprağı iyileştirir, canlılara yaşam alanı sağlar";
        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        return enemy == null ? "" : enemy.IsStunned ? "Teneke • Sersemleme " + enemy.StunRemaining.ToString("0.0") + " sn\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Küçük hava filtresi tak"
            : enemy.DisplayName + " • Can " + Mathf.CeilToInt(enemy.Health);
    }
    private void Notify(string text) { Feedback = text; FeedbackUntil = Time.time + 3f; }
    private void Scan()
    {
        EcoDiscoveries.DiscoverRegion(EcoRegion.At(owner.position));
        if (!Aim(40f, out var hit)) LastScan = EcoRegion.At(owner.position)?.Describe() ?? "Bölge dışında • Hedef yok";
        else
        {
            EcoDiscoveries.DiscoverRegion(EcoRegion.At(hit.point));
            var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
            var water = hit.collider.GetComponentInParent<WeaponWaterSource>();
            var scrap = hit.collider.GetComponentInParent<RecyclableResource>();
            var plant = hit.collider.GetComponentInParent<PlantedSeed>();
            var power = hit.collider.GetComponentInParent<EcoPowerNode>();
            var waterStore = hit.collider.GetComponentInParent<EcoWaterNode>();
            var recycling = hit.collider.GetComponentInParent<EcoRecyclingFacility>();
            var farm = hit.collider.GetComponentInParent<EcoFarm>();
            LastScan = recycling != null ? recycling.Describe() : farm != null ? farm.Describe() : waterStore != null ? waterStore.Describe() : power != null ? power.Describe() : plant != null ? ContextHint() : enemy != null ? enemy.DisplayName + " | Can: " + Mathf.CeilToInt(enemy.Health) + " | " + enemy.StateName
                : water != null ? (water.IsClean ? "Temiz su" : "Kirli su")
                : scrap != null ? "Geri dönüştürülebilir: " + scrap.metal + " metal, " + scrap.plastic + " plastik, " + scrap.organic + " organik"
                : EcoRegion.At(hit.point)?.Describe() ?? hit.collider.name;
            if (hit.collider.GetComponentInParent<EcoFactory>() is EcoFactory factory) LastScan = factory.Describe();
            if (hit.collider.GetComponentInParent<EcoFire>() is EcoFire fire) LastScan = fire.Describe();
            if (hit.collider.GetComponentInParent<EcoFishingSpot>() is EcoFishingSpot fishing) LastScan = fishing.Describe();
        }
        Scanned?.Invoke(LastScan); Changed?.Invoke();
    }
    private void ShowStream(Vector3 target, bool contact, Vector3 normal, float seconds)
    {
        if (streamEffects == null) return;
        var flow = Selected == Tool.Recycler ? WeaponVisualEffects.Flow.Recycle
            : Mode == VacuumMode.Water ? WeaponVisualEffects.Flow.Water
            : Mode == VacuumMode.Disperse ? WeaponVisualEffects.Flow.Disperse : WeaponVisualEffects.Flow.Collect;
        streamEffects.Show(flow, holder.transform.position + AimDir * 0.25f, target, contact, normal, seconds);
    }
    private void ResetProcessing() { processing = null; progress = 0; if (streamEffects != null) streamEffects.Stop(); }
    private void OnDisable() { ResetProcessing(); StopCharging(); EcoMarketUI.CloseFor(this); }

    [Serializable]
    private sealed class SessionData
    {
        public int version = 2;
        public int selected, mode; public bool metalMode;
        public int[] produce;
        public float energy, water;
        public bool dirtyWater;
        public int metalAmmo, seeds, metal, plastic, rawMetal, rawPlastic, food, compost;
        public int organic, organicRemainder, yep, smallFilters, largeFilters;
        public int[] upgrades;
        public int[] otherSeeds;
        public int selectedSeed;
        public int cleanFish, dirtyFish;
    }

    public string CaptureSessionJson()
    {
        var data = new SessionData
        {
            produce = (int[])cleanProduce.Clone(), selected = (int)Selected, mode = (int)Mode, metalMode = MetalMode,
            energy = Energy, water = Water, metalAmmo = MetalAmmo, seeds = Seeds,
            metal = Metal, plastic = Plastic, rawMetal = CollectedMetalScrap,
            rawPlastic = CollectedPlasticScrap, food = Food, compost = Compost, dirtyWater = dirtyWater,
            organic = CollectedOrganicWaste, organicRemainder = ProcessedOrganicRemainder, yep = Progression.Experience,
            smallFilters = SmallFilters, largeFilters = LargeFilters, upgrades = Upgrades.Capture(), otherSeeds = (int[])otherSeeds.Clone(), selectedSeed = (int)SelectedSeed,
            cleanFish = CleanFish, dirtyFish = DirtyFish
        };
        return JsonUtility.ToJson(data);
    }

    public void SaveSession()
    {
        if (EcoWorldSave.Current != null) { EcoWorldSave.Current.QuickSave(); return; }
        PlayerPrefs.SetString(sessionKey, CaptureSessionJson()); PlayerPrefs.Save();
    }
    public void LoadSession()
    {
        if (EcoWorldSave.Current != null) { EcoWorldSave.Current.QuickLoad(); return; }
        if (PlayerPrefs.HasKey(sessionKey)) RestoreSessionJson(PlayerPrefs.GetString(sessionKey));
    }
    public bool RestoreSessionJson(string json)
    {
        if (string.IsNullOrEmpty(json) || Progression == null || Upgrades == null) return false;
        SessionData data;
        try { data = JsonUtility.FromJson<SessionData>(json); }
        catch (ArgumentException) { return false; }
        if (data == null || !EcoRegion.Finite(data.energy) || !EcoRegion.Finite(data.water)) return false;
        StopCharging();
        Progression.Restore(data.yep); Upgrades.Restore(data.upgrades);
        Energy = Mathf.Clamp(data.energy, 0f, maximumEnergy); Water = Mathf.Clamp(data.water, 0f, maximumWater);
        MetalAmmo = Mathf.Max(0, data.metalAmmo); Seeds = Mathf.Max(0, data.seeds);
        Array.Clear(otherSeeds, 0, otherSeeds.Length);
        if (data.otherSeeds != null) for (int i = 0; i < Mathf.Min(otherSeeds.Length, data.otherSeeds.Length); i++) otherSeeds[i] = Mathf.Max(0, data.otherSeeds[i]);
        SelectedSeed = EcoCropCatalog.Valid((EcoCropCatalog.Kind)data.selectedSeed) ? (EcoCropCatalog.Kind)data.selectedSeed : EcoCropCatalog.Kind.Cabbage;
        Metal = Mathf.Max(0, data.metal); Plastic = Mathf.Max(0, data.plastic);
        CollectedMetalScrap = Mathf.Max(0, data.rawMetal); CollectedPlasticScrap = Mathf.Max(0, data.rawPlastic);
        Array.Clear(cleanProduce, 0, cleanProduce.Length);
        if (data.produce != null) for (int i = 0; i < Mathf.Min(3, data.produce.Length); i++) cleanProduce[i] = Mathf.Max(0, data.produce[i]);
        else cleanProduce[0] = Mathf.Max(0, data.food);
        Food = cleanProduce[0] + cleanProduce[1] + cleanProduce[2]; Compost = Mathf.Max(0, data.compost);
        CleanFish = Mathf.Clamp(data.cleanFish, 0, 20); DirtyFish = Mathf.Clamp(data.dirtyFish, 0, 20 - CleanFish);
        CollectedOrganicWaste = Mathf.Max(0, data.organic); ProcessedOrganicRemainder = Mathf.Clamp(data.organicRemainder, 0, 2);
        SmallFilters = Mathf.Clamp(data.smallFilters, 0, 20); LargeFilters = Mathf.Clamp(data.largeFilters, 0, 10);
        dirtyWater = data.dirtyWater;
        ResetProcessing();
        Mode = (VacuumMode)Mathf.Clamp(data.mode, 0, 2); MetalMode = data.metalMode;
        SelectTool(Mathf.Clamp(data.selected, 0, 4)); Changed?.Invoke(); return true;
    }
}
