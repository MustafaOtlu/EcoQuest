using System.Linq;
using NUnit.Framework;

/// <summary>Phase 3 content: the unlock tree is well formed and the card, crisis and goal pools are consistent.</summary>
public sealed class EcoMetaTests
{
    [Test]
    public void UnlockTreeIsWellFormed()
    {
        var ids = EcoMeta.Unlocks.Select(u => u.id).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "duplicate unlock id");
        foreach (var unlock in EcoMeta.Unlocks)
        {
            Assert.Contains(unlock.group, EcoMeta.Groups, unlock.id);
            Assert.Greater(unlock.cost, 0, unlock.id);
            foreach (var need in unlock.requires)
            {
                var parent = EcoMeta.Unlocks.FirstOrDefault(u => u.id == need);
                Assert.IsNotNull(parent, unlock.id + " needs unknown " + need);
                Assert.AreEqual(unlock.group, parent.group, unlock.id + " needs a node from another branch");
                Assert.AreEqual(0, parent.requires.Length, unlock.id + ": the tree is two levels deep");
            }
        }
        // Every group has a root a new profile can buy.
        foreach (var group in EcoMeta.Groups)
            Assert.IsTrue(EcoMeta.Unlocks.Any(u => u.group == group && u.requires.Length == 0), group);
    }

    [Test]
    public void CardsAndCrisesHaveUniqueIdsAndText()
    {
        var cards = EcoDayCards.All.Select(c => c.id).ToList();
        Assert.AreEqual(cards.Count, cards.Distinct().Count());
        Assert.GreaterOrEqual(cards.Count, 19);
        foreach (var card in EcoDayCards.All) Assert.IsFalse(string.IsNullOrEmpty(card.title) || string.IsNullOrEmpty(card.text) || string.IsNullOrEmpty(card.icon), card.id);
        var crises = EcoCrisis.All.Select(c => c.id).ToList();
        Assert.AreEqual(crises.Count, crises.Distinct().Count());
        Assert.GreaterOrEqual(crises.Count, 10);
    }

    [Test]
    public void EveryProblemCanDrawEveryCrisisButNotTwiceInARow()
    {
        for (int seed = 1; seed < 60; seed++)
            foreach (EcoProblem problem in System.Enum.GetValues(typeof(EcoProblem)))
            {
                string yesterday = "";
                for (int day = 1; day <= 5; day++)
                {
                    string id = EcoCrisis.Draw(seed, day, problem, yesterday);
                    Assert.IsNotNull(EcoCrisis.Find(id));
                    Assert.AreNotEqual(yesterday, id);
                    yesterday = id;
                }
            }
    }

    [Test]
    public void EnergyPlansOpenWindAndHydroWithoutTheProjects()
    {
        var saved = EcoDiscoveries.Capture();
        try
        {
            EcoDiscoveries.Restore(new[] { "recipe.wind" });
            Assert.IsFalse(EcoDiscoveries.CanBuild("wind"), "wind still waits for the first energy project");
            Assert.IsFalse(EcoDiscoveries.CanBuild("hydro"));
            EcoDiscoveries.Restore(new[] { "plan.energy" });
            Assert.IsTrue(EcoDiscoveries.CanBuild("wind"));
            Assert.IsTrue(EcoDiscoveries.CanBuild("hydro"));
            Assert.IsNotNull(EcoMeta.Unlocks.FirstOrDefault(u => u.id == "energy_plans"));
        }
        finally { EcoDiscoveries.Restore(saved); }
    }

    [Test]
    public void EnvironmentMoodFollowsThePanel()
    {
        // Clean values show nothing; the worst values show the full effect; worse never looks better.
        Assert.AreEqual(0f, EcoEnvironmentMood.SmogFor(36f), 1e-4f, "good air (AQI 36) is clear");
        Assert.AreEqual(1f, EcoEnvironmentMood.SmogFor(250f), 1e-4f);
        Assert.AreEqual(0f, EcoEnvironmentMood.HeatFor(11f), 1e-4f, "carbon load 11 gives no haze");
        Assert.AreEqual(0f, EcoEnvironmentMood.GloomFor(84f), 1e-4f, "health 84 keeps full colour");
        Assert.AreEqual(1f, EcoEnvironmentMood.GloomFor(20f), 1e-4f);
        for (float v = 0f; v < 300f; v += 5f)
        {
            Assert.LessOrEqual(EcoEnvironmentMood.SmogFor(v), EcoEnvironmentMood.SmogFor(v + 5f));
            Assert.LessOrEqual(EcoEnvironmentMood.HeatFor(v / 5f), EcoEnvironmentMood.HeatFor(v / 5f + 1f));
            Assert.GreaterOrEqual(EcoEnvironmentMood.GloomFor(v / 3f), EcoEnvironmentMood.GloomFor(v / 3f + 1f));
        }
    }

    [Test]
    public void WindValleysCanRollTheWindGoal()
    {
        bool seen = false;
        for (int seed = 1; seed < 400 && !seen; seed++)
        {
            var scenario = EcoScenario.Roll(seed, EcoDifficulty.Normal);
            var layout = EcoValley.Layout(scenario);
            var goals = EcoGoalCatalog.Pick(scenario, EcoValley.Kinds(layout));
            if (goals.Any(g => g.id == "wind_farm"))
            {
                Assert.Contains(EcoTile.Wind, layout, "wind goal without wind hills");
                seen = true;
            }
        }
        Assert.IsTrue(seen, "the wind goal never comes up");
    }
}
