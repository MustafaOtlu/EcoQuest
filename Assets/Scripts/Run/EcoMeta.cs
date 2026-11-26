using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Permanent profile across runs: green credits, unlocks and best grades. A separate JSON file, so loading
/// or losing a run save never touches it.</summary>
public static class EcoMeta
{
    [Serializable]
    public sealed class Profile
    {
        public int credits, runs, wins, bestScore = -1;
        public string bestGrade = "";
        public List<string> unlocks = new List<string>();
        public List<string> history = new List<string>();
    }

    public sealed class Unlock
    {
        public string id, title, text, icon, group;
        public int cost;
        /// <summary>Unlock ids of which at least one must be owned first (empty: a root of the tree).</summary>
        public string[] requires = new string[0];
    }

    public static readonly string[] Groups = { "Başlangıç", "Uzmanlık", "Sabah ve kriz", "Zorluk" };

    /// <summary>The unlock tree (GDD v5 §4). Each branch starts cheap; deeper nodes need an earlier one.</summary>
    public static readonly Unlock[] Unlocks =
    {
        new Unlock { id = "start_grant", group = "Başlangıç", title = "Başlangıç hibesi", text = "Her run +20 metal, +20 plastikle başlar", icon = "metal", cost = 30 },
        new Unlock { id = "seed_bank", group = "Başlangıç", title = "Tohum bankası", text = "Her run +10 ağaç tohumuyla başlar", icon = "seed", cost = 35 },
        new Unlock { id = "blueprints", group = "Başlangıç", title = "Su planları", text = "Pompa, arıtma ve depo tarifleri her run'da baştan açık", icon = "water", cost = 45, requires = new[] { "start_grant" } },
        new Unlock { id = "efficient_tools", group = "Başlangıç", title = "Verimli aletler", text = "Aletlerin elektrik harcaması %15 az", icon = "electricity", cost = 50, requires = new[] { "start_grant", "seed_bank" } },

        new Unlock { id = "branch_water", group = "Uzmanlık", title = "Su dalı", text = "[K] uzmanlıkta Su dalını açar (Enerji dalı herkese açık)", icon = "water", cost = 40 },
        new Unlock { id = "branch_ecology", group = "Uzmanlık", title = "Ekoloji dalı", text = "[K] uzmanlıkta Ekoloji dalını açar", icon = "biodiversity", cost = 40 },
        new Unlock { id = "spec_master", group = "Uzmanlık", title = "Usta", text = "Uzmanlık dalları 2 yerine 3. kademeye çıkabilir", icon = "yep", cost = 80, requires = new[] { "branch_water", "branch_ecology" } },

        new Unlock { id = "early_warning", group = "Sabah ve kriz", title = "Erken uyarı", text = "Akşam krizini öğleden haber verir", icon = "clock", cost = 40 },
        new Unlock { id = "card_reroll", group = "Sabah ve kriz", title = "Kart yenileme", text = "Günde bir kez sabah kartlarını yenile", icon = "ui_journal", cost = 45 },
        new Unlock { id = "fourth_card", group = "Sabah ve kriz", title = "Geniş seçenek", text = "Sabahları 3 yerine 4 kart", icon = "ui_journal", cost = 60, requires = new[] { "card_reroll", "early_warning" } },

        new Unlock { id = "crisis_mode", group = "Zorluk", title = "Kriz zorluğu", text = "Daha kirli vadiler, daha güçlü krizler, 1,4× kredi", icon = "poi_warning", cost = 50 },
    };

    public static bool Available(Unlock unlock) => unlock.requires.Length == 0 || unlock.requires.Any(Has);

    /// <summary>The Energy branch is open to everyone; the other two are bought.</summary>
    public static bool BranchOpen(int branch) => branch == 0 || branch == 1 && Has("branch_water") || branch == 2 && Has("branch_ecology");
    public static int MaxSpecRank => Has("spec_master") ? 3 : 2;

    private static Profile profile;
    public static string FilePath => Path.Combine(Application.persistentDataPath, "EcoQuest", "meta.json");

    public static Profile Current
    {
        get
        {
            if (profile != null) return profile;
            try
            {
                if (File.Exists(FilePath)) profile = JsonUtility.FromJson<Profile>(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { Debug.LogWarning("EcoMeta: profil okunamadı, yenisi açılıyor. " + error.Message); }
            profile ??= new Profile();
            profile.unlocks ??= new List<string>();
            profile.history ??= new List<string>();
            return profile;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => profile = null;

    public static bool Has(string unlock) => Current.unlocks.Contains(unlock);

    public static bool Buy(string id)
    {
        var unlock = Unlocks.FirstOrDefault(u => u.id == id);
        if (unlock == null || Has(id) || !Available(unlock) || Current.credits < unlock.cost) return false;
        Current.credits -= unlock.cost; Current.unlocks.Add(id); Save(); return true;
    }

    public static void RecordRun(string valley, string grade, int score, int credits, bool won)
    {
        var p = Current;
        p.runs++; if (won) p.wins++;
        p.credits += Mathf.Max(0, credits);
        if (score > p.bestScore) { p.bestScore = score; p.bestGrade = grade; }
        p.history.Add(DateTime.Now.ToString("yyyy-MM-dd") + " · " + valley + " · " + grade + " · " + score);
        while (p.history.Count > 20) p.history.RemoveAt(0);
        Save();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(Current, true), Encoding.UTF8);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak"); else File.Move(temporary, FilePath);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        { Debug.LogWarning("EcoMeta: profil kaydedilemedi. " + error.Message); }
    }
}
