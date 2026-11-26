using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Run screens drawn with the notebook theme: briefing, morning cards, evening crisis, result and the goal
/// panel, plus the small run chip on the HUD. Modal screens freeze the world through EcoUIFlow.</summary>
public sealed class EcoRunUI : MonoBehaviour
{
    public enum View { None, Briefing, Cards, Crisis, Result, Goals, Unlocks }
    public View Current { get; private set; }
    public bool Busy => Current != View.None && Current != View.Goals;
    public static bool ChipVisible { get; private set; }
    /// <summary>Bottom of the run chip in screen pixels (0 when hidden), for cards stacked under it.</summary>
    public static float ChipBottomPixels { get; private set; }

    private EcoRun run;
    private View unlocksReturn = View.Briefing;
    private Vector2 scroll;
    private GUIStyle big, small, tag, cost;

    internal void Bind(EcoRun owner) => run = owner;

    public void ShowBriefing() => Open(View.Briefing);
    public void ShowCards() => Open(View.Cards);
    public void ShowCrisis() => Open(View.Crisis);
    public void ShowResult() => Open(View.Result);
    public void ShowGoals() { if (Current == View.None) Open(View.Goals); }

    public void AfterLoad()
    {
        Close();
        if (run.Ended) ShowResult();
        else if (run.State.offerOpen) ShowCards();
    }

    private void Open(View view)
    {
        Current = view; scroll = Vector2.zero;
        EcoUIFlow.SetPanel(this, true, view != View.Goals);
    }

    private void Close()
    {
        Current = View.None;
        EcoUIFlow.SetPanel(this, false);
    }

    private void OnDisable() { if (Current != View.None) Close(); }

