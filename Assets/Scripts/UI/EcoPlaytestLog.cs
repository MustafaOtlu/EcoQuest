using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

// GDD v4 §18 / Ö6: a small local event log for playtests. One CSV per session under
// <persistentDataPath>/playtest/: real time, game day and hour, event, detail. It records when the objective step
// changes, when a project completes, faints, idle stretches (10 s or more without input, pause excluded) and the
// session end, so the playtest hypotheses (time to the first project, idle share, where people stop) can be measured.
public sealed class EcoPlaytestLog : MonoBehaviour
{
    private const float IdleThreshold = 10f;
    private static EcoPlaytestLog current;
    private string path;
    private string lastObjective = "", lastObjectiveText = "";
    private float lastInput, idleSince = -1f, idleTotal;
    private bool wasRecovering;
    private PlayerVitals vitals;
    private EcoProjects projects;

    public static string FilePath => current != null ? current.path : "";

    private void Awake()
    {
        current = this;
        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "playtest");
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv");
            File.WriteAllText(path, "real_seconds,day,hour,event,detail\n");
        }
        catch (Exception e) { Debug.LogWarning("Playtest günlüğü açılamadı: " + e.Message); path = null; }
        lastInput = Time.realtimeSinceStartup;
        Write("oturum_basladi", Application.version);
    }

    private void OnDestroy() { if (current == this) current = null; }

    /// <summary>Logs the objective card when its step changes (counters such as "4/10" do not count as a change).</summary>
    public static void Objective(string text)
    {
        if (current == null || string.IsNullOrEmpty(text)) return;
        string step = Regex.Replace(text, @"\d+", "#");
        if (step == current.lastObjective) return;
        current.lastObjective = step; current.lastObjectiveText = text;
        current.Write("hedef", text);
    }

    private void Update()
    {
        if (projects == null && EcoProjects.Current != null) { projects = EcoProjects.Current; projects.Completed += OnProjectCompleted; }
        if (vitals == null) vitals = FindFirstObjectByType<PlayerVitals>();
        if (vitals != null && vitals.IsRecovering != wasRecovering)
        {
            wasRecovering = vitals.IsRecovering;
            if (wasRecovering) Write("bayilma", vitals.transform.position.ToString("F0"));
        }
        TrackIdle();
    }

    private void TrackIdle()
    {
        float now = Time.realtimeSinceStartup;
        var keyboard = Keyboard.current; var mouse = Mouse.current;
        bool input = (keyboard != null && keyboard.anyKey.isPressed)
            || (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0.5f || mouse.leftButton.isPressed || mouse.rightButton.isPressed
                || Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f));
        // A paused game or an open menu is not idleness in play; restart the clock instead.
        if (input || PlayerController.Paused)
        {
            if (idleSince >= 0f)
            {
                float idle = now - idleSince;
                idleTotal += idle;
                Write("bosta_bitti", idle.ToString("0", CultureInfo.InvariantCulture) + " sn");
                idleSince = -1f;
            }
            lastInput = now;
            return;
        }
        if (idleSince < 0f && now - lastInput >= IdleThreshold) { idleSince = lastInput; Write("bosta_basladi", lastObjectiveText); }
    }

    private void OnProjectCompleted(int index)
    {
        string name = projects != null && index >= 0 && index < projects.All.Count ? projects.All[index].name : index.ToString();
        Write("proje_tamam", name);
    }

    private void OnApplicationQuit()
    {
        if (idleSince >= 0f) idleTotal += Time.realtimeSinceStartup - idleSince;
        Write("oturum_bitti", "toplam boşta " + idleTotal.ToString("0", CultureInfo.InvariantCulture) + " sn");
    }

    private void Write(string kind, string detail)
    {
        if (string.IsNullOrEmpty(path)) return;
        var clock = EcoWorldClock.Current;
        string line = Time.realtimeSinceStartup.ToString("0.0", CultureInfo.InvariantCulture) + ","
            + (clock != null ? clock.day.ToString(CultureInfo.InvariantCulture) : "") + ","
            + (clock != null ? clock.hour.ToString("0.00", CultureInfo.InvariantCulture) : "") + ","
            + kind + ",\"" + (detail ?? "").Replace("\"", "'") + "\"\n";
        try { File.AppendAllText(path, line); }
        catch (Exception e) { Debug.LogWarning("Playtest günlüğü yazılamadı: " + e.Message); path = null; }
    }
}
