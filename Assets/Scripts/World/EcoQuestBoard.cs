using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Quest board: every open job in the world, grouped by region and ordered easy → hard. The list is read from the live
// world (waste, thirsty crops, fires, threats, inlets, factories, the region's project), so a quest closes by itself
// when its job is done. The player picks one to track; the objective card and the guide arrow then follow it.
public static class EcoQuestBoard
{
    public static readonly string[] LevelNames = { "", "Kolay", "Orta", "Zor" };

    public sealed class Quest
    {
        public string id, title, detail, icon;
        public int level;
        public EcoRegion region;
        public Vector3? target;
        public float progress = -1f;
    }

    public static string TrackedId { get; private set; } = "";
    private static string trackedTitle = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { TrackedId = ""; trackedTitle = ""; LastFinished = ""; FinishedCount = 0; }

    public static bool IsTracked(Quest quest) => quest != null && quest.id == TrackedId;
    /// <summary>Tracks the quest, or stops tracking it when it is already tracked.</summary>
    public static void Toggle(Quest quest)
    {
        if (quest == null) return;
        if (IsTracked(quest)) { TrackedId = ""; trackedTitle = ""; return; }
        TrackedId = quest.id; trackedTitle = quest.title;
    }

    /// <summary>Counts finished tracked quests; the HUD compares it with what it last saw to announce <see cref="LastFinished"/>.</summary>
    public static int FinishedCount { get; private set; }
    public static string LastFinished { get; private set; } = "";

    /// <summary>The tracked quest as it stands now. When its job is done the quest is gone from the world and tracking ends.</summary>
    public static Quest Tracked(Vector3 from)
    {
        if (string.IsNullOrEmpty(TrackedId)) return null;
        var quest = All(from).FirstOrDefault(q => q.id == TrackedId);
        if (quest == null) { LastFinished = trackedTitle; FinishedCount++; TrackedId = ""; trackedTitle = ""; }
        return quest;
    }

    /// <summary>Open quests of one region, easy first.</summary>
    public static List<Quest> For(EcoRegion region, Vector3 from) => region == null ? new List<Quest>() : All(from).Where(q => q.region == region).ToList();

    /// <summary>Action instructions shared by the sidebar and quest board; reading them never changes progress or hints.</summary>
    public static string HowTo(Quest quest)
    {
        if (quest == null || string.IsNullOrEmpty(quest.id)) return "Bir görev seç; yapılacak adımlar burada görünsün.";
        string interact = EcoKeys.Tag(EcoKeys.Action.Interact), mode = EcoKeys.Tag(EcoKeys.Action.Mode);
        string rotate = EcoKeys.Tag(EcoKeys.Action.Rotate);
        int separator = quest.id.IndexOf(':');
        string kind = separator < 0 ? quest.id : quest.id.Substring(0, separator);
        switch (kind)
        {
            case "harvest":
                return "1. İşaretlenen olgun bitkinin yanına yaklaş.\n2. " + interact + " ile hasat et; ürünü ve tohumları al.\n3. Bölgedeki diğer olgun ürünler için tekrarla.";
            case "water":
                return "1. Depoya temiz su doldur; kirli su bitkiye zarar verir.\n2. Susuz fidanın yanında " + interact + " ile sula.\n3. Alternatif: [1] vakumda " + mode + " ile Su modunu seç ve fidana tut.";
            case "waste":
                return "1. [1] ile vakumu seç ve atıklara yaklaş.\n2. Atıkları yarı saydam etki alanına al; sol tuşu basılı tut. Nesnenin kenarına tam tıklaman gerekmez.\n3. Toplanan ham atığı [3] geri dönüşüm aracında " + rotate + " ile işle.";
            case "fire":
                return "1. Temiz su deposunu doldur ve yangına yaklaş.\n2. [1] vakumda " + mode + " ile Su modunu seç.\n3. Alevlere sol tuşu basılı tut; yangın sönene kadar su uygula.";
            case "threat":
                return "1. Uygun aracı seç: vakum çoğu tehdide uygun modu kendisi seçer.\n2. Düşmana bir kez tıkla; otomatik saldırı sürer. Sağ tıkla bırak.\n3. Teneke için [2] aracında " + mode + " ile Demir top modunu seç.\n4. Teneke sersemleyince yaklaş ve " + interact + " ile küçük filtre tak; filtreyi marketten edin.";
            case "inlet":
                return "1. Onarım için 10 metal ve 5 plastik hazırla.\n2. İşaretlenen kirletici girişinin yanına git.\n3. " + interact + " ile girişi kapat. Mevcut kirli suyu temizlemek için arıtmayı sürdür.";
            case "filter":
                return "1. Marketten büyük hava filtresi edin.\n2. Filtresiz fabrikanın yanına yaklaş.\n3. " + interact + " ile filtre tak. Filtre hava kirliliğini azaltır; karbon için ayrıca temiz enerji dönüşümü gerekir.";
            case "heavy":
                return "1. Geri dönüşüm aracının işleme kademesini görevde belirtilen seviyeye yükselt.\n2. [3] ile geri dönüşüm aracını seç.\n3. Büyük atığı etki alanına al ve sol tuşu işlem bitene kadar basılı tut.";
            case "station":
                return "1. Buzuldaki izleme istasyonuna git.\n2. Yanında " + interact + " ile istasyonu etkinleştir.\n3. İstasyon ölçüm yapar; karbonu azaltmak için temiz enerji ve habitat projelerini sürdür.";
            case "trees":
                return "1. [2] aracını seç; Demir top açıksa " + mode + " ile tohum moduna dön.\n2. " + rotate + " ile Ağaç tohumunu seç; deponda yoksa marketten edin.\n3. Açık, uygun toprağa sol tıkla ekim yap; fidanlar arasında boşluk bırak.\n4. Temiz suyla sula ve büyürken bakımını sürdür.";
            case "project":
                int index;
                var projects = EcoProjects.Current;
                if (separator >= 0 && int.TryParse(quest.id.Substring(separator + 1), out index)
                    && projects != null && index >= 0 && index < projects.All.Count)
                {
                    var project = projects.All[index];
                    projects.Step(index, out _, out var location);
                    return "1. " + (string.IsNullOrEmpty(location) ? "Projenin çalışma alanına git." : location + " noktasına git.")
                        + "\n2. " + project.action + "\n3. Tamamlanma şartı: " + project.goal
                        + "\n4. Koşulları kesintisiz 120 oyun saniyesi koru. " + EcoKeys.Tag(EcoKeys.Action.Journal) + " ile proje günlüğünü açabilirsin.";
                }
                break;
        }
        return "1. Görev işaretine doğru ilerle.\n2. Üstteki güncel görev adımını uygula.\n3. İş tamamlandığında görev listesi kendiliğinden güncellenir.";
    }

