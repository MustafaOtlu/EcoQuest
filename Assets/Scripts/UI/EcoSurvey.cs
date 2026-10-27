using System;
using System.IO;
using UnityEngine;

// GDD §16: optional five-question learning check, asked before play and again after the final. Answers are written to
// the player's local save folder (survey-v2-pre.json / survey-v2-post.json; v2 = the sustainability questions); nothing is sent anywhere.
public sealed class EcoSurvey : MonoBehaviour
{
    public static EcoSurvey Current { get; private set; }
    // Plain everyday sustainability questions; each one is also something the game itself shows.
    private static readonly string[] Questions =
    {
        "Hangisi yenilenebilir bir enerji kaynağıdır?",
        "Bir göle fabrikadan kirli su akıyorsa ilk ne yapılmalı?",
        "Havadaki karbondioksit (CO₂) artarsa ne olur?",
        "Geri dönüşümün en önemli faydası nedir?",
        "Güneş paneli gece elektrik üretmez. Gece ışık yakmak için ne gerekir?",
    };
    private static readonly string[][] Options =
    {
        new[] { "Kömür", "Rüzgâr", "Doğalgaz" },
        new[] { "Kirli suyun geldiği yeri kapatmak", "Göle daha çok balık bırakmak", "Gölün suyunu boşaltmak" },
        new[] { "Dünya soğur", "Hiçbir şey değişmez", "Daha çok ısı tutulur, iklim ısınır" },
        new[] { "Ham madde ve enerji tasarrufu sağlar", "Çöpü daha hızlı çürütür", "Fabrikaları kapatır" },
        new[] { "Gündüz fazla elektriği bataryada saklamak", "Paneli lambaya çevirmek", "Daha fazla panel kurmak" },
    };
    private static readonly int[] Correct = { 1, 0, 2, 0, 0 };

    [Serializable] private sealed class Result { public string kind, savedAt; public int[] answers; public int score; }
    private bool open, post;
    private GUIStyle option;
    public bool IsOpen => open;
    private readonly int[] answers = { -1, -1, -1, -1, -1 };
    private bool postAsked;

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }
    public static bool Done(bool post) => File.Exists(PathFor(post));
    private static string PathFor(bool post) => Path.Combine(Application.persistentDataPath, "EcoQuest", post ? "survey-v2-post.json" : "survey-v2-pre.json");

    public void Open(bool afterFinal)
    {
        post = afterFinal; open = true;
        for (int i = 0; i < answers.Length; i++) answers[i] = -1;
        EcoUIFlow.SetPanel(this, true);
    }
    private void Close() { open = false; EcoUIFlow.SetPanel(this, false); }

    private void Update()
    {
        var finale = EcoFinale.Current;
        if (!postAsked && finale != null && finale.Won && !Done(true) && !open) { postAsked = true; Open(true); }
    }

    private void OnGUI()
    {
        if (!open) return;
        EcoTheme.Begin();
        if (option == null) { option = new GUIStyle(EcoTheme.Button) { wordWrap = true, fontSize = 14, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 4, 6) }; }
        var r = EcoTheme.Window(780f, 640f, post ? "Sürdürülebilirlik anketi (son)" : "Sürdürülebilirlik anketi (ön)", "İsteğe bağlı • 5 soru • yanıtlar yalnız bu bilgisayarda kalır.", "ui_journal");
        float x = r.x + 40f, w = r.width - 80f, y = r.y + 100f;
        for (int q = 0; q < 5; q++)
        {
            GUI.Label(new Rect(x, y + q * 90f, w, 26f), (q + 1) + ". " + Questions[q], EcoTheme.Body);
            for (int o = 0; o < 3; o++)
            {
                bool on = answers[q] == o; var prev = GUI.color; if (on) GUI.color = EcoSettings.Good;
                // Two-line answers need the taller 54 px wood; at 46 px they ran into the frame.
                if (GUI.Button(new Rect(x + o * (w / 3f), y + q * 90f + 28f, w / 3f - 6f, 54f), Options[q][o], option)) answers[q] = o;
                GUI.color = prev;
            }
        }
        bool complete = Array.TrueForAll(answers, a => a >= 0);
        GUI.enabled = complete;
        if (GUI.Button(new Rect(x, r.yMax - 64f, w * 0.5f - 6f, 40f), "Gönder", EcoTheme.Button)) { Save(); Close(); }
        GUI.enabled = true;
        if (GUI.Button(new Rect(x + w * 0.5f + 6f, r.yMax - 64f, w * 0.5f - 6f, 40f), "Şimdi değil", EcoTheme.Button)) Close();
        EcoTheme.End();
    }

    private void Save()
    {
        int score = 0; for (int i = 0; i < 5; i++) if (answers[i] == Correct[i]) score++;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PathFor(post)));
            File.WriteAllText(PathFor(post), JsonUtility.ToJson(new Result { kind = post ? "post" : "pre", savedAt = DateTime.UtcNow.ToString("o"), answers = (int[])answers.Clone(), score = score }));
            FindFirstObjectByType<PlayerWeaponSystem>()?.ShowFeedback("Anket kaydedildi • " + score + "/5 doğru");
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("Anket kaydedilemedi: " + error.Message); }
    }
}
