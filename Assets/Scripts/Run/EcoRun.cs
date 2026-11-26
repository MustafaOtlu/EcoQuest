using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>One run: a rolled valley, three goals and five game days. Mornings offer a card, evenings bring a crisis,
/// night five is the storm (EcoFinale), and the sixth dawn grades the valley and pays green credits (EcoMeta).
/// The run lives on the EcoProjects object and is saved as a global hook (CaptureState / RestoreState).</summary>
[DefaultExecutionOrder(20)]
public sealed class EcoRun : MonoBehaviour
{
    public const int Days = 5;
    public const float CollapseHealth = 25f, GoalHoldSeconds = 120f;
    public enum Result { Running, Success, Failed, Collapsed }

    [Serializable]
    public sealed class RunState
    {
        public bool active, training, finaleWon;
        public int seed, difficulty, startDay = 1, lastMorningDay, lastCrisisDay, lastWarningDay, lowHealthDays, fireGuardDay = -1, rerollDay = -1;
        public bool patrol;
        public float playSeconds;
        // Start cover per region key: the cap for wild-tree regrowth (EcoRegion.wildCover), kept across loads.
        public List<string> coverKeys = new List<string>();
        public List<float> coverValues = new List<float>();
        public int result, score, credits;
        public string grade = "";
        public float solarBoost = 1f, windBoost = 1f, recoveryBoost = 1f;
        public List<EcoGoalState> goals = new List<EcoGoalState>();
        public List<string> cards = new List<string>(), crises = new List<string>();
        public List<float> health = new List<float>();
        public string[] offer = new string[0];
        public bool offerOpen;
    }

    public static EcoRun Current { get; private set; }
    public RunState State { get; private set; } = new RunState();
    public EcoScenario Scenario { get; private set; }
    public bool Active => State.active;
    public bool Ended => State.result != (int)Result.Running;
    public Result Outcome => (Result)State.result;
    public IReadOnlyList<EcoGoalState> Goals => State.goals;
    public int RunDay => Clock != null ? Clock.day - State.startDay + 1 : 1;
    /// <summary>Night five: the storm terminal opens on the last day.</summary>
    public bool FinaleOpen => Active && !Ended && RunDay >= Days;
    public string LastCrisis { get; private set; } = "";
    public string LastCrisisResult { get; private set; } = "";
    public string CardFeedback { get; private set; } = "";
    public float Health { get; private set; }

    // Pending run shown on the briefing before anything in the world changes.
    public EcoScenario Pending { get; private set; }
    public List<EcoGoalState> PendingGoals { get; private set; } = new List<EcoGoalState>();

    public event Action RunStarted, RunEnded;
    public event Action<int> DayEnded;

    public static float SolarFactor => Current != null && Current.Active && Current.Scenario != null ? Current.State.solarBoost * Current.Scenario.solarYield : 1f;
    public static float WindFactor => Current != null && Current.Active && Current.Scenario != null ? Current.State.windBoost * Current.Scenario.windYield : 1f;
    /// <summary>GDD v5 §6: in a rolled valley nature heals at about a third of the v4 rate, so a valley left alone does
    /// not fix itself in five days; the training valley keeps the gentle v4 rate.</summary>
    public const float NaturalRecovery = 0.35f;
    public static float RecoveryFactor => Current != null && Current.Active ? Current.State.recoveryBoost * (Current.State.training ? 1f : NaturalRecovery) : 1f;

    private static bool autoStart;
    /// <summary>"Yeni vadi" from the result screen reloads the scene and skips the title straight into a briefing.</summary>
    public static void RequestAutoStart() => autoStart = true;
    public static bool ConsumeAutoStart() { bool value = autoStart; autoStart = false; return value; }

    private EcoWorldClock clock;
    private EcoWorldClock Clock => clock != null ? clock : clock = EcoWorldClock.Current;
    private EcoRunUI ui;
    private float accumulated, nextScan;
    private EcoFire fireTemplate;

    private void Awake()
    {
        Current = this;
        ui = GetComponent<EcoRunUI>();
        if (ui == null) ui = gameObject.AddComponent<EcoRunUI>();
        ui.Bind(this);
    }
    private void OnDestroy() { if (Current == this) Current = null; }

    // ================================================================== starting

