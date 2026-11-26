#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Editor-only balance probe (GDD v5 §6): plays a run as an idle player at high speed. It takes the first morning card,
/// dismisses every crisis and never builds anything, so the log shows what the valley does on its own: how health
/// drifts, how hard each crisis hits and whether doing nothing can still pass (it must not). One line per day and a
/// summary go to the console as BALANCE_PROBE.
/// </summary>
public sealed class EcoBalanceProbe : MonoBehaviour
{
    public float speed = 30f;
    private EcoRun run;
    private int lastDay = -1;
    private static readonly MethodInfo CloseView = typeof(EcoRunUI).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);

    public static EcoBalanceProbe Attach(float speed)
    {
        var probe = new GameObject("Balance probe").AddComponent<EcoBalanceProbe>();
        probe.speed = speed;
        return probe;
    }

    private void Update()
    {
        run ??= EcoRun.Current;
        if (run == null || !run.Active) return;
        var ui = run.GetComponent<EcoRunUI>();
        if (run.Ended)
        {
            Time.timeScale = 1f;
            Debug.Log("BALANCE_PROBE END " + EcoValleyAssembler.Current?.Code + " seed " + run.State.seed + " " + run.Outcome + " grade " + run.State.grade
                + " score " + run.State.score + " health " + string.Join("/", run.State.health.Select(h => h.ToString("0")))
                + " goals " + string.Join(" ", run.Goals.Select(g => g.id + (g.done ? "+" : "-"))) + " crises " + string.Join(" ", run.State.crises));
            if (ui != null && ui.Current != EcoRunUI.View.None) CloseView.Invoke(ui, null);
            Destroy(gameObject);
            return;
        }
        if (run.State.offerOpen) run.ChooseCard(run.State.offer[0]);
        if (ui != null && (ui.Current == EcoRunUI.View.Crisis || ui.Current == EcoRunUI.View.Cards)) CloseView.Invoke(ui, null);
        Time.timeScale = speed;
        if (run.RunDay != lastDay)
        {
            lastDay = run.RunDay;
            Debug.Log("BALANCE_PROBE day " + run.RunDay + " health " + EcoRun.ValleyHealth().ToString("0") + " carbon " + EcoRegion.WorldCarbon().ToString("0.0")
                + " last crisis " + run.LastCrisis + " (" + run.LastCrisisResult + ") waste " + EcoRun.WastePiles());
        }
    }

    private void OnDestroy() { if (Time.timeScale > 1f) Time.timeScale = 1f; }
}
#endif
