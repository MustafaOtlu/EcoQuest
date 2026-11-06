using System.Collections;
using UnityEngine;

// A small, saved introduction to the actual construction and electricity systems.
[DefaultExecutionOrder(-5)]
public sealed class EcoEnergyTutorial : MonoBehaviour
{
    public static EcoEnergyTutorial Current { get; private set; }
    public bool initialized, kitGranted, complete;
    public int stage;
    public float chargedEnergy, observedCharge;
    public string solarId = "", batteryId = "", chargerId = "";

    public bool Active => !complete;
    public float ChargedEnergy => chargedEnergy;
    public float Progress => complete ? 1f : Mathf.Clamp01((stage + (stage == 6 ? chargedEnergy / 10f : 0f)) / 7f);
    private PlayerWeaponSystem player;
    private EcoEnergyTutorialSite site;
    private EcoPowerNode solar, battery, charger;
    private float nextScan;
    private bool restored;
    private bool hidePending;

    private void Awake() => Current = this;
    private void OnDestroy() { if (Current == this) Current = null; }

    private IEnumerator Start()
    {
        yield return null;
        yield return null; // PlayerWeaponSystem.Start and the world's initial template capture come first.
        while (player == null || player.Upgrades == null || Loading)
        {
            if (player == null) player = FindFirstObjectByType<PlayerWeaponSystem>();
            yield return null;
        }
        FindSite();
        if (!initialized && !restored)
        {
            initialized = true;
            if (!kitGranted)
            {
                // The existing new-game kit already contains 50M/40P; never add a second copy.
                player.ReturnMaterials(Mathf.Max(0, 50 - player.Metal), Mathf.Max(0, 40 - player.Plastic));
                if (player.Energy > player.maximumEnergy * 0.8f)
                    player.SpendFraction(1f - player.maximumEnergy * 0.8f / player.Energy);
                kitGranted = true;
                player.ShowFeedback("Başlangıç çantası hazır: 50 metal, 40 plastik. İlk enerji ağını kendin kur.");
            }
        }
        if (Active) site?.HideLegacy();
        Refresh();
    }

    private static bool Loading => EcoWorldSave.Current != null && EcoWorldSave.Current.IsLoading;
    private void FindSite() { if (site == null) site = FindFirstObjectByType<EcoEnergyTutorialSite>(); }
    private void Update()
    {
        if (!initialized || Loading || Time.unscaledTime < nextScan) return;
        if (complete && EcoProjects.Current != null && EcoProjects.Current.All.Count > 0 && EcoProjects.Current.All[0].done) return;
        if (complete && Own(solar, EcoPowerNode.Kind.Solar) && Own(battery, EcoPowerNode.Kind.Battery) && Own(charger, EcoPowerNode.Kind.Charger)) return;
        nextScan = Time.unscaledTime + 0.25f;
        Refresh();
    }

    private bool Own(EcoPowerNode node, EcoPowerNode.Kind kind)
        => node != null && node.Operational && node.kind == kind && node.gameObject.scene == gameObject.scene
            && node.TryGetComponent<EcoStructure>(out var structure) && structure.playerBuilt;