    /// <summary>Title "Yeni oyun": roll a valley and show the briefing. The world changes only on "Başla".</summary>
    public void RequestNewRun()
    {
        bool first = EcoMeta.Current.runs == 0;
        Pending = first ? EcoScenario.Training() : EcoScenario.Roll(NewSeed(), EcoDifficulty.Normal);
        PendingGoals = EcoGoalCatalog.Pick(Pending, PendingKinds());
        ui.ShowBriefing();
    }
    public void SetPendingDifficulty(EcoDifficulty difficulty)
    {
        if (Pending == null || Pending.training) return;
        Pending = EcoScenario.Roll(Pending.seed, difficulty); PendingGoals = EcoGoalCatalog.Pick(Pending, PendingKinds());
    }
    public void RerollPending()
    {
        if (Pending == null || Pending.training) return;
        Pending = EcoScenario.Roll(NewSeed(), Pending.difficulty); PendingGoals = EcoGoalCatalog.Pick(Pending, PendingKinds());
    }
    /// <summary>Lets a returning player skip the training valley.</summary>
    public void SkipTraining()
    {
        Pending = EcoScenario.Roll(NewSeed(), EcoDifficulty.Normal); PendingGoals = EcoGoalCatalog.Pick(Pending, PendingKinds());
    }
    private static int NewSeed() => (int)(DateTime.UtcNow.Ticks % 2000000000L) ^ UnityEngine.Random.Range(1, 99999);

    /// <summary>Goals are picked from the regions the pending valley will have, not the one on screen.</summary>
    private HashSet<EcoRegionInfo.Kind> PendingKinds() => EcoValleyAssembler.Current != null ? EcoValley.Kinds(EcoValley.Layout(Pending)) : PresentKinds();

    public void BeginPending()
    {
        if (Pending == null) return;
        // Another valley layout: reload the scene into it; the run starts there as soon as the world exists.
        var assembler = EcoValleyAssembler.Current;
        string code = EcoValley.PlanCode(Pending);
        if (assembler != null && code != assembler.Code)
        {
            pendingStart = Pending; Pending = null;
            EcoValley.Request(code); EcoValley.SkipTitle = true;
            PlayerController.SetPaused(false);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            return;
        }
        Begin(Pending, PendingGoals, true);
        Pending = null;
    }

    private static EcoScenario pendingStart;
    private IEnumerator Start()
    {
        if (pendingStart == null) yield break;
        var scenario = pendingStart; pendingStart = null;
        yield return null; yield return null; // regions, clock and the save system finish starting first
        Begin(scenario, EcoGoalCatalog.Pick(scenario, PresentKinds()), true);
    }

    private void Begin(EcoScenario scenario, List<EcoGoalState> goals, bool applyWorld)
    {
        Scenario = scenario;
        State = new RunState { active = true, training = scenario.training, seed = scenario.seed, difficulty = (int)scenario.difficulty,
            goals = goals.Select(g => new EcoGoalState { id = g.id, required = g.required, threshold = g.threshold }).ToList() };
        if (Clock != null)
        {
            if (applyWorld) { Clock.day = 1; Clock.hour = 7f; }
            State.startDay = Clock.day; State.lastMorningDay = Clock.day;
        }
        if (applyWorld) { ApplyScenario(scenario); TightenGoals(scenario); }
        EcoPlaytestLog.Objective("Run başladı: " + scenario.valleyName + " · " + EcoScenario.DifficultyNames[(int)scenario.difficulty] + " · tohum " + scenario.seed);
        RunStarted?.Invoke();
        OfferCards();
    }

    // A target the valley already meets on arrival is no goal. Once the rolled values are in the world, each measured
    // goal is set to at least a fixed step better than where this valley starts (never easier than the catalogue).
    private void TightenGoals(EcoScenario scenario)
    {
        if (scenario.training) return;
        int level = (int)scenario.difficulty;
        float share = new[] { 0.8f, 0.7f, 0.6f }[level];
        foreach (var goal in State.goals)
        {
            var spec = goal.Spec;
            if (spec == null) continue;
            float before = goal.threshold;
            switch (spec.metric)
            {
                case EcoGoalMetric.WorldCarbon:
                    goal.threshold = Mathf.Min(goal.threshold, Mathf.Max(3f, Mathf.Round(EcoRegion.WorldCarbon(true) * share)));
                    break;
                case EcoGoalMetric.IndustryAqi:
                {
                    var region = RegionOf(EcoRegionInfo.Kind.Industry);
                    if (region != null) goal.threshold = Mathf.Min(goal.threshold, Mathf.Max(25f, Mathf.Round(region.AQI * share / 5f) * 5f));
                    break;
                }
                case EcoGoalMetric.ValleyHealth:
                    goal.threshold = Mathf.Max(goal.threshold, Mathf.Min(90f, Mathf.Round(ValleyHealth() + 8f + 4f * level)));
                    break;
                case EcoGoalMetric.LakeOxygen:
                {
                    var lake = RegionOf(EcoRegionInfo.Kind.Lake);
                    if (lake != null) goal.threshold = Mathf.Max(goal.threshold, Mathf.Min(9f, Mathf.Round((lake.DissolvedOxygen + 1f + 0.5f * level) * 2f) / 2f));
                    break;
                }
                case EcoGoalMetric.GroveCover:
                {
                    var grove = RegionOf(EcoRegionInfo.Kind.Grove) ?? RegionOf(EcoRegionInfo.Kind.Wind);
                    if (grove != null) goal.threshold = Mathf.Max(goal.threshold, Mathf.Min(0.9f, Mathf.Round((grove.vegetation + 0.16f + 0.04f * level) * 20f) / 20f));
                    break;
                }
            }
            if (!Mathf.Approximately(before, goal.threshold))
                EcoPlaytestLog.Objective("Hedef eşiği vadiye göre ayarlandı: " + spec.title + " " + before.ToString("0.##") + " → " + goal.threshold.ToString("0.##"));
        }
    }

