using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// GDD §12 final. [F] next to the terminal (glacier station, once active) starts the encounter when readiness >= 50 %,
// the terminal holds >= 40 EB / 30 SB and the vehicle carries >= 40 EB / 15 SB / 5 iron balls. Three focus orbs
// (heat -> smoke -> core) must be balanced in order; the monster only adds pressure and can never end the fight.
// Losing (recovery) removes every transient actor and resets the progress; the world is untouched and [F] retries.
public sealed class EcoFinale : MonoBehaviour
{
    public const float MinimumReadiness = 0.5f;
    public const float TerminalEnergyCap = 120f, TerminalWaterCap = 60f, BalanceSeconds = 4f;
    public static EcoFinale Current { get; private set; }

    public bool Won { get; private set; }
    public bool Fighting => encounter != null;
    public int Phase { get; private set; }
    public Transform EncounterRoot => encounter;
    // Saved by reflection (EcoSaveIdentity "EcoFinale" private state).
    private float terminalEnergy, terminalWater;
    public float TerminalEnergy { get => terminalEnergy; private set => terminalEnergy = value; }
    public float TerminalWater { get => terminalWater; private set => terminalWater = value; }
    public float RequiredWater => 12f * RequirementScale;
    public int RequiredMetalHits => Mathf.Max(1, Mathf.CeilToInt(5f * RequirementScale));
    public bool OutageActive => Time.time < outageUntil;
    private float RequirementScale => readiness >= 0.999f ? 0.75f : 1f;
    public Vector3 TerminalPosition => EcoGlacierStation.Current != null ? EcoGlacierStation.Current.transform.position : transform.position;

