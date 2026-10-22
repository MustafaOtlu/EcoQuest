using System;
using System.IO;
using UnityEngine;

// GDD §17: performance probe. [F3] shows frame rate, the slowest 1 % of frames, memory and scene object counts; a CSV line
// is appended to the local save folder every 10 s while the overlay has been used. Target: >= 30 FPS average.
public sealed class EcoPerf : MonoBehaviour
{
    public static EcoPerf Current { get; private set; }
    private const int Window = 600;
    private readonly float[] frames = new float[Window];
    private int count, head;
    private bool show;
    private float nextLog, nextAppend;
    public float AverageFps { get; private set; }
    public float Low1PercentFps { get; private set; }
    private string line = "";

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame) show = !show;
        float dt = Time.unscaledDeltaTime; if (dt <= 0f) return;
        frames[head] = dt; head = (head + 1) % Window; count = Mathf.Min(count + 1, Window);
        if (Time.unscaledTime < nextLog) return;
        nextLog = Time.unscaledTime + 1f; Compute();
        line = "FPS " + AverageFps.ToString("0") + " • alt %1 " + Low1PercentFps.ToString("0") + " • bellek " + (GC.GetTotalMemory(false) / 1048576f).ToString("0") + " MB • yapı "
            + FindObjectsByType<EcoStructure>(FindObjectsSortMode.None).Length + " • tehdit " + FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).Length;
        if (show && Time.unscaledTime >= nextAppend) { nextAppend = Time.unscaledTime + 10f; Append(); }
    }

    public void Compute()
    {
        if (count == 0) return;
        var copy = new float[count]; Array.Copy(frames, copy, count); Array.Sort(copy);
        float sum = 0f; foreach (float f in copy) sum += f;
        AverageFps = count / sum;
        int tail = Mathf.Max(1, count / 100); float worst = 0f; for (int i = count - tail; i < count; i++) worst += copy[i];
        Low1PercentFps = tail / worst;
    }
    private void Append()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "EcoQuest", "perf.csv"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, DateTime.UtcNow.ToString("o") + "," + AverageFps.ToString("0.0") + "," + Low1PercentFps.ToString("0.0") + "\n");
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { }
    }
    private void OnGUI()
    {
        if (!show) return;
        GUI.Box(new Rect(10f, Screen.height - 54f, 620f, 30f), line);
    }
}