    private void Update()
    {
        if (Current == View.None) return;
        if (!EcoUIFlow.IsPanel(this))
        {
            if (Current == View.Goals) { Current = View.None; return; }
            if (!PlayerController.Paused) EcoUIFlow.SetPanel(this, true, true); // a modal run screen keeps its place
        }
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (Current == View.Goals || Current == View.Crisis))
        { EcoUIFlow.ConsumeEscape(); Close(); }
    }

    // ================================================================== drawing

    private void OnGUI()
    {
        if (run == null) return;
        if (PlayerController.Paused && Current != View.Briefing) { ChipBottomPixels = 0f; return; }
        if (Current == View.Briefing && PlayerController.Paused) return;
        EcoTheme.Begin();
        Styles();
        GUI.depth = -4;
        switch (Current)
        {
            case View.Briefing: DrawBriefing(); break;
            case View.Cards: DrawCards(); break;
            case View.Crisis: DrawCrisis(); break;
            case View.Result: DrawResult(); break;
            case View.Goals: DrawGoals(); break;
            case View.Unlocks: DrawUnlocks(); break;
        }
        DrawChip();
        EcoTheme.End();
    }

    private void Styles()
    {
        if (big != null) return;
        big = new GUIStyle(EcoTheme.Subtitle) { fontSize = 54, alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(EcoTheme.Muted) { fontSize = 14, wordWrap = true };
        tag = new GUIStyle(EcoTheme.Muted) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        cost = new GUIStyle(small);
        cost.normal.textColor = EcoSettings.Bad;
    }

    private static float H(GUIStyle style, string text, float width) => style.CalcHeight(new GUIContent(text ?? ""), Mathf.Max(20f, width));

    private void DrawBriefing()
    {
        var s = run.Pending;
        if (s == null) { Close(); return; }
        var r = EcoTheme.Window(980f, 720f, s.valleyName, s.training
            ? "İlk vadi: alışma turu. 5 günde 2 zorunlu hedefi tamamla."
            : "Her vadi farklı. 5 oyun gününde 2 zorunlu hedefi tamamla, bonusu kovala, vadiyi sağlıklı bırak.", "ui_map");
        float x = r.x + 36f, y = r.y + 112f, column = (r.width - 96f) * 0.5f, right = x + column + 24f;
        float footer = r.yMax - 64f;

        // Left: problem and regions.
        GUI.Label(new Rect(x, y, column, 28f), "Ana sorun · " + s.ProblemName, EcoTheme.Subtitle);
        float py = y + 32f, ph = H(EcoTheme.Body, s.ProblemText, column);
        GUI.Label(new Rect(x, py, column, ph), s.ProblemText, EcoTheme.Body);
        py += ph + 14f;
        GUI.Label(new Rect(x, py, column, 26f), "Vadinin haritası ve bölgeler", EcoTheme.Subtitle);
        py += 32f;
        // The valley is assembled only when the run starts, so its regions come from the layout, not the scene.
        foreach (var row in EcoValley.Rows(EcoValley.Layout(s)))
        {
            if (py > footer - 70f) continue;
            var a = row.pollution;
            float pollution = s.training ? (a.x + a.y + a.z) / 3f : (s.Pollution(row.kind, a.x) + s.Pollution(row.kind, a.y) + s.Pollution(row.kind, a.z)) / 3f;
            EcoTheme.Icon(new Rect(x, py + 1f, 22f, 22f), RegionIcon(row.kind));
            GUI.Label(new Rect(x + 30f, py, column * 0.45f, 24f), row.name + " · " + row.place, EcoTheme.Muted);
            Bar(new Rect(x + 30f + column * 0.47f, py + 9f, column * 0.35f, 7f), pollution / 100f, pollution >= 45f ? EcoSettings.Bad : pollution >= 20f ? EcoUI.Amber : EcoSettings.Good);
            GUI.Label(new Rect(x + column - 50f, py, 50f, 24f), "%" + pollution.ToString("0"), EcoTheme.Muted);
            py += 27f;
        }
        if (!s.training)
            GUI.Label(new Rect(x, footer - 64f, column, 56f), (s.fires > 0 ? s.fires + " yangın · " : "") + s.wastePiles + " atık yığını · güneş verimi %" + (s.solarYield * 100f).ToString("0")
                + " · rüzgâr verimi %" + (s.windYield * 100f).ToString("0"), small);

        // Right: goals, difficulty, credits.
        GUI.Label(new Rect(right, y, column, 28f), "Hedefler", EcoTheme.Subtitle);
        float gy = y + 34f;
        foreach (var goal in run.PendingGoals)
        {
            var spec = goal.Spec;
            string detail = spec.Detail(goal.threshold);
            float dh = H(small, detail, column - 40f);
            GUI.Box(new Rect(right, gy, column, dh + 40f), GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(right + 10f, gy + 10f, 24f, 24f), spec.icon);
            GUI.Label(new Rect(right + 42f, gy + 6f, column - 130f, 26f), spec.title, EcoTheme.Body);
            GUI.Label(new Rect(right + column - 88f, gy + 8f, 80f, 22f), goal.required ? "ZORUNLU" : "BONUS", tag);
            GUI.Label(new Rect(right + 42f, gy + 32f, column - 52f, dh), detail, small);
            gy += dh + 48f;
        }
        if (!s.training) { GUI.Label(new Rect(right, gy - 4f, column, 20f), "Ölçülen hedefler, vadinin başlangıç durumundan daha iyisini ister.", small); gy += 18f; }
        gy += 6f;
        if (!s.training)
        {
            GUI.Label(new Rect(right, gy, column, 26f), "Zorluk", EcoTheme.Subtitle);
            gy += 32f;
            float pill = (column - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                bool locked = i == 2 && !EcoMeta.Has("crisis_mode");
                GUI.enabled = !locked;
                var style = (int)s.difficulty == i ? EcoTheme.PrimaryButton : EcoTheme.Button;
                if (GUI.Button(new Rect(right + i * (pill + 8f), gy, pill, 34f), EcoScenario.DifficultyNames[i] + (locked ? " · kilitli" : ""), style))
                    run.SetPendingDifficulty((EcoDifficulty)i);
                GUI.enabled = true;
            }
            gy += 44f;
        }
        DrawShop(new Rect(right, gy, column, footer - gy - 8f));

        // Footer.
        float bw = 200f;
        if (!s.training && GUI.Button(new Rect(x, footer, bw, 42f), "Başka vadi", EcoTheme.Button)) run.RerollPending();
        if (s.training && EcoMeta.Current.runs == 0 && GUI.Button(new Rect(x, footer, bw + 40f, 42f), "Eğitimi atla", EcoTheme.Button)) run.SkipTraining();
        if (EcoTheme.IconButton(new Rect(r.xMax - 36f - 220f, footer, 220f, 42f), "Vadiye in", "player", EcoTheme.PrimaryButton))
        { Close(); run.BeginPending(); }
    }

    // A compact credit line and the way into the unlock tree (the tree itself has its own screen).
    private void DrawShop(Rect area)
    {
        var profile = EcoMeta.Current;
        GUI.Label(new Rect(area.x, area.y, area.width, 26f), "Yeşil kredi: " + profile.credits + (profile.runs > 0 ? "  ·  en iyi not " + (profile.bestGrade.Length > 0 ? profile.bestGrade : "—") : ""), EcoTheme.Subtitle);
        int owned = EcoMeta.Unlocks.Count(u => EcoMeta.Has(u.id));
        int buyable = EcoMeta.Unlocks.Count(u => !EcoMeta.Has(u.id) && EcoMeta.Available(u) && profile.credits >= u.cost);
        GUI.Label(new Rect(area.x, area.y + 28f, area.width, 22f), owned + "/" + EcoMeta.Unlocks.Length + " açılım" + (buyable > 0 ? " · " + buyable + " tanesi şimdi alınabilir" : ""), small);
        if (area.height >= 90f && EcoTheme.IconButton(new Rect(area.x, area.y + 54f, Mathf.Min(area.width, 300f), 36f), "Açılım ağacı", "ui_skills"))
        { unlocksReturn = Current; Current = View.Unlocks; scroll = Vector2.zero; }
    }

    private void DrawUnlocks()
    {
        var profile = EcoMeta.Current;
        var r = EcoTheme.Window(1120f, 760f, "Açılım ağacı", "Yeşil kredi: " + profile.credits + " · run'lar arasında kalıcıdır. Derin açılımlar aynı daldan birini ister.", "ui_skills");
        float x = r.x + 32f, y = r.y + 112f, w = r.width - 64f, gap = 14f;
        int columns = EcoMeta.Groups.Length;
        float cw = (w - gap * (columns - 1)) / columns, bottom = r.yMax - 72f;
        for (int c = 0; c < columns; c++)
        {
            float cx = x + c * (cw + gap), cy = y;
            GUI.Label(new Rect(cx, cy, cw, 26f), EcoMeta.Groups[c], EcoTheme.Subtitle);
            cy += 30f;
            foreach (var unlock in EcoMeta.Unlocks.Where(u => u.group == EcoMeta.Groups[c]))
            {
                bool owned = EcoMeta.Has(unlock.id), open = EcoMeta.Available(unlock);
                float th = H(small, unlock.text, cw - 24f);
                string needs = open || owned ? "" : "Önce: " + string.Join(" ya da ", unlock.requires.Select(id => EcoMeta.Unlocks.First(u => u.id == id).title));
                float nh = needs.Length > 0 ? H(tag, needs, cw - 24f) : 0f;
                float h = 44f + th + nh + 40f;
                if (cy + h > bottom) break;
                var box = new Rect(cx, cy, cw, h);
                GUI.Box(box, GUIContent.none, EcoTheme.Card);
                EcoTheme.Icon(new Rect(box.x + 12f, box.y + 12f, 26f, 26f), unlock.icon);
                GUI.Label(new Rect(box.x + 46f, box.y + 12f, cw - 56f, 26f), unlock.title + (owned ? " ✓" : ""), EcoTheme.Body);
                GUI.Label(new Rect(box.x + 12f, box.y + 40f, cw - 24f, th), unlock.text, small);
                if (nh > 0f) GUI.Label(new Rect(box.x + 12f, box.y + 42f + th, cw - 24f, nh), needs, tag);
                GUI.enabled = !owned && open && profile.credits >= unlock.cost;
                string label = owned ? "Açık" : unlock.cost + " kredi";
                if (GUI.Button(new Rect(box.x + 12f, box.yMax - 36f, cw - 24f, 28f), label, owned ? EcoTheme.Button : EcoTheme.PrimaryButton)) EcoMeta.Buy(unlock.id);
                GUI.enabled = true;
                cy += h + 8f;
            }
        }
        if (profile.runs > 0 && profile.history.Count > 0)
            GUI.Label(new Rect(x, r.yMax - 64f, w - 260f, 44f), "Son run: " + profile.history[profile.history.Count - 1] + " · toplam " + profile.runs + " run, " + profile.wins + " başarı", small);
        if (GUI.Button(new Rect(r.xMax - 32f - 220f, r.yMax - 62f, 220f, 42f), "Geri", EcoTheme.PrimaryButton)) { Current = unlocksReturn; scroll = Vector2.zero; }
    }

    private void DrawCards()
    {
        var state = run.State;
        if (!state.offerOpen) { Close(); return; }
        int day = run.RunDay;
        string summary = day <= 1 ? "Gün başlıyor. Bugün için bir destek seç." : "Dün gece: ekosistem sağlığı %" + run.Health.ToString("0")
            + " · hedefler " + run.Goals.Count(g => g.done) + "/" + run.Goals.Count + (state.lowHealthDays > 0 ? " · DİKKAT: vadi çökmek üzere" : "");
        var r = EcoTheme.Window(1000f, 560f, "Gün " + day + "/" + EcoRun.Days + " · sabah", summary, "w_sun");
        float x = r.x + 36f, y = r.y + 118f, w = r.width - 72f;
        int n = state.offer.Length;
        float gap = 14f, cw = (w - gap * (n - 1)) / Mathf.Max(1, n), ch = r.yMax - 96f - y;
        for (int i = 0; i < n; i++)
        {
            var card = EcoDayCards.Find(state.offer[i]);
            if (card == null) continue;
            var box = new Rect(x + i * (cw + gap), y, cw, ch);
            GUI.Box(box, GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(box.x + (cw - 46f) * 0.5f, box.y + 16f, 46f, 46f), card.icon);
            var title = new GUIStyle(EcoTheme.Subtitle) { alignment = TextAnchor.UpperCenter };
            float th = H(title, card.title, cw - 24f);
            GUI.Label(new Rect(box.x + 12f, box.y + 70f, cw - 24f, th), card.title, title);
            var text = new GUIStyle(EcoTheme.Body) { alignment = TextAnchor.UpperCenter, fontSize = 16 };
            float bh = H(text, card.text, cw - 24f);
            GUI.Label(new Rect(box.x + 12f, box.y + 76f + th, cw - 24f, bh), card.text, text);
            if (card.cost.Length > 0)
            {
                var c = new GUIStyle(cost) { alignment = TextAnchor.UpperCenter };
                GUI.Label(new Rect(box.x + 12f, box.y + 84f + th + bh, cw - 24f, H(c, card.cost, cw - 24f)), card.cost, c);
            }
            if (!card.repeatable) GUI.Label(new Rect(box.x + 12f, box.yMax - 82f, cw - 24f, 20f), "bir kez alınır", tag);
            if (GUI.Button(new Rect(box.x + 14f, box.yMax - 54f, cw - 28f, 40f), "Seç", EcoTheme.PrimaryButton))
            {
                if (run.ChooseCard(card.id)) Close();
            }
        }
        string footer = run.CardFeedback.Length > 0 ? run.CardFeedback : "Kartlar " + (EcoRun.Days - day + 1) + " gün daha gelecek. Akşam 18:00'de bir kriz olur; gece 5'te fırtına gelir.";
        bool reroll = EcoMeta.Has("card_reroll");
        GUI.Label(new Rect(x, r.yMax - 76f, w - (reroll ? 230f : 0f), 50f), footer, small);
        if (reroll)
        {
            GUI.enabled = run.CanReroll;
            if (GUI.Button(new Rect(r.xMax - 36f - 210f, r.yMax - 78f, 210f, 40f), run.CanReroll ? "Kartları yenile" : "Bugün yenilendi", EcoTheme.Button)) run.RerollCards();
            GUI.enabled = true;
        }
    }

    private void DrawCrisis()
    {
        var crisis = EcoCrisis.Find(run.LastCrisis);
        if (crisis == null) { Close(); return; }
        var r = EcoTheme.Window(660f, 360f, "Akşam krizi · " + crisis.title, "Gün " + run.RunDay + "/" + EcoRun.Days + " · güç ×" + EcoCrisis.Strength(run.RunDay, run.Scenario.difficulty).ToString("0.0"), crisis.icon);
        float x = r.x + 40f, y = r.y + 116f, w = r.width - 80f;
        float h = H(EcoTheme.Body, crisis.text, w);
        GUI.Label(new Rect(x, y, w, h), crisis.text, EcoTheme.Body);
        y += h + 12f;
        GUI.Label(new Rect(x, y, w, 50f), "Sonuç: " + run.LastCrisisResult, EcoTheme.Subtitle);
        if (GUI.Button(new Rect(r.xMax - 40f - 180f, r.yMax - 62f, 180f, 42f), "Anladım", EcoTheme.PrimaryButton)) Close();
    }

    private void DrawResult()
    {
        var state = run.State;
        string heading = run.Outcome == EcoRun.Result.Success ? "Vadi kurtarıldı!" : run.Outcome == EcoRun.Result.Collapsed ? "Vadi çöktü" : "Hedefler tutmadı";
        string sub = run.Outcome == EcoRun.Result.Collapsed ? "Ekosistem sağlığı iki gün üst üste %" + EcoRun.CollapseHealth.ToString("0") + "'in altında kaldı."
            : run.Outcome == EcoRun.Result.Failed ? "5 gün bitti ama zorunlu hedeflerin hiçbiri tamamlanmadı." : run.Scenario.valleyName + " · " + EcoScenario.DifficultyNames[state.difficulty];
        var r = EcoTheme.Window(860f, 680f, heading, sub, run.Outcome == EcoRun.Result.Success ? "yep" : "poi_warning");
        float x = r.x + 40f, y = r.y + 112f, w = r.width - 80f;
        GUI.Label(new Rect(x, y, 140f, 110f), state.grade, big);
        int total = run.Score(out int goals, out int health, out int finale);
        float lx = x + 160f;
        GUI.Label(new Rect(lx, y + 4f, w - 160f, 26f), "Puan " + total + "/100", EcoTheme.Subtitle);
        GUI.Label(new Rect(lx, y + 34f, w - 160f, 24f), "Hedefler " + goals + " · Ekosistem sağlığı " + health + " (%" + run.Health.ToString("0") + ") · Fırtına " + finale, EcoTheme.Muted);
        GUI.Label(new Rect(lx, y + 62f, w - 160f, 26f), "+" + state.credits + " yeşil kredi  (toplam " + EcoMeta.Current.credits + ")", EcoTheme.Body);
        y += 124f;
        foreach (var goal in run.Goals)
        {
            var spec = goal.Spec;
            EcoTheme.Icon(new Rect(x, y + 2f, 22f, 22f), goal.done ? "ui_check" : "ui_cross");
            GUI.Label(new Rect(x + 30f, y, w - 30f, 26f), (goal.required ? "" : "Bonus · ") + spec.title + " — " + spec.Detail(goal.threshold), EcoTheme.Muted);
            y += 30f;
        }
        y += 8f;
        if (state.cards.Count > 0)
        {
            string cards = "Seçilen kartlar: " + string.Join(", ", state.cards.Select(id => EcoDayCards.Find(id)?.title ?? id));
            float ch = H(small, cards, w);
            GUI.Label(new Rect(x, y, w, ch), cards, small); y += ch + 6f;
        }
        if (state.crises.Count > 0)
        {
            string crises = "Atlatılan krizler: " + string.Join(", ", state.crises.Select(id => EcoCrisis.Find(id)?.title ?? id));
            float ch = H(small, crises, w);
            GUI.Label(new Rect(x, y, w, ch), crises, small); y += ch + 10f;
        }
        float footer = r.yMax - 64f;
        DrawShop(new Rect(x, y, w * 0.6f, footer - y - 8f));
        if (EcoTheme.IconButton(new Rect(r.xMax - 40f - 220f, footer, 220f, 42f), "Yeni vadi", "ui_map", EcoTheme.PrimaryButton))
        {
            Close();
            EcoRun.RequestAutoStart();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }
        if (GUI.Button(new Rect(r.xMax - 40f - 220f - 230f, footer, 220f, 42f), "Vadide kal", EcoTheme.Button)) Close();
        if (EcoTheme.IconButton(new Rect(x, footer, 220f, 42f), "Açılım ağacı", "ui_skills")) { unlocksReturn = View.Result; Current = View.Unlocks; scroll = Vector2.zero; }
    }

    private void DrawGoals()
    {
        var r = EcoTheme.Window(780f, 560f, run.Scenario.valleyName, "Gün " + Mathf.Min(run.RunDay, EcoRun.Days) + "/" + EcoRun.Days + " · " + run.Scenario.ProblemName
            + " · ekosistem sağlığı %" + run.Health.ToString("0"), "ui_journal", 40f);
        if (EcoTheme.CloseButton(r)) { Close(); return; }
        float x = r.x + 40f, y = r.y + 114f, w = r.width - 80f;
        foreach (var goal in run.Goals)
        {
            var spec = goal.Spec;
            string detail = spec.Detail(goal.threshold);
            float dh = H(small, detail, w - 60f);
            GUI.Box(new Rect(x, y, w, dh + 86f), GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(x + 12f, y + 12f, 28f, 28f), spec.icon);
            if (goal.done) EcoTheme.Icon(new Rect(x + 28f, y + 30f, 18f, 18f), "ui_check");
            GUI.Label(new Rect(x + 52f, y + 8f, w - 160f, 26f), spec.title, EcoTheme.Body);
            GUI.Label(new Rect(x + w - 98f, y + 10f, 88f, 22f), goal.done ? "TAMAM" : goal.required ? "ZORUNLU" : "BONUS", tag);
            GUI.Label(new Rect(x + 52f, y + 34f, w - 64f, dh), detail, small);
            Bar(new Rect(x + 52f, y + 42f + dh, w - 70f, 6f), goal.done ? 1f : goal.progress, EcoSettings.Good);
            string status = goal.done ? "Tamamlandı" : (goal.status ?? "") + (goal.met && goal.Spec.metric != EcoGoalMetric.Project ? " · doğrulanıyor " + goal.held.ToString("0") + "/120 sn" : "");
            GUI.Label(new Rect(x + 52f, y + 52f + dh, w - 64f, 24f), status, small);
            y += dh + 94f;
        }
        string footer = "Krizler: " + (run.State.crises.Count == 0 ? "henüz yok" : string.Join(", ", run.State.crises.Select(id => EcoCrisis.Find(id)?.title)))
            + "   ·   Kartlar: " + (run.State.cards.Count == 0 ? "—" : string.Join(", ", run.State.cards.Select(id => EcoDayCards.Find(id)?.title)));
        GUI.Label(new Rect(x, r.yMax - 64f, w, 44f), footer, small);
    }

    /// <summary>Compact run status under the objective line: day, valley health and the three goals. Click to open.</summary>
    private void DrawChip()
    {
        bool show = run.Active && Current == View.None && !EcoUI.WorldCovered && EcoUI.Mode != EcoUI.HudMode.Hidden;
        ChipVisible = show;
        if (!show) { if (Event.current.type == EventType.Repaint) ChipBottomPixels = 0f; return; }
        float pixels = Screen.width / Mathf.Max(1f, EcoTheme.Width);
        float width = 300f, rowH = 22f, height = 62f + run.Goals.Count * rowH;
        var rect = new Rect(EcoTheme.Width - width - 16f, EcoQuestHUD.RightColumnBottomPixels / pixels + 8f, width, height);
        EcoTheme.Panel(rect, false);
        var clock = EcoWorldClock.Current;
        string day = run.Ended ? "Run bitti · not " + run.State.grade : "Gün " + Mathf.Min(run.RunDay, EcoRun.Days) + "/" + EcoRun.Days
            + (clock != null ? " · " + Mathf.FloorToInt(clock.hour).ToString("00") + ":" + Mathf.FloorToInt(clock.hour % 1f * 60f).ToString("00") : "")
            + (run.FinaleOpen ? " · fırtına gecesi" : "");
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, width - 24f, 22f), day, new GUIStyle(EcoTheme.Muted) { fontSize = 15, fontStyle = FontStyle.Bold });
        float health = run.Health;
        Bar(new Rect(rect.x + 12f, rect.y + 34f, width - 90f, 7f), health / 100f, health < EcoRun.CollapseHealth ? EcoSettings.Bad : health < 50f ? EcoUI.Amber : EcoSettings.Good);
        GUI.Label(new Rect(rect.xMax - 74f, rect.y + 26f, 64f, 22f), "%" + health.ToString("0"), small);
        float y = rect.y + 50f;
        foreach (var goal in run.Goals)
        {
            var spec = goal.Spec;
            EcoTheme.Icon(new Rect(rect.x + 12f, y + 2f, 16f, 16f), goal.done ? "ui_check" : spec.icon);
            GUI.Label(new Rect(rect.x + 34f, y, width - 110f, rowH), (goal.required ? "" : "+ ") + spec.title, small);
            Bar(new Rect(rect.xMax - 72f, y + 8f, 60f, 5f), goal.done ? 1f : goal.progress, EcoSettings.Good);
            y += rowH;
        }
        if (Event.current.type == EventType.Repaint) ChipBottomPixels = rect.yMax * pixels;
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) ShowGoals();
    }

    private static void Bar(Rect rect, float fill, Color color)
    {
        var previous = GUI.color;
        GUI.color = EcoNotebook.Inset; GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static string RegionIcon(EcoRegionInfo.Kind kind)
    {
        switch (kind)
        {
            case EcoRegionInfo.Kind.Glacier: return "region_glacier";
            case EcoRegionInfo.Kind.Wind: return "region_wind";
            case EcoRegionInfo.Kind.City: return "region_city";
            case EcoRegionInfo.Kind.Sun: return "region_sun";
            case EcoRegionInfo.Kind.Industry: return "region_industry";
            case EcoRegionInfo.Kind.Lake: return "region_lake";
            default: return "poi_tree";
        }
    }
}