    private Transform encounter;
    private EnemyBrain boss;
    private readonly EcoFinaleFocus[] foci = new EcoFinaleFocus[3];
    private PlayerWeaponSystem weapons;
    private PlayerVitals vitals;
    private bool wasRecovering, airTransferred;
    private float readiness, bossRespawn, nextHint, nextRefresh;
    private float messageUntil;
    private string message = "";
    private GUIStyle big, small;
    private float nextOutage, outageUntil, nextDrought, nextSmog, pressureFree;
    private readonly Dictionary<EcoPowerNode, Vector2> saved = new Dictionary<EcoPowerNode, Vector2>();

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; EndOutage(); }

    // ------------------------------------------------------------------ pressure (one at a time, spaced)

    private void UpdatePressure()
    {
        if (!Fighting) { nextDrought = nextSmog = 0f; return; }
        if (nextDrought <= 0f) { nextDrought = Time.time + 55f; nextSmog = Time.time + 75f; }
        if (Time.time < pressureFree || OutageActive) return;
        if (Time.time >= nextDrought)
        {
            nextDrought = Time.time + 60f; pressureFree = Time.time + 12f;
            foreach (var node in FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None))
                // GDD §12: a drought wave takes at most 20 % of the open reserve; a closed tank halves that loss.
                if (node.kind == EcoWaterNode.Kind.Tank) node.cleanWater *= 0.9f;
                else if (node.kind == EcoWaterNode.Kind.Purifier) node.cleanWater *= 0.8f;
            Say("Kuraklık! Açık rezervden %20, kapalı depodan %10 temiz su buharlaştı.", 6f);
        }
        else if (Time.time >= nextSmog)
        {
            nextSmog = Time.time + 60f; pressureFree = Time.time + 12f;
            foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None))
            { float guard = 1f - 0.6f * region.vegetation; region.AddPollution(10f * guard, 8f * guard, 6f * guard, 5f * guard); }
            Say("Duman dalgası! Bölgelerde kirlilik arttı; bitki örtüsü etkiyi azaltır.", 6f);
        }
    }

    private void UpdateOutage()
    {
        if (!Fighting) { EndOutage(); nextOutage = 0f; return; }
        if (nextOutage <= 0f) nextOutage = Time.time + 35f;
        if (!OutageActive && saved.Count > 0) EndOutage();
        if (Time.time < nextOutage || OutageActive || Time.time < pressureFree) return;
        nextOutage = Time.time + 45f; outageUntil = Time.time + 12f; pressureFree = outageUntil + 3f;
        foreach (var node in FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None))
        {
            if (node.kind != EcoPowerNode.Kind.Solar && node.kind != EcoPowerNode.Kind.Wind) continue;
            saved[node] = new Vector2(node.solarExposure, node.windExposure);
            node.solarExposure = 0f; node.windExposure = 0f;
        }
        Say("İklim Canavarı enerji hatlarını kesti! Batarya ve depolama devrede olmalı (12 sn).", 6f);
    }

    private void EndOutage()
    {
        foreach (var pair in saved) if (pair.Key != null) { pair.Key.solarExposure = pair.Value.x; pair.Key.windExposure = pair.Value.y; }
        saved.Clear(); outageUntil = 0f;
    }

    // ------------------------------------------------------------------ state

    [System.Serializable] public sealed class ReserveState { public float energy, water; }
    /// <summary>Save hook (EcoWorldSave global hooks): the terminal reserve survives save/load.</summary>
    public ReserveState CaptureState() => new ReserveState { energy = terminalEnergy, water = terminalWater };
    public void RestoreState(ReserveState state)
    {
        if (state == null) return;
        terminalEnergy = Mathf.Clamp(state.energy, 0f, TerminalEnergyCap); terminalWater = Mathf.Clamp(state.water, 0f, TerminalWaterCap);
    }

    public void RestoreState(bool won)
    {
        EndOutage();
        Won = won;
        ClearEncounter();
    }

    private void ClearEncounter()
    {
        if (encounter != null) Destroy(encounter.gameObject);
        encounter = null; boss = null; Phase = 0; airTransferred = false;
        for (int i = 0; i < foci.Length; i++) foci[i] = null;
    }

    private void Update()
    {
        UpdateOutage();
        UpdatePressure();
        if (Won) return;
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>();
        if (Fighting) { UpdateFight(); return; }
        var keyboard = Keyboard.current;
        if (keyboard != null && EcoKeys.Pressed(EcoKeys.Action.Finale) && !EcoUIFlow.InputBlocked && !EcoMarketUI.BlocksGameplay) TryBegin();
    }

    private void TryBegin()
    {
        var projects = EcoProjects.Current;
        if (projects == null || weapons == null) return;
        var station = EcoGlacierStation.Current;
        if (station == null || !station.Active) { Say("Final terminali buzul istasyonunda: önce istasyonu çalıştır " + EcoKeys.Tag(EcoKeys.Action.Interact) + "."); return; }
        if ((weapons.transform.position - TerminalPosition).sqrMagnitude > 9f * 9f) { Say("Final için buzul terminaline yaklaş ve " + EcoKeys.Tag(EcoKeys.Action.Finale) + "'ye bas."); return; }
        if (projects.Readiness < MinimumReadiness) { Say("Final için hazırlık yetersiz: en az " + Mathf.CeilToInt(MinimumReadiness * 6f) + " proje bitmeli (" + EcoKeys.Tag(EcoKeys.Action.Journal) + " günlük)."); return; }
        if (TerminalEnergy < 40f || TerminalWater < 30f)
        { Say("Terminal rezervi yetersiz: " + TerminalEnergy.ToString("0") + "/40 EB, " + TerminalWater.ToString("0") + "/30 SB. Terminalde " + EcoKeys.Tag(EcoKeys.Action.Interact) + " ile araçtan rezerv yükle.", 7f); return; }
        if (weapons.Energy < 40f || weapons.Water < 15f || weapons.MetalAmmo < 5)
        { Say("Araç hazır değil: en az 40 EB (" + weapons.Energy.ToString("0") + "), 15 SB (" + weapons.Water.ToString("0") + "), 5 demir top (" + weapons.MetalAmmo + ").", 7f); return; }
        Begin(projects.Readiness);
    }

    private void Begin(float level)
    {
        EnemyBrain template = null;
        foreach (var e in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) if (e != null && e.State != EnemyBrain.Behaviour.Dead) { template = e; break; }
        if (template == null) { Say("Final varlığı için şablon düşman bulunamadı; zafer verilmedi. Haritayı yeniden kur.", 8f); return; }
        EcoWorldSave.Current?.SaveBeforeFinale(); // pre-final snapshot; saving is deferred while the fight runs
        readiness = level; Phase = 0; airTransferred = false;
        encounter = new GameObject("Finale encounter (temporary)").transform;
        encounter.position = TerminalPosition;
        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120f + 30f) * Mathf.Deg2Rad;
            Vector3 position = TerminalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 12f;
            if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out var hit, 150f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point;
            var go = new GameObject("Focus " + i); go.transform.SetParent(encounter, true); go.transform.position = position;
            foci[i] = go.AddComponent<EcoFinaleFocus>(); foci[i].Initialize(this, i);
        }
        SpawnBoss(template);
        RefreshFoci();
        wasRecovering = vitals != null && vitals.IsRecovering;
        pressureFree = Time.time + 10f; nextOutage = nextDrought = nextSmog = 0f;
        Say("İklim Canavarı uyandı! Odakları sırayla dengele: ısı (temiz su) → duman (anti-vakum) → çekirdek (demir top + terminalde E).", 9f);
    }

    private void SpawnBoss(EnemyBrain template)
    {
        var go = template.gameObject; bool was = go.activeSelf; go.SetActive(false);
        var clone = Instantiate(go); go.SetActive(was);
        clone.name = "İklim Canavarı"; clone.transform.localScale = Vector3.one * 2.5f;
        clone.transform.SetParent(encounter, true);
        var actor = clone.AddComponent<EcoFinaleActor>(); actor.Initialize(this);
        Vector3 pos = TerminalPosition + new Vector3(0f, 0f, 16f);
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var near, 14f, UnityEngine.AI.NavMesh.AllAreas)) pos = near.position;
        clone.transform.position = pos;
        boss = clone.GetComponent<EnemyBrain>();
        float weak = Mathf.Clamp01(readiness); // 0.5 .. 1
        boss.maximumHealth = Mathf.Lerp(1800f, 700f, weak);
        boss.damage = Mathf.Lerp(22f, 10f, weak);
        boss.moveSpeed = 2f; boss.chaseSpeed = 3.2f; boss.cooldown = 1.6f;
        boss.detectionRange = 60f; boss.leashRange = 120f; boss.attackRange = 8f;
        clone.SetActive(true);
    }

    private void UpdateFight()
    {
        bool recovering = vitals != null && vitals.IsRecovering;
        if (recovering && !wasRecovering) { Fail("Yenildin! Final sıfırlandı, dünya duruyor. Rezervi tazele ve " + EcoKeys.Tag(EcoKeys.Action.Finale) + " ile tekrar dene."); return; }
        wasRecovering = recovering;
        // The monster is pressure, not the objective: if it falls it simply returns after a pause.
        if (boss == null || boss.State == EnemyBrain.Behaviour.Dead)
        {
            if (bossRespawn <= 0f) bossRespawn = Time.time + 20f;
            else if (Time.time >= bossRespawn)
            {
                bossRespawn = 0f; EnemyBrain template = null;
                foreach (var e in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) if (e != null && e.State != EnemyBrain.Behaviour.Dead && e.GetComponentInParent<EcoFinaleActor>() == null) { template = e; break; }
                if (template != null) SpawnBoss(template);
            }
        }
        if (Phase > 2) return;
        var focus = foci[Phase];
        if (focus == null) { Fail("Odak kayboldu; final sıfırlandı."); return; }
        if (Phase == 0) Hold(focus, focus.WaterReceived >= RequiredWater);
        else if (Phase == 1)
        {
            if (!airTransferred && focus.AirSeconds >= 3f)
            {
                if (TerminalEnergy >= 20f) { TerminalEnergy -= 20f; airTransferred = true; Say("Anti-vakum tamam • terminalden 20 EB aktarıldı. Dengeyi koru.", 4f); }
                else if (Time.time >= nextHint) { nextHint = Time.time + 5f; Say("Terminalde 20 EB yok! Rezerv yükle " + EcoKeys.Tag(EcoKeys.Action.Interact) + ".", 4f); }
            }
            Hold(focus, airTransferred);
        }
        if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 0.25f; RefreshFoci(); }
    }

    private void Hold(EcoFinaleFocus focus, bool ok)
    {
        focus.Held = ok ? Mathf.Min(BalanceSeconds, focus.Held + Time.deltaTime) : 0f;
        if (focus.Held >= BalanceSeconds) Balance(focus);
    }

    private void Balance(EcoFinaleFocus focus)
    {
        focus.Balanced = true; Phase++;
        RefreshFoci();
        if (Phase > 2) { Win(); return; }
        Say("Odak dengelendi! Sıradaki: " + foci[Phase].name + ".", 5f);
    }

    private void RefreshFoci() { foreach (var f in foci) if (f != null) f.RefreshVisual(); }

    private void Fail(string reason)
    {
        ClearEncounter();
        Say(reason, 9f);
    }

    private void Win()
    {
        ClearEncounter();
        Won = true;
        Say("İklim Canavarı yatıştı! Yerleşim ve çocuk nefes alıyor. KAZANDIN!\nSerbest oyun sürüyor: siparişler, çevre ve yerleşim seninle.", 25f);
        EcoWorldSave.Current?.RequestAutosave();
    }

    /// <summary>[E] at the terminal: finish the core phase when armour is open, otherwise load reserve from the vehicle.</summary>
    public bool TryInteractAtTerminal(PlayerWeaponSystem w)
    {
        if (w == null || Won) return false;
        var station = EcoGlacierStation.Current;
        if (station == null || !station.Active) return false;
        if ((w.transform.position - TerminalPosition).sqrMagnitude > 7f * 7f) return false;
        if (Fighting && Phase == 2 && foci[2] != null && foci[2].MetalHits >= RequiredMetalHits) { Balance(foci[2]); return true; }
        float energy = Mathf.Min(20f, w.Energy, TerminalEnergyCap - TerminalEnergy);
        float water = Mathf.Min(10f, w.Water, TerminalWaterCap - TerminalWater);
        bool movedEnergy = energy >= 1f && w.TrySpendElectricity(energy);
        bool movedWater = water >= 1f && w.TryTakeCleanWater(water);
        if (movedEnergy) TerminalEnergy += energy;
        if (movedWater) TerminalWater += water;
        w.ShowFeedback(movedEnergy || movedWater
            ? "Terminal rezervi: " + TerminalEnergy.ToString("0") + "/120 EB • " + TerminalWater.ToString("0") + "/60 SB"
            : "Rezerv aktarılamadı: terminal dolu ya da araçta yeterli elektrik/temiz su yok (" + TerminalEnergy.ToString("0") + "/120 EB, " + TerminalWater.ToString("0") + "/60 SB).");
        return true;
    }

    private void Say(string text, float seconds = 6f) { message = text; messageUntil = Time.unscaledTime + seconds; }

    // ------------------------------------------------------------------ HUD

    private void OnGUI()
    {
        if (big == null) big = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        if (small == null) small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var projects = EcoProjects.Current;
        if (Fighting)
        {
            if (boss != null && boss.State != EnemyBrain.Behaviour.Dead)
            {
                float f = Mathf.Clamp01(boss.Health / boss.maximumHealth);
                var back = new Rect(Screen.width * 0.3f, Screen.height * 0.07f, Screen.width * 0.4f, 14);
                GUI.Box(back, GUIContent.none);
                var prev = GUI.color; GUI.color = EcoSettings.Bad;
                GUI.DrawTexture(new Rect(back.x + 1, back.y + 1, (back.width - 2) * f, back.height - 2), Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(new Rect(back.x, back.y - 22, back.width, 20), "İklim Canavarı (baskı)", small);
            }
            if (Phase <= 2 && foci[Phase] != null)
                GUI.Label(new Rect(Screen.width * 0.25f, Screen.height * 0.1f, Screen.width * 0.5f, 60f), "FAZ " + (Phase + 1) + "/3 • " + foci[Phase].Describe().Replace("\n", " • "), small);
        }
        else if (!Won && projects != null && projects.Readiness >= MinimumReadiness && !EcoMarketUI.IsOpen && EcoGlacierStation.Current != null && EcoGlacierStation.Current.Active
            && weapons != null && (weapons.transform.position - TerminalPosition).sqrMagnitude < 9f * 9f)
        {
            GUI.Label(new Rect(Screen.width * 0.5f - 280f, 52f, 560f, 48f),
                "" + EcoKeys.Tag(EcoKeys.Action.Finale) + " Finali başlat • hazırlık %" + Mathf.RoundToInt(projects.Readiness * 100f) + " • Terminal " + TerminalEnergy.ToString("0") + " EB / " + TerminalWater.ToString("0") + " SB (" + EcoKeys.Tag(EcoKeys.Action.Interact) + " yükle)", small);
        }
        if (Time.unscaledTime <= messageUntil && message.Length > 0 && !(EcoSurvey.Current != null && EcoSurvey.Current.IsOpen))
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.14f, Screen.width * 0.7f, 90f), message, big);
    }
}
