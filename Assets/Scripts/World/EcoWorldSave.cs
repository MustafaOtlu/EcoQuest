using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// One file owns the world and inventory from one transaction boundary (GDD §14).
[DefaultExecutionOrder(900)]
public sealed class EcoWorldSave : MonoBehaviour
{
    public const string Key = "EcoQuest.world.v1";
    public const float AutosaveSeconds = 180f;
    public const int SchemaVersion = 2;
    public static EcoWorldSave Current { get; private set; }
    public string Status { get; private set; } = "";
    public bool IsLoading => loading;
    public bool CanWrite => initialized && !loading && !writing && FindFirstObjectByType<PlayerWeaponSystem>() != null && !BossActive;
    public bool HasSave => Slots.Any(id => File.Exists(PathFor(id)) || File.Exists(PathFor(id) + ".bak"));
    public string SaveDirectory => Path.Combine(Application.persistentDataPath, "EcoQuest", "Saves");
    public string LastSavedAt { get; private set; } = "";

    public sealed class SlotInfo
    {
        public string Id, Name, SavedAt, Detail;
        public bool Exists, CanLoad, IsBackup;
        public int Day;
    }
    [Serializable] private sealed class Envelope
    { public int format = SchemaVersion; public string scene, savedAt, checksum, payload; }
    [Serializable] private sealed class Entity
    {
        public string id, kind, template, parent;
        public bool initial, active;
        public Vector3 position, scale;
        public Quaternion rotation;
        public EcoSaveIdentity.State[] components;
        public Hook[] hooks;
    }
    [Serializable] private sealed class Link
    {
        public string id, from, to;
        public bool water, playerBuilt, connected;
        public int fluid, cost;
        public float rate;
    }
    [Serializable] private sealed class Hook { public string type, json; }
    [Serializable] private sealed class OrderData
    { public string title; public int food, metal, plastic, issuedDay, cropKind; public bool delivered, fish; }
    [Serializable] private sealed class Snapshot
    {
        public int version = SchemaVersion;
        public string session, scene;
        public Vector3 player;
        public Quaternion playerRotation;
        public int day;
        public Entity[] entities;
        public Link[] links;
        public bool[] projectDone;
        public float[] projectHeld;
        public int selectedProject;
        public int[] projectHints, specialization;
        public bool charged, wasBurning, finaleWon;
        public float chargedEnergy;
        public int ordersRefreshDay;
        public OrderData[] orders;
        public Hook[] globalHooks;
        public EcoSaveIdentity.Value[] market;
        public string[] rewards, discoveries;
    }

    private static readonly string[] Slots = { "manual-1", "manual-2", "manual-3", "quick", "auto-1", "auto-2", "auto-3" };
    private static readonly HashSet<string> TrackedTypes = new HashSet<string>
    {
        "EcoStructure", "EcoPowerNode", "EcoWaterNode", "EcoRecyclingFacility", "EcoFactory", "EcoRegion", "EcoWorldClock",
        "EcoHabitat", "EcoFire", "EcoVegetation", "EcoFishingSpot", "EcoEnemySpawnZone", "EnemyBrain", "RecyclableResource",
        "PlantedSeed", "PlayerVitals", "WeaponWaterSource", "EcoGlacierMelt", "EcoGlacierStation", "EcoPollutionInlet",
        "EcoStory", "EcoDiscoveries", "EcoHydroPlant", "EcoChargingStation", "EcoFarm"
    };
    private readonly Dictionary<string, GameObject> initialTemplates = new Dictionary<string, GameObject>();
    private readonly Queue<string> deferredSlots = new Queue<string>();
    private Transform templateBank;
    private bool loading, writing, initialized, autosaveRequested;
    private float activeSeconds, statusUntil;
    private int nextAuto;
    private string pendingSlot;
    private bool BossActive
    { get { var finale = FindFirstObjectByType<EcoFinale>(); return finale != null && finale.Fighting; } }

