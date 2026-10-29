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