    /// <summary>All open quests: the player's region first, then the nearest regions; inside a region easy → hard.</summary>
    public static List<Quest> All(Vector3 from)
    {
        var quests = new List<Quest>();
        var regions = Object.FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID).Where(r => r != null).ToArray();
        if (regions.Length == 0) return quests;
        var waste = Object.FindObjectsByType<RecyclableResource>(FindObjectsSortMode.None);
        var crops = Object.FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None);
        var fires = Object.FindObjectsByType<EcoFire>(FindObjectsSortMode.None);
        var enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);
        var inlets = Object.FindObjectsByType<EcoPollutionInlet>(FindObjectsSortMode.None);
        var factories = Object.FindObjectsByType<EcoFactory>(FindObjectsSortMode.None);
        var station = EcoGlacierStation.Current;
        var projects = EcoProjects.Current;
        var saving = EcoHomeSaving.Current;
        var tanks = Object.FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None).Where(q => q != null && q.kind == EcoWaterNode.Kind.Tank && q.leaking).ToArray();
        string interact = EcoKeys.Tag(EcoKeys.Action.Interact);

        foreach (var region in regions)
        {
            // Things inside an overlapping pocket (the base inside the plain) belong to the pocket, so nothing counts twice.
            bool Here(Component c) => c != null && EcoRegion.At(c.transform.position) == region;
            string key = region.Key;

            var mature = crops.Where(c => Here(c) && !c.IsWithered && c.IsMature && c.kind != EcoCropCatalog.Kind.Tree).ToArray();
            if (mature.Length > 0)
                quests.Add(new Quest { id = "harvest:" + key, level = 1, icon = "food", region = region, title = "Olgun ürünü hasat et",
                    detail = mature.Length + " ürün hazır · yanında " + interact, target = Nearest(mature, from) });
            var thirsty = crops.Where(c => Here(c) && c.NeedsWater).ToArray();
            if (thirsty.Length > 0)
                quests.Add(new Quest { id = "water:" + key, level = 1, icon = "water", region = region, title = "Fidanları sula",
                    detail = thirsty.Length + " fidan susuz · yanında " + interact, target = Nearest(thirsty, from) });
            var litter = waste.Where(w => Here(w) && w.requiredRecyclerRank <= 0).ToArray();
            if (litter.Length > 0)
                quests.Add(new Quest { id = "waste:" + key, level = 1, icon = "scrap", region = region, title = "Atıkları topla",
                    detail = litter.Length + " atık · [1] vakumla nişan al", target = Nearest(litter, from) });

            var burning = fires.Where(f => Here(f) && f.Burning).ToArray();
            if (burning.Length > 0)
                quests.Add(new Quest { id = "fire:" + key, level = 2, icon = "poi_fire", region = region, title = "Yangını söndür",
                    detail = burning.Length + " yangın · vakumla nişan al", target = Nearest(burning, from) });
            var threats = enemies.Where(e => Here(e) && e.Health > 0f && e.State != EnemyBrain.Behaviour.Dead).ToArray();
            if (threats.Length > 0)
                quests.Add(new Quest { id = "threat:" + key, level = 2, icon = "poi_enemy", region = region, title = "Tehditleri uzaklaştır",
                    detail = threats.Length + " tehdit · vakum moduna kendisi geçer; Teneke'ye [2] demir top", target = Nearest(threats, from) });
            foreach (var inlet in inlets)
                if (Here(inlet) && !inlet.Remediated)
                    quests.Add(new Quest { id = "inlet:" + key, level = 2, icon = "poi_inlet", region = region, title = "Kirletici girişi kapat",
                        detail = "Yanında " + interact + " · 10 metal + 5 plastik", target = inlet.transform.position });
            var local = factories.Where(Here).ToArray();
            var unfiltered = local.Where(f => !f.Filtered).ToArray();
            if (unfiltered.Length > 0)
                quests.Add(new Quest { id = "filter:" + key, level = 2, icon = "poi_factory", region = region, title = "Fabrikalara filtre tak",
                    detail = (local.Length - unfiltered.Length) + "/" + local.Length + " filtreli · fabrikada " + interact,
                    target = Nearest(unfiltered, from), progress = (local.Length - unfiltered.Length) / (float)local.Length });
            var heavy = waste.Where(w => Here(w) && w.requiredRecyclerRank > 0).ToArray();
            if (heavy.Length > 0)
                quests.Add(new Quest { id = "heavy:" + key, level = 2, icon = "tool_recycle", region = region, title = "Büyük atığı geri dönüştür",
                    detail = heavy.Length + " büyük atık · [3] geri dönüşüm, işleme kademesi " + heavy.Min(w => w.requiredRecyclerRank), target = Nearest(heavy, from) });
            if (station != null && Here(station) && !station.Active)
                quests.Add(new Quest { id = "station:" + key, level = 2, icon = "poi_station", region = region, title = "İzleme istasyonunu etkinleştir",
                    detail = "İstasyonun yanında " + interact, target = station.transform.position });
            var info = region.GetComponent<EcoRegionInfo>();
            if (info != null && (info.kind == EcoRegionInfo.Kind.Grove || info.kind == EcoRegionInfo.Kind.Wind) && region.vegetation < 0.6f)
                quests.Add(new Quest { id = "trees:" + key, level = 2, icon = "crop_tree", region = region, title = "Ağaç dik",
                    detail = "Bitki örtüsü %" + Mathf.RoundToInt(region.vegetation * 100f) + "/60 · [2] ağaç tohumu, açık toprağa",
                    target = region.transform.position, progress = region.vegetation / 0.6f });

            // Demand side: the home meter's saving measures and leaking tanks (EcoHomeSaving).
            if (saving != null && saving.Meter != null && !saving.AllDone && EcoRegion.At(saving.Meter.position) == region)
                quests.Add(new Quest { id = "saving:home", level = saving.FreeMeasuresDone ? 2 : 1, icon = "electricity", region = region,
                    title = saving.FreeMeasuresDone ? "Evde verimli donanım" : "Evde enerji tasarrufu",
                    detail = "Ev sayacında " + interact + " · önlemler " + saving.DoneCount + "/4", target = saving.Meter.position, progress = saving.DoneCount / 4f });
            var leaks = tanks.Where(Here).ToArray();
            if (leaks.Length > 0)
                quests.Add(new Quest { id = "leak:" + key, level = 1, icon = "water", region = region, title = "Sızdıran depoyu tamir et",
                    detail = leaks.Length + " depo su kaybediyor · yanında " + interact + " (1 metal)", target = Nearest(leaks, from) });

            if (info != null && projects != null && info.projectIndex >= 0 && info.projectIndex < projects.All.Count && !projects.All[info.projectIndex].done)
            {
                var project = projects.All[info.projectIndex];
                string step = projects.Step(info.projectIndex, out var goal, out _);
                // The title already names the project: keep only the step after "Name · ", and the concrete action
                // instead of the generic "see the journal" line.
                if (step.StartsWith("Proje:")) step = project.action;
                else { int cut = step.IndexOf(" · "); if (cut > 0 && cut < 32) step = step.Substring(cut + 3); }
                quests.Add(new Quest { id = "project:" + info.projectIndex, level = 3, icon = "poi_project", region = region, title = "Proje: " + project.name,
                    detail = step, target = goal ?? region.transform.position, progress = project.held / EcoProjects.VerifySeconds });
            }
        }

        var home = EcoRegion.At(from);
        return quests.OrderBy(q => q.region == home ? 0 : 1)
            .ThenBy(q => (q.region.transform.position - from).sqrMagnitude)
            .ThenBy(q => q.level).ToList();
    }

    private static Vector3? Nearest(IEnumerable<Component> items, Vector3 from)
    {
        Vector3? best = null; float distance = float.MaxValue;
        foreach (var item in items)
        {
            float d = (item.transform.position - from).sqrMagnitude;
            if (d < distance) { distance = d; best = item.transform.position; }
        }
        return best;
    }
}