    private void Awake()
    {
        if (Current != null && Current != this) { enabled = false; return; }
        Current = this;
    }
    private IEnumerator Start()
    {
        yield return null; // let runtime scene bootstrappers finish first
        var bank = new GameObject("EcoQuest save templates (inactive)");
        bank.SetActive(false); bank.hideFlags = HideFlags.HideAndDontSave; templateBank = bank.transform;
        foreach (var go in TrackedObjects())
        {
            var identity = EcoSaveIdentity.Ensure(go, true);
            string kind = KindOf(go);
            if (kind != "enemy" && kind != "waste" && kind != "vegetation" && kind != "plant") continue;
            var prototype = Instantiate(go, templateBank);
            prototype.name = go.name; prototype.SetActive(false);
            initialTemplates[identity.Id] = prototype;
        }
        string newest = "";
        for (int i = 0; i < 3; i++)
            if (TryRead("auto-" + (i + 1), out var envelope, out _, out _) && string.CompareOrdinal(envelope.savedAt, newest) > 0)
            { newest = envelope.savedAt; nextAuto = (i + 1) % 3; }
        initialized = true;
    }
    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (templateBank != null) Destroy(templateBank.gameObject);
    }
    private void Update()
    {
        if (!initialized || loading) return;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (EcoKeys.Pressed(EcoKeys.Action.QuickSave)) QuickSave();
            if (EcoKeys.Pressed(EcoKeys.Action.QuickLoad)) QuickLoad();
        }
        if (!PlayerController.Paused && Time.timeScale > 0f) activeSeconds += Time.unscaledDeltaTime;
        if (activeSeconds >= AutosaveSeconds) autosaveRequested = true;
    }
    private void LateUpdate()
    {
        if (!CanWrite) return;
        if (pendingSlot == null && deferredSlots.Count > 0) pendingSlot = deferredSlots.Dequeue();
        if (pendingSlot != null)
        {
            string slot = pendingSlot; pendingSlot = null;
            WriteSlot(slot, false);
        }
        if (!autosaveRequested || PlayerController.Paused || Time.timeScale <= 0f) return;
        var vitals = FindFirstObjectByType<PlayerVitals>();
        if (vitals != null && vitals.IsRecovering) return;
        if (WriteSlot("auto-" + (nextAuto + 1), true))
        { nextAuto = (nextAuto + 1) % 3; activeSeconds = 0f; autosaveRequested = false; }
    }

    // Existing callers stay valid. Requests are captured only after the triggering transaction completes.
    public void Save(bool quiet) { if (quiet) RequestAutosave(); else QuickSave(); }
    public void Load() { QuickLoad(); }
    public void RequestAutosave() { autosaveRequested = true; }
    public bool QuickSave() => QueueSave("quick");
    public bool SaveManual(int slot) => slot >= 1 && slot <= 3 && QueueSave("manual-" + slot);
    public bool LoadManual(int slot) => slot >= 1 && slot <= 3 && LoadSlot("manual-" + slot);
    public bool QuickLoad()
    {
        var latest = GetSlots().Where(s => s.CanLoad).OrderByDescending(s => s.SavedAt, StringComparer.Ordinal).FirstOrDefault();
        if (latest != null && latest.Id == "pre-finale") return LoadSlot(latest.Id);
        if (File.Exists(PathFor("quick")) || File.Exists(PathFor("quick") + ".bak")) return LoadSlot("quick");
        if (latest != null) return LoadSlot(latest.Id);
        Say(PlayerPrefs.HasKey(Key)
            ? "Eski kayıt eksik dünya biçiminde; kaynakların çoğalmaması için ayrı envanterle birleştirilmedi. Eski kayıt korundu."
            : "Kayıt bulunamadı.");
        return false;
    }
    private bool QueueSave(string slot)
    {
        if (!initialized || loading || writing) { Say("Dünya henüz kayıt için hazır değil."); return false; }
        if (BossActive)
        {
            if (!deferredSlots.Contains(slot)) deferredSlots.Enqueue(slot);
            Say("Kayıt isteği bekliyor: final denemesi bitince dünya ve envanter birlikte kaydedilecek.");
            return true;
        }
        if (pendingSlot != null && pendingSlot != slot && !deferredSlots.Contains(pendingSlot)) deferredSlots.Enqueue(pendingSlot);
        pendingSlot = slot; return true;
    }
    // Synchronous by design: a failed snapshot must prevent the encounter from starting.
    public bool SaveBeforeFinale()
    {
        if (!CanWrite) { Say("Final öncesi güvenli kayıt oluşturulamadı."); return false; }
        bool result = WriteSlot("pre-finale", true);
        if (result) Say("Final öncesi dünya ve bütün kaynaklar güvenle kaydedildi.");
        return result;
    }
    public bool LoadBeforeFinale() => LoadSlot("pre-finale");
    public SlotInfo[] GetSlots()
    {
        // The safety checkpoint is also selectable after an interrupted finale attempt.
        var ids = File.Exists(PathFor("pre-finale")) || File.Exists(PathFor("pre-finale") + ".bak") ? Slots.Concat(new[] { "pre-finale" }) : Slots;
        return ids.Select(id =>
        {
            var info = new SlotInfo { Id = id, Name = SlotName(id), Exists = File.Exists(PathFor(id)) || File.Exists(PathFor(id) + ".bak"), Detail = "Boş slot", SavedAt = "" };
            if (!info.Exists) return info;
            if (!TryRead(id, out var envelope, out var snapshot, out var reason)) { info.Detail = reason; return info; }
            info.CanLoad = true; info.Day = snapshot.day; info.SavedAt = envelope.savedAt;
            info.IsBackup = reason.Length > 0;
            info.Detail = "Gün " + snapshot.day + " • " + (snapshot.entities?.Count(e => e.kind == "structure") ?? 0) + " yapı" + (info.IsBackup ? " • sağlam yedek" : "");
            return info;
        }).ToArray();
    }

    private bool WriteSlot(string slot, bool quiet)
    {
        if (!CanWrite) return false;
        writing = true;
        try
        {
            Snapshot snapshot = Capture();
            string json = JsonUtility.ToJson(snapshot);
            var envelope = new Envelope { scene = snapshot.scene, savedAt = DateTime.UtcNow.ToString("o"), payload = json, checksum = Digest(json) };
            Directory.CreateDirectory(SaveDirectory);
            string path = PathFor(slot), temporary = path + ".tmp", backup = path + ".bak";
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            // Atomic same-volume replacement; no partially written primary file is ever presented as a save.
            if (File.Exists(path)) File.Replace(temporary, path, backup);
            else File.Move(temporary, path);
            LastSavedAt = envelope.savedAt;
            if (!quiet) Say(SlotName(slot) + " kaydedildi • Dünya ve envanter aynı anda.");
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException || error is TargetInvocationException)
        { Say("Kayıt oluşturulamadı; önceki sağlam kayıt korundu. " + error.Message); return false; }
        finally { writing = false; }
    }
    public bool LoadSlot(string slot)
    {
        if (!ValidSlot(slot) || !initialized || loading || writing) return false;
        if (!TryRead(slot, out _, out var snapshot, out var reason)) { Say(reason); return false; }
        if (!string.IsNullOrEmpty(snapshot.scene) && snapshot.scene != SceneManager.GetActiveScene().path)
        { Say("Bu kayıt farklı bir haritaya ait: " + snapshot.scene); return false; }
        if (BossActive) FindFirstObjectByType<EcoFinale>()?.RestoreState(false);
        StartCoroutine(Restore(snapshot, reason.Length > 0));
        return true;
    }
    private bool TryRead(string slot, out Envelope envelope, out Snapshot snapshot, out string reason)
    {
        envelope = null; snapshot = null; reason = "Kayıt bulunamadı.";
        if (!ValidSlot(slot)) return false;
        for (int i = 0; i < 2; i++)
        {
            try
            {
                string candidate = PathFor(slot) + (i == 0 ? "" : ".bak");
                if (!File.Exists(candidate)) continue;
                var file = JsonUtility.FromJson<Envelope>(File.ReadAllText(candidate, Encoding.UTF8));
                if (file == null || file.format != SchemaVersion)
                { reason = "Bu kayıt sürümü okunamıyor; dosya korundu."; continue; }
                if (string.IsNullOrEmpty(file.payload) || Digest(file.payload) != file.checksum)
                { reason = "Kayıt bütünlüğü doğrulanamadı; dosya korundu."; continue; }
                var data = JsonUtility.FromJson<Snapshot>(file.payload);
                if (data == null || data.version != SchemaVersion || string.IsNullOrEmpty(data.session) || data.entities == null)
                { reason = "Kayıtta gerekli dünya veya envanter verileri eksik."; continue; }
                envelope = file; snapshot = data; reason = i == 1 ? "Son sağlam yedek yüklenecek." : "";
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { reason = "Kayıt okunamadı; dosya korundu. " + error.Message; }
        }
        return false;
    }

    private Snapshot Capture()
    {
        var objects = TrackedObjects();
        foreach (var go in objects) EcoSaveIdentity.Ensure(go);
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (weapons == null) throw new InvalidOperationException("Oyuncu envanteri hazır değil.");
        var data = new Snapshot { session = weapons.CaptureSessionJson(), scene = SceneManager.GetActiveScene().path,
            player = weapons.transform.position, playerRotation = weapons.transform.rotation, day = EcoWorldClock.Current != null ? EcoWorldClock.Current.day : 1 };
        var controller = FindFirstObjectByType<PlayerController>();
        if (controller != null) { data.player = controller.transform.position; data.playerRotation = controller.transform.rotation; }
        data.entities = objects.Where(go => (go.GetComponent<EnemyBrain>() == null || go.GetComponent<EnemyBrain>().Health > 0f)
            && (go.GetComponent<RecyclableResource>() == null || !go.GetComponent<RecyclableResource>().Claimed)).Select(CaptureEntity).ToArray();
        var links = new List<Link>();
        foreach (var cable in FindObjectsByType<EcoPowerCable>(FindObjectsSortMode.None))
            if (cable.a != null && cable.b != null) links.Add(new Link { id = EcoSaveIdentity.Ensure(cable.gameObject).Id,
                from = EcoSaveIdentity.Ensure(cable.a.gameObject).Id, to = EcoSaveIdentity.Ensure(cable.b.gameObject).Id,
                cost = cable.materialCost, connected = cable.connected, playerBuilt = cable.playerBuilt });
        foreach (var pipe in FindObjectsByType<EcoWaterPipe>(FindObjectsSortMode.None))
            if (pipe.from != null && pipe.to != null) links.Add(new Link { id = EcoSaveIdentity.Ensure(pipe.gameObject).Id, water = true,
                from = EcoSaveIdentity.Ensure(pipe.from.gameObject).Id, to = EcoSaveIdentity.Ensure(pipe.to.gameObject).Id,
                cost = pipe.materialCost, fluid = (int)pipe.fluid, connected = pipe.connected, playerBuilt = pipe.playerBuilt, rate = pipe.litresPerSecond });
        data.links = links.ToArray();
        var projects = EcoProjects.Current;
        if (projects != null)
        {
            data.projectDone = projects.All.Select(p => p.done).ToArray(); data.projectHeld = projects.All.Select(p => p.held).ToArray();
            data.charged = projects.Charged; data.wasBurning = projects.WasBurning;
            data.chargedEnergy = ReadProperty(projects, "ChargedEnergy", 0f); data.selectedProject = ReadProperty(projects, "SelectedIndex", 0);
            data.projectHints = ReadProperty(projects, "HintLevels", Array.Empty<int>());
        }
        var orders = EcoOrders.Current;
        if (orders != null)
        {
            data.ordersRefreshDay = orders.LastRefreshDay;
            data.orders = orders.Open.Select(o => new OrderData { title = o.title, food = o.food, metal = o.metal, plastic = o.plastic,
                issuedDay = o.issuedDay, delivered = o.delivered, cropKind = o.cropKind, fish = o.fish }).ToArray();
        }
        data.specialization = EcoSpecialization.Capture();
        var finale = FindFirstObjectByType<EcoFinale>(); data.finaleWon = finale != null && finale.Won;
        data.market = CaptureMarket();
        data.rewards = InvokeCaptureStrings(weapons.Progression, "CaptureRewards");
        data.discoveries = InvokeCaptureStaticStrings("EcoDiscoveries", "Capture");
        data.globalHooks = CaptureHooks(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c => c != null && (c.GetType().Name == "EcoStory" || c.GetType().Name == "EcoFinale" || c.GetType().Name == "EcoGlacierStation")));
        return data;
    }
    private Entity CaptureEntity(GameObject go)
    {
        var identity = EcoSaveIdentity.Ensure(go);
        string kind = KindOf(go), template = "";
        if (kind == "structure") template = go.GetComponent<EcoStructure>().buildingId;
        else if (kind == "plant") template = ((int)go.GetComponent<PlantedSeed>().kind).ToString();
        else if (kind == "enemy") template = ((int)go.GetComponent<EnemyBrain>().species).ToString();
        return new Entity { id = identity.Id, initial = identity.InitialObject, kind = kind, template = template,
            active = go.activeSelf, position = go.transform.position, rotation = go.transform.rotation, scale = go.transform.localScale,
            parent = go.transform.parent != null && go.transform.parent.TryGetComponent<EcoSaveIdentity>(out var parent) ? parent.Id : "",
            components = go.GetComponents<MonoBehaviour>().Where(c => c != null && TrackedTypes.Contains(c.GetType().Name)).Select(EcoSaveIdentity.Capture).ToArray(),
            hooks = CaptureHooks(go.GetComponents<MonoBehaviour>()) };
    }
    private IEnumerator Restore(Snapshot data, bool fromBackup)
    {
        loading = true; pendingSlot = null; deferredSlots.Clear(); autosaveRequested = false;
        float previousScale = Time.timeScale; Time.timeScale = 0f;
        string failure = null;
        try
        {
            var weapons = FindFirstObjectByType<PlayerWeaponSystem>();
            if (weapons == null || !weapons.RestoreSessionJson(data.session)) throw new InvalidOperationException("Envanter kaydı okunamadı.");
            FindFirstObjectByType<EcoFinale>()?.RestoreState(data.finaleWon);
            var wanted = new HashSet<string>(data.entities.Select(e => e.id));
            foreach (var go in TrackedObjects())
            {
                var identity = EcoSaveIdentity.Ensure(go);
                if (!wanted.Contains(identity.Id) && MutableEntity(go)) { go.SetActive(false); Destroy(go); }
            }
            foreach (var cable in FindObjectsByType<EcoPowerCable>(FindObjectsSortMode.None)) { cable.gameObject.SetActive(false); Destroy(cable.gameObject); }
            foreach (var pipe in FindObjectsByType<EcoWaterPipe>(FindObjectsSortMode.None)) { pipe.gameObject.SetActive(false); Destroy(pipe.gameObject); }
            foreach (var shot in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
            foreach (var shot in FindObjectsByType<PlayerMetalProjectile>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
            foreach (var shot in FindObjectsByType<PlayerSeedProjectile>(FindObjectsSortMode.None)) Destroy(shot.gameObject);
            var map = TrackedObjects().Where(go => wanted.Contains(EcoSaveIdentity.Ensure(go).Id)).GroupBy(go => EcoSaveIdentity.Ensure(go).Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var entity in data.entities)
            {
                if (map.TryGetValue(entity.id, out var changed) && changed != null && MutableEntity(changed)
                    && (KindOf(changed) != entity.kind || (changed.GetComponent<EnemyBrain>() != null && changed.GetComponent<EnemyBrain>().Health <= 0f)))
                { changed.SetActive(false); Destroy(changed); map.Remove(entity.id); }
                if (!map.TryGetValue(entity.id, out var go) || go == null)
                {
                    go = Recreate(entity);
                    if (go == null) throw new InvalidOperationException("Kayıttaki nesnenin kaynağı bulunamadı: " + entity.kind + " / " + entity.template);
                    map[entity.id] = go;
                }
                EcoSaveIdentity.Ensure(go).Assign(entity.id, entity.initial);
                var motor = go.GetComponent<CharacterController>(); bool motorEnabled = motor != null && motor.enabled;
                if (motorEnabled) motor.enabled = false;
                go.transform.SetPositionAndRotation(entity.position, entity.rotation);
                // The player's size is a setting (PlayerController.characterScale), not save data; older saves stored 1.
                go.transform.localScale = go.TryGetComponent<PlayerController>(out var hero) ? Vector3.one * hero.characterScale : entity.scale;
                go.SetActive(entity.active);
                foreach (var state in entity.components ?? Array.Empty<EcoSaveIdentity.State>())
                {
                    var type = typeof(EcoWorldSave).Assembly.GetType(state.type);
                    if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type) || !TrackedTypes.Contains(type.Name)) continue;
                    var component = go.GetComponent(type) ?? go.AddComponent(type);
                    EcoSaveIdentity.Apply(component, state);
                }
                if (motorEnabled && go.activeInHierarchy && (go.GetComponent<EnemyBrain>() == null || go.GetComponent<EnemyBrain>().Health > 0f)) motor.enabled = true;
                RestoreHooks(entity.hooks, go);
                if (go.TryGetComponent<EcoFactory>(out var factory)) factory.RestoreState(factory.Filtered, factory.Converted);
                if (go.TryGetComponent<EcoFire>(out var fire)) InvokeOptional(fire, "RefreshVisual");
                if (go.TryGetComponent<PlantedSeed>(out var plant)) InvokeOptional(plant, "UpdateVisual");
            }
            foreach (var entity in data.entities)
                if (!string.IsNullOrEmpty(entity.parent) && map.TryGetValue(entity.id, out var child) && map.TryGetValue(entity.parent, out var parent)) child.transform.SetParent(parent.transform, true);
            foreach (var link in data.links ?? Array.Empty<Link>()) RestoreLink(link, map);
            foreach (var water in FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None)) if (water.kind == EcoWaterNode.Kind.Intake) water.FindSource();
            RestoreProgress(data); RestoreMarket(data.market);
            InvokeRestoreStrings(weapons.Progression, "RestoreRewards", data.rewards);
            InvokeRestoreStaticStrings("EcoDiscoveries", "Restore", data.discoveries);
            RestoreHooks(data.globalHooks, null);
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null) { player.Teleport(data.player); player.transform.rotation = data.playerRotation; }
            EcoNavigation.MarkDirty();
        }
        catch (Exception error) { failure = error.Message; }
        yield return null; // finish deferred destruction with world simulation frozen
        Time.timeScale = previousScale; loading = false; activeSeconds = 0f;
        Say(failure == null ? "Dünya ve envanter yüklendi • Gün " + data.day + (fromBackup ? " • sağlam yedekten" : "")
            : "Yükleme tamamlanamadı. Kayıt dosyası korundu: " + failure);
    }
    private void RestoreProgress(Snapshot data)
    {
        var projects = EcoProjects.Current;
        if (projects != null && data.projectDone != null)
        {
            for (int i = 0; i < projects.All.Count; i++)
            {
                projects.All[i].done = i < data.projectDone.Length && data.projectDone[i];
                projects.All[i].held = data.projectHeld != null && i < data.projectHeld.Length ? Mathf.Clamp(data.projectHeld[i], 0, EcoProjects.VerifySeconds) : 0;
            }
            projects.Charged = data.charged; projects.WasBurning = data.wasBurning;
            WriteProperty(projects, "ChargedEnergy", data.chargedEnergy); WriteProperty(projects, "SelectedIndex", data.selectedProject); WriteProperty(projects, "HintLevels", data.projectHints);
        }
        if (EcoOrders.Current != null)
            EcoOrders.Current.Restore(data.ordersRefreshDay, (data.orders ?? Array.Empty<OrderData>()).Select(o =>
                new EcoOrders.Order { title = o.title, food = o.food, metal = o.metal, plastic = o.plastic, issuedDay = o.issuedDay,
                    delivered = o.delivered, cropKind = o.cropKind, fish = o.fish }).ToList());
        EcoSpecialization.Restore(data.specialization);
    }
    private GameObject Recreate(Entity entity)
    {
        if (initialTemplates.TryGetValue(entity.id, out var prototype) && prototype != null)
        {
            var clone = Instantiate(prototype, prototype.transform.position, prototype.transform.rotation);
            clone.hideFlags = HideFlags.None; clone.SetActive(true); return clone;
        }
        if (entity.kind == "structure")
        {
            int index = EcoBuildingCatalog.Find(entity.template);
            if (index < 0) return null;
            var prefab = Resources.Load<GameObject>("EcoInfrastructure/" + EcoBuildingCatalog.Entries[index].Prefab);
            return prefab != null ? Instantiate(prefab, entity.position, entity.rotation) : null;
        }
        if (entity.kind == "plant" && int.TryParse(entity.template, out int kind))
            return PlantedSeed.Create(entity.position, (EcoCropCatalog.Kind)kind)?.gameObject;
        if (entity.kind == "vegetation")
        {
            var plant = PlantedSeed.Create(entity.position, EcoCropCatalog.Kind.Tree);
            if (plant == null) return null;
            plant.growth = 1f; plant.moisture = 1f;
            InvokeOptional(plant, "MatureTree"); return plant.gameObject;
        }
        if (entity.kind == "enemy" && int.TryParse(entity.template, out int species))
        {
            foreach (var zone in FindObjectsByType<EcoEnemySpawnZone>(FindObjectsSortMode.None))
                foreach (var prefab in zone.enemyPrefabs ?? Array.Empty<GameObject>())
                    if (prefab != null && prefab.TryGetComponent<EnemyBrain>(out var brain) && (int)brain.species == species)
                        return Instantiate(prefab, entity.position, entity.rotation);
            return null;
        }
        if (entity.kind == "waste")
        {
            var state = entity.components?.FirstOrDefault(s => s.type == typeof(RecyclableResource).FullName);
            long metal = state?.fields?.FirstOrDefault(f => f.name == "metal")?.integer ?? 0;
            var drop = EcoWorldArt.Spawn(metal > 0 ? "MetalScrap" : "PlasticScrap", entity.position);
            if (drop != null && drop.GetComponent<RecyclableResource>() == null) drop.AddComponent<RecyclableResource>();
            return drop;
        }
        return null;
    }
    private static void RestoreLink(Link data, Dictionary<string, GameObject> map)
    {
        if (!map.TryGetValue(data.from, out var a) || !map.TryGetValue(data.to, out var b)) throw new InvalidOperationException("Kayıtlı bağlantının uç kimliği bulunamadı.");
        var go = new GameObject(data.water ? "Saved water pipe" : "Saved power cable");
        EcoSaveIdentity.Ensure(go).Assign(data.id, false);
        if (data.water)
        {
            var pipe = go.AddComponent<EcoWaterPipe>(); pipe.from = a.GetComponent<EcoWaterNode>(); pipe.to = b.GetComponent<EcoWaterNode>();
            pipe.fluid = (EcoWaterNode.Fluid)data.fluid; pipe.playerBuilt = data.playerBuilt; pipe.materialCost = data.cost; pipe.connected = data.connected; pipe.litresPerSecond = data.rate;
        }
        else
        {
            var cable = go.AddComponent<EcoPowerCable>(); cable.a = a.GetComponent<EcoPowerNode>(); cable.b = b.GetComponent<EcoPowerNode>();
            cable.playerBuilt = data.playerBuilt; cable.materialCost = data.cost; cable.connected = data.connected;
        }
        var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = Resources.Load<Material>("EcoInfrastructure/" + (data.water ? "WaterPipe" : "Cable"));
        line.startWidth = line.endWidth = data.water ? 0.09f : 0.035f; line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    private List<GameObject> TrackedObjects()
    {
        return Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(c => c != null && c.gameObject.scene.IsValid()
            && TrackedTypes.Contains(c.GetType().Name) && (templateBank == null || !c.transform.IsChildOf(templateBank))
            && !TemporaryFinaleObject(c.transform)).Select(c => c.gameObject).Distinct().ToList();
    }
    private static bool TemporaryFinaleObject(Transform node)
    {
        for (var current = node; current != null; current = current.parent)
            if (current.name == "Finale encounter (temporary)" || current.GetComponents<MonoBehaviour>().Any(c => c != null && c.GetType().Name == "EcoFinaleActor")) return true;
        return false;
    }
    private static bool MutableEntity(GameObject go) => go.GetComponent<EnemyBrain>() != null || go.GetComponent<PlantedSeed>() != null
        || go.GetComponent<RecyclableResource>() != null || go.GetComponent<EcoVegetation>() != null || (go.GetComponent<EcoStructure>() != null && go.GetComponent<EcoStructure>().playerBuilt);
    private static string KindOf(GameObject go) => go.GetComponent<EcoStructure>() != null ? "structure" : go.GetComponent<EnemyBrain>() != null ? "enemy"
        : go.GetComponent<PlantedSeed>() != null ? "plant" : go.GetComponent<EcoVegetation>() != null ? "vegetation" : go.GetComponent<RecyclableResource>() != null ? "waste" : "scene";
    private static Hook[] CaptureHooks(IEnumerable<MonoBehaviour> components)
    {
        var result = new List<Hook>();
        foreach (var component in components)
        {
            if (component == null) continue;
            var method = component.GetType().GetMethod("CaptureState", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType == typeof(void)) continue;
            object state = method.Invoke(component, null);
            if (state != null) result.Add(new Hook { type = component.GetType().FullName, json = JsonUtility.ToJson(state) });
        }
        return result.ToArray();
    }
    private static void RestoreHooks(Hook[] hooks, GameObject target)
    {
        foreach (var hook in hooks ?? Array.Empty<Hook>())
        {
            var type = typeof(EcoWorldSave).Assembly.GetType(hook.type);
            if (type == null) continue;
            var component = target != null ? target.GetComponent(type) : FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(c => c != null && c.GetType() == type);
            if (component == null) continue;
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(m => m.Name == "RestoreState" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType != typeof(bool));
            if (method != null) method.Invoke(component, new[] { JsonUtility.FromJson(hook.json, method.GetParameters()[0].ParameterType) });
        }
    }
    private static T ReadProperty<T>(object target, string name, T fallback)
    { var property = target.GetType().GetProperty(name); return property != null && property.GetValue(target) is T result ? result : fallback; }
    private static void WriteProperty(object target, string name, object value)
    { var property = target.GetType().GetProperty(name); if (property != null && property.CanWrite && value != null) property.SetValue(target, value); }
    private static void InvokeOptional(object target, string method)
    { target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.Invoke(target, null); }
    private static string[] InvokeCaptureStrings(object target, string method)
    { return target?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)?.Invoke(target, null) as string[] ?? Array.Empty<string>(); }
    private static void InvokeRestoreStrings(object target, string method, string[] values)
    { if (values != null) target?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)?.Invoke(target, new object[] { values }); }
    private static string[] InvokeCaptureStaticStrings(string typeName, string method)
    { return typeof(EcoWorldSave).Assembly.GetType(typeName)?.GetMethod(method, BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null) as string[] ?? Array.Empty<string>(); }
    private static void InvokeRestoreStaticStrings(string typeName, string method, string[] values)
    { if (values != null) typeof(EcoWorldSave).Assembly.GetType(typeName)?.GetMethod(method, BindingFlags.Static | BindingFlags.Public)?.Invoke(null, new object[] { values }); }
    private static EcoSaveIdentity.Value[] CaptureMarket()
    {
        var result = new List<EcoSaveIdentity.Value>();
        foreach (var field in typeof(EcoMarket).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
            if (field.Name == "boughtToday" || field.Name == "boughtDay")
            { var value = EcoSaveIdentity.Encode(field.Name, field.FieldType, field.GetValue(null)); if (value != null) result.Add(value); }
        return result.ToArray();
    }
    private static void RestoreMarket(EcoSaveIdentity.Value[] fields)
    {
        foreach (var value in fields ?? Array.Empty<EcoSaveIdentity.Value>())
        {
            var field = typeof(EcoMarket).GetField(value.name, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) continue;
            var decoded = EcoSaveIdentity.Decode(field.FieldType, value);
            if (field.IsInitOnly && field.GetValue(null) is Array existing && decoded is Array incoming)
            { Array.Clear(existing, 0, existing.Length); Array.Copy(incoming, existing, Math.Min(incoming.Length, existing.Length)); }
            else if (!field.IsInitOnly) field.SetValue(null, decoded);
        }
    }
    private string PathFor(string slot) => Path.Combine(SaveDirectory, slot + ".json");
    private static bool ValidSlot(string id) => id == "pre-finale" || Array.IndexOf(Slots, id) >= 0;
    private static string SlotName(string id) => id.StartsWith("manual-", StringComparison.Ordinal) ? "Elle kayıt " + id.Substring(7)
        : id.StartsWith("auto-", StringComparison.Ordinal) ? "Otomatik kayıt " + id.Substring(5) : id == "pre-finale" ? "Final öncesi" : "Hızlı kayıt";
    private static string Digest(string text)
    { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text))); }
    private void Say(string text)
    {
        Status = text; statusUntil = Time.unscaledTime + 5f;
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>(); if (weapons != null) weapons.ShowFeedback(text);
    }
    private void OnGUI()
    {
        if (Time.unscaledTime > statusUntil || string.IsNullOrEmpty(Status)) return;
        // Small themed toast in the free bottom-right corner (it used to be bare text over the toolbelt).
        // Drawn above the pause menu's shade (saving from Kayıt slotları), with text kept inside the 16 px wooden frame.
        GUI.depth = -10;
        EcoTheme.Begin();
        var style = EcoTheme.Muted;
        float width = Mathf.Min(440f, EcoTheme.Width - 40f), textWidth = width - 74f;
        float height = style.CalcHeight(new GUIContent(Status), textWidth) + 32f;
        var card = new Rect(EcoTheme.Width - width - 16f, EcoTheme.Height - height - 16f, width, height);
        EcoTheme.Panel(card, false);
        EcoTheme.Icon(new Rect(card.x + 18f, card.y + (card.height - 26f) * 0.5f, 26f, 26f), "calendar");
        GUI.Label(new Rect(card.x + 52f, card.y + 16f, textWidth, height - 32f), Status, style);
        EcoTheme.End();
    }
}