    private void ApplyScenario(EcoScenario scenario)
    {
        if (!scenario.training)
            foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None))
            {
                if (!region.TryGetComponent<EcoRegionInfo>(out var info)) continue;
                region.airPollution = scenario.Pollution(info.kind, region.airPollution);
                region.waterPollution = scenario.Pollution(info.kind, region.waterPollution);
                region.soilPollution = scenario.Pollution(info.kind, region.soilPollution);
                region.carbon = scenario.Pollution(info.kind, region.carbon);
                region.vegetation = scenario.Vegetation(info.kind, region.vegetation);
                region.wildCover = region.vegetation;
                State.coverKeys.Add(region.Key); State.coverValues.Add(region.vegetation);
            }
        SpawnFires(scenario.fires, EcoRegionInfo.Kind.Grove, EcoRegionInfo.Kind.Wind);
        SpawnWaste(scenario.wastePiles, EcoRegionInfo.Kind.City, EcoRegionInfo.Kind.Sun, EcoRegionInfo.Kind.Industry, EcoRegionInfo.Kind.Lake);
        if (!scenario.training) ScatterLitter();
        var player = FindFirstObjectByType<PlayerWeaponSystem>();
        if (player != null)
        {
            int grant = EcoMeta.Has("start_grant") ? 20 : 0;
            player.ReturnMaterials(scenario.startMetal + grant, scenario.startPlastic + grant);
            if (EcoMeta.Has("seed_bank")) player.AddSeeds(EcoCropCatalog.Kind.Tree, 10);
            if (EcoMeta.Has("blueprints")) EcoDiscoveries.Remember("recipe.water");
            if (EcoMeta.Has("energy_plans")) EcoDiscoveries.Remember("plan.energy");
        }
    }

    // ================================================================== loop

    private void Update()
    {
        if (!Active || Ended || Clock == null) return;
        if (!State.training && EcoTraffic.Current == null) EcoTraffic.Ensure();
        var save = EcoWorldSave.Current;
        if (save != null && save.IsLoading) return;
        accumulated += Time.deltaTime;
        if (!PlayerController.Paused) State.playSeconds += Time.unscaledDeltaTime;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            float seconds = accumulated; accumulated = 0f;
            Health = ValleyHealth();
            foreach (var goal in State.goals) Evaluate(goal, seconds);
        }
        if (PlayerController.Paused || EcoUIFlow.Paused || ui.Busy) return;
        if (State.offerOpen) { if (!EcoUIFlow.IsOpen) ui.ShowCards(); return; }

        int day = Clock.day; float hour = Clock.hour;
        if (day > State.lastMorningDay && hour >= EcoWorldClock.Sunrise) { Morning(); return; }
        if (RunDay > Days) return;
        if (EcoMeta.Has("early_warning") && hour >= 12f && hour < 18f && State.lastWarningDay < day)
        {
            State.lastWarningDay = day;
            var warn = EcoCrisis.Find(NextCrisis());
            FindFirstObjectByType<PlayerWeaponSystem>()?.ShowFeedback("Erken uyarı · bu akşam: " + warn.title);
        }
        if (hour >= 18f && State.lastCrisisDay < day && !EcoMarketUI.IsOpen) Evening();
    }

    private string NextCrisis() => EcoCrisis.Draw(State.seed, RunDay, Scenario.problem, State.crises.Count > 0 ? State.crises[State.crises.Count - 1] : "");

    private void Evening()
    {
        State.lastCrisisDay = Clock.day;
        string id = NextCrisis();
        State.crises.Add(id);
        LastCrisis = id;
        LastCrisisResult = EcoCrisis.Apply(id, this, RunDay);
        EcoPlaytestLog.Objective("Kriz (gün " + RunDay + "): " + EcoCrisis.Find(id).title + " · " + LastCrisisResult);
        ui.ShowCrisis();
        EcoWorldSave.Current?.RequestAutosave();
    }

    private void Morning()
    {
        int finished = RunDay - 1;
        State.lastMorningDay = Clock.day;
        Health = ValleyHealth();
        State.health.Add(Health);
        State.lowHealthDays = Health < CollapseHealth ? State.lowHealthDays + 1 : 0;
        DayEnded?.Invoke(finished);
        if (State.lowHealthDays >= 2) { End(Result.Collapsed); return; }
        if (RunDay > Days)
        {
            if (EcoFinale.Current != null && EcoFinale.Current.Fighting) { State.lastMorningDay = Clock.day - 1; return; } // finish the storm first
            End(State.goals.Any(g => g.required && g.done) ? Result.Success : Result.Failed);
            return;
        }
        OfferCards();
        EcoWorldSave.Current?.RequestAutosave();
    }

    private void OfferCards()
    {
        int count = EcoMeta.Has("fourth_card") ? 4 : 3;
        State.offer = EcoDayCards.Draw(State.seed, RunDay, Scenario.problem, State.cards, count);
        State.offerOpen = State.offer.Length > 0;
        CardFeedback = "";
        if (State.offerOpen) ui.ShowCards();
    }

    public bool CanReroll => State.offerOpen && EcoMeta.Has("card_reroll") && State.rerollDay != RunDay;

    /// <summary>Meta unlock: once a day the morning hand can be swapped for a fresh one (deterministic per seed and day).</summary>
    public bool RerollCards()
    {
        if (!CanReroll) return false;
        State.rerollDay = RunDay;
        var taken = new List<string>(State.cards); taken.AddRange(State.offer.Where(id => !(EcoDayCards.Find(id)?.repeatable ?? true)));
        var fresh = EcoDayCards.Draw(unchecked(State.seed * 7 + 4099), RunDay, Scenario.problem, taken, State.offer.Length);
        if (fresh.Length == 0) return false;
        State.offer = fresh; CardFeedback = "Kartlar yenilendi.";
        return true;
    }

    /// <summary>Morning pick. Returns false when the card cannot be paid for.</summary>
    public bool ChooseCard(string id)
    {
        if (!State.offerOpen || !State.offer.Contains(id)) return false;
        string feedback = EcoDayCards.Apply(id, this);
        if (feedback == null) { CardFeedback = "Bu kartın bedeli karşılanamıyor."; return false; }
        State.cards.Add(id); State.offerOpen = false; State.offer = new string[0];
        CardFeedback = feedback;
        FindFirstObjectByType<PlayerWeaponSystem>()?.ShowFeedback(feedback);
        EcoPlaytestLog.Objective("Kart (gün " + RunDay + "): " + EcoDayCards.Find(id).title);
        return true;
    }

    // ================================================================== goals

    private void Evaluate(EcoGoalState goal, float seconds)
    {
        var spec = goal.Spec;
        if (spec == null) return;
        if (spec.metric == EcoGoalMetric.Project)
        {
            var projects = EcoProjects.Current;
            if (projects == null || spec.project < 0 || spec.project >= projects.All.Count) return;
            var project = projects.All[spec.project];
            goal.met = project.met; goal.status = project.status;
            goal.progress = project.done ? 1f : project.held / EcoProjects.VerifySeconds;
            if (project.done && !goal.done) Complete(goal);
            return;
        }
        goal.met = Measure(spec, goal.threshold, out goal.status, out goal.progress);
        if (goal.done) return;
        goal.held = goal.met ? Mathf.Min(GoalHoldSeconds, goal.held + seconds) : 0f;
        if (goal.held >= GoalHoldSeconds) Complete(goal);
    }

    private void Complete(EcoGoalState goal)
    {
        goal.done = true; goal.progress = 1f;
        var spec = goal.Spec;
        FindFirstObjectByType<PlayerWeaponSystem>()?.ShowFeedback((goal.required ? "HEDEF TAMAMLANDI • " : "BONUS HEDEF • ") + spec.title);
        EcoPlaytestLog.Objective("Hedef tamamlandı: " + spec.title + " (gün " + RunDay + ")");
        EcoWorldSave.Current?.RequestAutosave();
    }

    private static bool Measure(EcoGoalSpec spec, float t, out string status, out float progress)
    {
        status = ""; progress = 0f;
        switch (spec.metric)
        {
            case EcoGoalMetric.LakeOxygen:
            {
                var lake = RegionOf(EcoRegionInfo.Kind.Lake);
                if (lake == null) { status = "Göl yok"; return false; }
                float value = lake.DissolvedOxygen;
                status = "Oksijen " + value.ToString("0.0") + " / " + t.ToString("0.0") + " mg/L";
                progress = Mathf.Clamp01(value / t); return value >= t;
            }
            case EcoGoalMetric.IndustryAqi:
            {
                var industry = RegionOf(EcoRegionInfo.Kind.Industry);
                if (industry == null) { status = "Sanayi yok"; return false; }
                int aqi = industry.AQI;
                status = "AQI " + aqi + " / en çok " + t.ToString("0");
                progress = Mathf.Clamp01(t / Mathf.Max(1f, aqi)); return aqi <= t;
            }
            case EcoGoalMetric.GroveCover:
            {
                var grove = RegionOf(EcoRegionInfo.Kind.Grove) ?? RegionOf(EcoRegionInfo.Kind.Wind);
                if (grove == null) { status = "Orman yok"; return false; }
                float habitat = grove.TryGetComponent<EcoHabitat>(out var h) ? h.recovery : 0f, need = EcoGoalSpec.GroveHabitat(t);
                bool burning = FindObjectsByType<EcoFire>(FindObjectsSortMode.None).Any(f => f.Burning && grove.Contains(f.transform.position));
                status = "Örtü %" + (grove.vegetation * 100f).ToString("0") + "/" + (t * 100f).ToString("0") + " · Habitat %" + (habitat * 100f).ToString("0") + "/" + (need * 100f).ToString("0") + (burning ? " · yangın var" : "");
                progress = Mathf.Clamp01(Mathf.Min(grove.vegetation / t, habitat / Mathf.Max(0.01f, need))) * (burning ? 0.5f : 1f);
                return !burning && grove.vegetation >= t && habitat >= need;
            }
            case EcoGoalMetric.WorldCarbon:
            {
                float carbon = EcoRegion.WorldCarbon();
                status = "CO₂ " + (425f + carbon * 2.5f).ToString("0") + " / en çok " + (425f + t * 2.5f).ToString("0") + " ppm";
                progress = Mathf.Clamp01(t / Mathf.Max(0.1f, carbon)); return carbon <= t;
            }
            case EcoGoalMetric.CleanGenerators:
            {
                int count = FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None).Count(n => n.Operational
                    && (n.kind == EcoPowerNode.Kind.Solar || n.kind == EcoPowerNode.Kind.Wind || n.kind == EcoPowerNode.Kind.Hydro)
                    && n.TryGetComponent<EcoStructure>(out var s) && s.playerBuilt);
                status = "Üretici " + count + " / " + t.ToString("0");
                progress = Mathf.Clamp01(count / Mathf.Max(1f, t)); return count >= t;
            }
            case EcoGoalMetric.FishStock:
            {
                var spots = FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None);
                float average = spots.Length == 0 ? 0f : spots.Average(s => s.population);
                status = "Balık stoku " + average.ToString("0.0") + " / " + t.ToString("0");
                progress = Mathf.Clamp01(average / t); return spots.Length > 0 && average >= t;
            }
            case EcoGoalMetric.WasteCleared:
            {
                int left = WastePiles();
                status = "Kalan yığın " + left + " / en çok " + t.ToString("0");
                progress = left <= t ? 1f : Mathf.Clamp01(1f - (left - t) / 12f); return left <= t;
            }
            case EcoGoalMetric.TreesGrowing:
            {
                int count = PlantedTrees();
                status = "Ağaç " + count + " / " + t.ToString("0");
                progress = Mathf.Clamp01(count / Mathf.Max(1f, t)); return count >= t;
            }
            case EcoGoalMetric.WindGenerators:
            {
                var hills = RegionOf(EcoRegionInfo.Kind.Wind);
                int count = FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None).Count(n => n.Operational && n.kind == EcoPowerNode.Kind.Wind
                    && n.TryGetComponent<EcoStructure>(out var s) && s.playerBuilt && (hills == null || hills.Contains(n.transform.position)));
                status = "Türbin " + count + " / " + t.ToString("0");
                progress = Mathf.Clamp01(count / Mathf.Max(1f, t)); return count >= t;
            }
            case EcoGoalMetric.ValleyHealth:
            {
                float health = ValleyHealth();
                status = "Sağlık %" + health.ToString("0") + " / %" + t.ToString("0");
                progress = Mathf.Clamp01(health / t); return health >= t;
            }
        }
        return false;
    }

    /// <summary>The project whose steps lead toward the most urgent unfinished goal (-1 when none is linked).</summary>
    public int FocusProject(EcoProjects projects)
    {
        foreach (bool required in new[] { true, false })
            foreach (var goal in State.goals)
            {
                if (goal.done || goal.required != required) continue;
                var spec = goal.Spec;
                if (spec != null && spec.project >= 0 && spec.project < projects.All.Count && !projects.All[spec.project].done) return spec.project;
            }
        return -1;
    }

    public static int WastePiles() => FindObjectsByType<RecyclableResource>(FindObjectsSortMode.None)
        .Count(w => w != null && !w.Claimed && !w.litter && w.GetComponent<EnemyLootDrop>() == null);

    // ================================================================== health, ending

    /// <summary>Valley ecosystem health 0..100: half the average region and half the worst region, so one ruined area
    /// cannot hide behind clean mountains.</summary>
    public static float ValleyHealth()
    {
        float sum = 0f, worst = 100f; int count = 0;
        foreach (var r in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None))
        {
            if (r == null || !r.TryGetComponent<EcoRegionInfo>(out _)) continue;
            float value = RegionHealth(r);
            sum += value; worst = Mathf.Min(worst, value); count++;
        }
        return count == 0 ? 0f : 0.5f * (sum / count) + 0.5f * worst;
    }

    /// <summary>Same formula as the world HUD's ecosystem row.</summary>
    public static float RegionHealth(EcoRegion r)
    {
        float air = 100f - r.airPollution, water = 100f - r.waterPollution, soil = 100f - r.soilPollution;
        float recovery = r.TryGetComponent<EcoHabitat>(out var habitat) ? habitat.recovery : 0f;
        float bio = Mathf.Clamp(r.vegetation * 55f + recovery * 30f + water * 0.15f, 0f, 100f);
        return Mathf.Clamp(air * 0.30f + water * 0.25f + soil * 0.20f + bio * 0.25f, 0f, 100f);
    }

    public void FinaleWon() { if (Active && !Ended) { State.finaleWon = true; EcoWorldSave.Current?.RequestAutosave(); } }

    /// <summary>Score breakdown shown on the result screen and used for credits.</summary>
    public int Score(out int goalsPart, out int healthPart, out int finalePart)
    {
        goalsPart = State.goals.Where(g => g.done).Sum(g => g.required ? 30 : 15);
        healthPart = Mathf.RoundToInt(Mathf.Clamp(Health, 0f, 100f) * 0.25f);
        finalePart = State.finaleWon ? 10 : 0;
        return Mathf.Clamp(goalsPart + healthPart + finalePart, 0, 100);
    }

    public static string GradeOf(int score) => score >= 90 ? "S" : score >= 75 ? "A" : score >= 60 ? "B" : "C";

    private void End(Result result)
    {
        Health = ValleyHealth();
        State.result = (int)result;
        State.score = Score(out _, out _, out _);
        bool success = result == Result.Success;
        State.grade = success ? GradeOf(State.score) : "—";
        int credits;
        if (State.training) credits = success ? 30 : 10;
        else
        {
            credits = success ? (State.grade == "S" ? 60 : State.grade == "A" ? 45 : State.grade == "B" ? 30 : 20) : 8;
            credits += State.goals.Count(g => !g.required && g.done) * 5 + (State.finaleWon ? 10 : 0);
            float scale = Scenario.difficulty == EcoDifficulty.Calm ? 0.8f : Scenario.difficulty == EcoDifficulty.Crisis ? 1.4f : 1f;
            credits = Mathf.RoundToInt(credits * scale);
        }
        State.credits = credits;
        EcoMeta.RecordRun(Scenario.valleyName, State.grade, State.score, credits, success);
        LogRun(result);
        EcoPlaytestLog.Objective("Run bitti: " + result + " · not " + State.grade + " · puan " + State.score + " · +" + credits + " kredi");
        RunEnded?.Invoke();
        ui.ShowResult();
        EcoWorldSave.Current?.RequestAutosave();
    }

    private void LogRun(Result result)
    {
        string Q(string v) => "\"" + (v ?? "").Replace("\"", "'") + "\"";
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string row = string.Join(",", new[]
        {
            DateTime.Now.ToString("yyyy-MM-dd HH:mm", inv), Q(Scenario.valleyName), EcoValleyAssembler.Current != null ? EcoValleyAssembler.Current.Code : "",
            State.seed.ToString(inv), EcoScenario.DifficultyNames[State.difficulty], Scenario.problem.ToString(), result.ToString(), State.grade, State.score.ToString(inv),
            State.credits.ToString(inv), (State.playSeconds / 60f).ToString("0.0", inv), State.goals.Count(g => g.done).ToString(inv),
            Q(string.Join(" ", State.goals.Select(g => g.id + (g.done ? "+" : "-")))), Q(string.Join(" ", State.cards)), Q(string.Join(" ", State.crises)),
            Q(string.Join(" ", State.health.Select(h => h.ToString("0", inv)))),
        });
        EcoPlaytestLog.Run(row);
    }

    // ================================================================== save hook

    public RunState CaptureState() => State.active ? State : null;

    public void RestoreState(RunState state)
    {
        if (state == null || !state.active) return;
        State = state;
        State.goals ??= new List<EcoGoalState>(); State.cards ??= new List<string>(); State.crises ??= new List<string>();
        State.health ??= new List<float>(); State.offer ??= new string[0];
        Scenario = State.training ? EcoScenario.Training() : EcoScenario.Roll(State.seed, (EcoDifficulty)State.difficulty);
        restored = true;
    }
    private bool restored;

    /// <summary>Called by EcoWorldSave after a load. A save from before runs existed starts a run on the loaded world.</summary>
    public void AfterLoad()
    {
        Pending = null;
        if (restored)
        {
            restored = false;
            // Enemy zone budgets live in the scene; a patrol card bought earlier in the run applies again.
            if (State.patrol) foreach (var zone in FindObjectsByType<EcoEnemySpawnZone>(FindObjectsSortMode.None)) zone.dailyBudget = Mathf.Max(1, zone.dailyBudget - 2);
            if (!State.training)
                foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None))
                {
                    int i = State.coverKeys != null ? State.coverKeys.IndexOf(region.Key) : -1;
                    // Older run saves have no start cover; the loaded cover is the closest stand-in.
                    region.wildCover = i >= 0 && i < State.coverValues.Count ? State.coverValues[i] : region.vegetation;
                }
            ui.AfterLoad(); return;
        }
        var scenario = EcoScenario.Roll(NewSeed(), EcoDifficulty.Normal);
        Begin(scenario, EcoGoalCatalog.Pick(scenario, PresentKinds()), false);
    }

    // ================================================================== world helpers

    public static HashSet<EcoRegionInfo.Kind> PresentKinds()
    {
        var kinds = new HashSet<EcoRegionInfo.Kind>();
        foreach (var info in FindObjectsByType<EcoRegionInfo>(FindObjectsSortMode.None)) kinds.Add(info.kind);
        return kinds.Count > 0 ? kinds : null;
    }

    /// <summary>The region of a kind. With several (two lakes) the one carrying a project wins, then the most polluted;
    /// the choice is cached so cleaning a lake never switches the goal over to the other one.</summary>
    /// <summary>Trees the player planted that are taking root: saplings at half growth or more, plus the grown trees
    /// they became (a mature sapling turns into a tree and leaves PlantedSeed behind, so both are counted).</summary>
    public static int PlantedTrees() => FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).Count(p => p.kind == EcoCropCatalog.Kind.Tree && !p.IsWithered && p.growth >= 0.5f)
        + EcoVegetation.PlantedHealthy();

    public static EcoRegion RegionOf(EcoRegionInfo.Kind kind)
    {
        if (regionCache.TryGetValue(kind, out var cached) && cached != null && cached.isActiveAndEnabled) return cached;
        EcoRegion best = null; float worst = -1f;
        foreach (var info in FindObjectsByType<EcoRegionInfo>(FindObjectsSortMode.InstanceID))
        {
            if (info == null || info.kind != kind || !info.TryGetComponent<EcoRegion>(out var region)) continue;
            float rank = (info.projectIndex >= 0 ? 1000f : 0f) + region.airPollution + region.waterPollution + region.soilPollution;
            if (rank > worst) { worst = rank; best = region; }
        }
        if (best != null) regionCache[kind] = best;
        return best;
    }
    private static readonly Dictionary<EcoRegionInfo.Kind, EcoRegion> regionCache = new Dictionary<EcoRegionInfo.Kind, EcoRegion>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { regionCache.Clear(); autoStart = false; pendingStart = null; }

    private System.Random spawnRandom;
    private System.Random SpawnRandom => spawnRandom ??= new System.Random(State.seed ^ 0x5bd1e995);

    public int SpawnFires(int count, params EcoRegionInfo.Kind[] kinds)
    {
        if (count <= 0) return 0;
        // The authored brush fire is the model; the save system's hidden copy is only a fallback.
        if (fireTemplate == null) fireTemplate = FindObjectsByType<EcoFire>(FindObjectsSortMode.InstanceID).FirstOrDefault()
            ?? FindObjectsByType<EcoFire>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).FirstOrDefault();
        if (fireTemplate == null) return 0;
        int lit = 0;
        for (int i = 0; i < count; i++)
        {
            if (!FindSpot(kinds, 1.4f, out var position)) continue;
            var go = Instantiate(fireTemplate.gameObject, position, Quaternion.identity);
            go.name = "Run fire"; go.hideFlags = HideFlags.None; go.SetActive(true);
            if (go.TryGetComponent<EcoSaveIdentity>(out var identity)) identity.Assign("runtime:" + Guid.NewGuid().ToString("N"), false);
            var fire = go.GetComponent<EcoFire>();
            fire.fuel = 140f; fire.intensity = 1f;
            lit++;
        }
        return lit;
    }

    public int SpawnWaste(int count, params EcoRegionInfo.Kind[] kinds)
    {
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            if (!FindSpot(kinds, 0.8f, out var position)) continue;
            bool metal = SpawnRandom.NextDouble() < 0.5;
            var drop = EcoWorldArt.Spawn(metal ? "MetalScrap" : "PlasticScrap", position + Vector3.up * 0.05f);
            if (drop == null) continue;
            drop.name = "Run waste";
            var resource = drop.GetComponent<RecyclableResource>();
            if (resource == null) resource = drop.AddComponent<RecyclableResource>();
            resource.metal = metal ? 4 : 1; resource.plastic = metal ? 1 : 4;
            placed++;
        }
        return placed;
    }

    /// <summary>
    /// Cans and bottles lying around at the start, in proportion to how dirty each region is (average of air, water and
    /// soil pollution): a clean glacier has almost none, a smoggy factory district a dozen. Each is worth 2 metal or
    /// 2 plastic, so the first minutes of a dirty valley already pay for the first builds.
    /// </summary>
    private int ScatterLitter()
    {
        int placed = 0;
        foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID))
        {
            if (!region.TryGetComponent<EcoRegionInfo>(out _)) continue;
            float dirt = (region.airPollution + region.waterPollution + region.soilPollution) / 3f;
            int count = Mathf.Min(14, Mathf.RoundToInt(LitterFor(dirt)));
            for (int i = 0; i < count && placed < 70; i++)
            {
                if (!FindSpot(new[] { region }, 0.4f, out var position)) continue;
                if (EcoTraffic.Litter(position, SpawnRandom.NextDouble() < 0.5) != null) placed++;
            }
        }
        EcoPlaytestLog.Objective("Başlangıç çöpü: " + placed + " parça (kirliliğe göre)");
        return placed;
    }

    /// <summary>Opening litter for a region's average pollution (0–100): none below 5, one per 5 points above.</summary>
    public static float LitterFor(float pollution) => Mathf.Max(0f, pollution - 5f) / 5f;

    /// <summary>Open, flat ground inside one of the given region kinds, away from the player and other objects.</summary>
    private bool FindSpot(EcoRegionInfo.Kind[] kinds, float clearance, out Vector3 position)
        => FindSpot(kinds.Select(RegionOf).Where(r => r != null).ToArray(), clearance, out position);

    private bool FindSpot(EcoRegion[] regions, float clearance, out Vector3 position)
    {
        position = Vector3.zero;
        if (regions.Length == 0) return false;
        var player = FindFirstObjectByType<PlayerController>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            var region = regions[SpawnRandom.Next(regions.Length)];
            var local = new Vector3(((float)SpawnRandom.NextDouble() - 0.5f) * region.size.x * 0.85f, 0f, ((float)SpawnRandom.NextDouble() - 0.5f) * region.size.y * 0.85f);
            Vector3 p = region.transform.TransformPoint(local);
            if (!Physics.Raycast(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, out var hit, 150f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!EcoGround.IsGround(hit.collider) || hit.normal.y < 0.85f || EcoRegion.At(hit.point) != region) continue;
            if (player != null && (hit.point - player.transform.position).sqrMagnitude < 12f * 12f) continue;
            bool clear = true;
            foreach (var c in Physics.OverlapSphere(hit.point + Vector3.up * (clearance + 0.1f), clearance, ~0, QueryTriggerInteraction.Ignore))
                if (!EcoGround.IsGround(c)) { clear = false; break; }
            if (!clear) continue;
            position = hit.point; return true;
        }
        return false;
    }
}