    private EcoPowerNode Resolve(EcoPowerNode[] nodes, string id, EcoPowerNode.Kind kind)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var node in nodes)
            if (Own(node, kind) && node.TryGetComponent<EcoSaveIdentity>(out var identity) && identity.Id == id) return node;
        return null;
    }
    private EcoPowerNode Nearest(EcoPowerNode[] nodes, EcoPowerNode.Kind kind, Vector3 anchor)
    {
        EcoPowerNode best = null; float distance = float.PositiveInfinity;
        foreach (var node in nodes)
        {
            if (!Own(node, kind)) continue;
            float d = (node.transform.position - anchor).sqrMagnitude;
            if (d >= distance) continue;
            best = node; distance = d;
        }
        return best;
    }
    private static bool DirectLink(EcoPowerNode a, EcoPowerNode b)
    {
        if (a == null || b == null) return false;
        foreach (var cable in EcoPowerCable.Active)
            if (cable != null && cable.playerBuilt && cable.IsConnected
                && ((cable.a == a && cable.b == b) || (cable.a == b && cable.b == a))) return true;
        return false;
    }

    private void Refresh()
    {
        if (Loading) return;
        if (player == null) player = FindFirstObjectByType<PlayerWeaponSystem>();
        if (player == null || player.Upgrades == null) return;
        FindSite();
        if (hidePending && Active) { site?.HideLegacy(); hidePending = false; }
        var nodes = FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None);
        solar = Resolve(nodes, solarId, EcoPowerNode.Kind.Solar) ?? Nearest(nodes, EcoPowerNode.Kind.Solar, SolarPosition);
        solarId = solar != null ? EcoSaveIdentity.Ensure(solar.gameObject).Id : "";
        battery = Resolve(nodes, batteryId, EcoPowerNode.Kind.Battery) ?? Nearest(nodes, EcoPowerNode.Kind.Battery, BatteryPosition);
        batteryId = battery != null ? EcoSaveIdentity.Ensure(battery.gameObject).Id : "";
        var nextCharger = Resolve(nodes, chargerId, EcoPowerNode.Kind.Charger) ?? Nearest(nodes, EcoPowerNode.Kind.Charger, ChargerPosition);
        string nextId = nextCharger != null ? EcoSaveIdentity.Ensure(nextCharger.gameObject).Id : "";
        if (nextId != chargerId)
        {
            if (!complete) chargedEnergy = 0f;
            observedCharge = nextCharger != null && nextCharger.TryGetComponent<EcoChargingStation>(out var station) ? station.TotalDeliveredEnergy : 0f;
        }
        charger = nextCharger; chargerId = nextId;
        if (complete) { stage = 7; return; }
        bool openedBuild = stage > 0 || (player.Builder != null && player.Builder.IsBuilding);
        stage = !openedBuild ? 0 : solar == null ? 1 : battery == null ? 2 : !DirectLink(solar, battery) ? 3
            : charger == null ? 4 : !DirectLink(battery, charger) ? 5 : 6;
        ObserveOwnCharge();
        if (stage == 6 && chargedEnergy >= 10f)
        {
            complete = true; stage = 7;
            player.ShowFeedback("Başlangıç tamam • Paneli, bataryayı ve şarj istasyonunu kurdun; kendi ağından 10 EB şarj ettin.");
        }
    }

    public bool MatchesNetwork(EcoPowerNode candidate)
        => candidate != null && candidate == charger && Own(solar, EcoPowerNode.Kind.Solar)
            && Own(battery, EcoPowerNode.Kind.Battery) && Own(charger, EcoPowerNode.Kind.Charger)
            && DirectLink(solar, battery) && DirectLink(battery, charger)
            && EcoPowerGrid.AreConnected(solar, charger);

    public float ObserveOwnCharge()
    {
        if (Loading || charger == null || !charger.TryGetComponent<EcoChargingStation>(out var station)) return chargedEnergy;
        float delivered = Mathf.Max(0f, station.TotalDeliveredEnergy);
        if (stage >= 6 && MatchesNetwork(charger))
            chargedEnergy = Mathf.Clamp(chargedEnergy + Mathf.Max(0f, delivered - observedCharge), 0f, 10f);
        observedCharge = delivered;
        return chargedEnergy;
    }

    public void RestoreAfterLoad(bool hasSavedState, bool firstEnergyProjectDone)
    {
        restored = true; initialized = true; kitGranted = true;
        FindSite();
        solar = battery = charger = null;
        if (firstEnergyProjectDone) { complete = true; stage = 7; chargedEnergy = 10f; return; }
        if (!hasSavedState)
        {
            complete = false; stage = 0; chargedEnergy = observedCharge = 0f;
            solarId = batteryId = chargerId = "";
        }
        stage = complete ? 7 : Mathf.Clamp(stage, 0, 6);
        chargedEnergy = EcoRegion.Finite(chargedEnergy) ? Mathf.Clamp(chargedEnergy, 0f, 10f) : 0f;
        observedCharge = EcoRegion.Finite(observedCharge) ? Mathf.Max(0f, observedCharge) : 0f;
        hidePending = Active;
        nextScan = 0f;
    }

    private Vector3 SolarPosition => solar != null ? solar.transform.position : site != null ? site.SolarPosition : transform.position;
    private Vector3 BatteryPosition => site != null ? SolarPosition + site.BatteryPosition - site.SolarPosition : SolarPosition + Vector3.right * 4f;
    private Vector3 ChargerPosition => site != null ? SolarPosition + site.ChargerPosition - site.SolarPosition : SolarPosition + new Vector3(4f, 0f, 4f);
    public Vector3? Target => complete ? (Vector3?)null : stage <= 1 ? SolarPosition : stage == 2 ? BatteryPosition
        : stage == 3 ? (solar != null ? solar.transform.position : SolarPosition) : stage == 4 ? ChargerPosition
        : charger != null ? charger.transform.position : ChargerPosition;
    public string TargetName => stage <= 1 ? "Güneş paneli alanı" : stage == 2 ? "Batarya alanı" : stage == 3 ? "Panel–batarya bağlantısı"
        : stage == 4 ? "Şarj istasyonu alanı" : stage == 5 ? "Batarya–şarj bağlantısı" : "Kendi şarj istasyonun";
    public string StepText => complete ? "Başlangıç tamam • Enerji ağını kurdun"
        : stage == 0 ? "Başlangıç 1/7 · " + EcoKeys.Tag(EcoKeys.Action.Build) + " ile inşa modunu aç"
        : stage == 1 ? "Başlangıç 2/7 · [1] Güneş panelini kur · 12 metal, 6 plastik"
        : stage == 2 ? "Başlangıç 3/7 · [3] Bataryayı panelin yakınına kur · 8 metal, 6 plastik"
        : stage == 3 ? "Başlangıç 4/7 · Panel ile bataryayı elektrik kablosuyla bağla"
        : stage == 4 ? "Başlangıç 5/7 · [4] Şarj istasyonunu bataryanın yanına kur · 6 metal, 4 plastik"
        : stage == 5 ? "Başlangıç 6/7 · Bataryayı şarj istasyonuna kabloyla bağla"
        : "Başlangıç 7/7 · Şarj istasyonunda " + EcoKeys.Tag(EcoKeys.Action.Interact) + " · " + chargedEnergy.ToString("0.0") + "/10 EB";
    public string Instructions
    {
        get
        {
            string build = EcoKeys.Tag(EcoKeys.Action.Build), interact = EcoKeys.Tag(EcoKeys.Action.Interact);
            string cable = "[Tab] ile 3. sayfaya geç, [1] Kablo'yu seç. Önce ilk yapıya, sonra ikinciye sol tıkla. Kablo metre başına 1 metal + 1 plastik harcar.";
            switch (stage)
            {
                case 0: return "1. " + build + " ile inşa modunu aç.\n2. Başlangıç çantandaki 50 metal ve 40 plastik bu ağ için yeterli. Yapıları birbirine yakın kur.\n3. İlk olarak güneş panelini seçeceğiz.";
                case 1: return "1. İlk inşa sayfasında [1] Güneş panelini seç.\n2. İşaretlenen açık, düz toprağa sol tıkla. Kırmızı önizleme yerleşimin uygun olmadığını gösterir.\n3. " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " döndürür. Ağaç ve bina gölgesinden uzak dur.";
                case 2: return "1. İlk sayfada [3] Bataryayı seç.\n2. Panelin yaklaşık 4 metre yanına sol tıkla; yapılar çakışmasın.\n3. Batarya fazla elektriği depolar; yeni kurulduğunda boştur.";
                case 3: return "1. " + cable + "\n2. Güneş panelini ve bataryayı bağla. [Sağ tık] yarım kalan bağlantıyı iptal eder.";
                case 4: return "1. [Tab] ile ilk sayfaya dön; [4] Şarj istasyonunu seç.\n2. Bataryaya yakın, yürüyerek ulaşabileceğin düz toprağa kur.\n3. Bu istasyon ağdaki elektriği aracına aktaracak.";
                case 5: return "1. " + cable + "\n2. Bataryayı şarj istasyonuna bağla. Panel, batarya ve istasyon aynı ağda olmalı.";
                case 6: return "1. " + build + " ile inşa modundan çık ve kendi şarj istasyonuna yaklaş.\n2. " + interact + " ile şarjı başlat; toplam 10 EB al.\n3. Gündüz güneş üretir. Üretim yoksa panelin gölgesini ve kabloları kontrol et; dolu batarya güneşsiz zamanda besler.\n4. Aracın doluysa önce biraz kullanıp şarj için yer aç.";
                default: return "Enerji ağını kurdun. Proje günlüğündeki ilk enerji koşullarını sürdürerek mahalleyi geliştirmeye devam et.";
            }
        }
    }
}
