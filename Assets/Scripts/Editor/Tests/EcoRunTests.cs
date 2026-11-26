using System.Linq;
using NUnit.Framework;

/// <summary>Run rolls are pure functions of seed and difficulty: same input, same valley; different seeds, different valleys.</summary>
public sealed class EcoRunTests
{
    [Test]
    public void SameSeedGivesSameScenarioAndGoals()
    {
        var a = EcoScenario.Roll(424242, EcoDifficulty.Normal);
        var b = EcoScenario.Roll(424242, EcoDifficulty.Normal);
        Assert.AreEqual(a.valleyName, b.valleyName);
        Assert.AreEqual(a.problem, b.problem);
        CollectionAssert.AreEqual(a.severity, b.severity);
        CollectionAssert.AreEqual(a.greenery, b.greenery);
        Assert.AreEqual(a.fires, b.fires);
        Assert.AreEqual(a.wastePiles, b.wastePiles);
        CollectionAssert.AreEqual(EcoGoalCatalog.Pick(a).Select(g => g.id + g.required + g.threshold).ToArray(),
            EcoGoalCatalog.Pick(b).Select(g => g.id + g.required + g.threshold).ToArray());
        for (int day = 1; day <= 5; day++)
        {
            CollectionAssert.AreEqual(EcoDayCards.Draw(a.seed, day, a.problem, null, 3), EcoDayCards.Draw(b.seed, day, b.problem, null, 3));
            Assert.AreEqual(EcoCrisis.Draw(a.seed, day, a.problem, ""), EcoCrisis.Draw(b.seed, day, b.problem, ""));
        }
    }

    [Test]
    public void DifferentSeedsGiveDifferentValleys()
    {
        var signatures = Enumerable.Range(0, 20).Select(i =>
        {
            var s = EcoScenario.Roll(1000 + i * 7919, EcoDifficulty.Normal);
            return s.valleyName + s.problem + string.Join(",", EcoGoalCatalog.Pick(s).Select(g => g.id));
        }).Distinct().Count();
        Assert.Greater(signatures, 12);
    }

    [Test]
    public void EveryRunHasTwoRequiredAndOneBonusFromDifferentGroups()
    {
        for (int i = 0; i < 200; i++)
        {
            var goals = EcoGoalCatalog.Pick(EcoScenario.Roll(i * 104729 + 17, (EcoDifficulty)(i % 3)));
            Assert.AreEqual(2, goals.Count(g => g.required));
            Assert.AreEqual(1, goals.Count(g => !g.required));
            Assert.AreEqual(3, goals.Select(g => g.Spec.group).Distinct().Count());
            Assert.IsFalse(goals.Any(g => g.Spec.trainingOnly));
        }
    }

    [Test]
    public void DifficultyMovesThresholdsNotGoals()
    {
        var calm = EcoGoalCatalog.Pick(EcoScenario.Roll(5150, EcoDifficulty.Calm));
        var crisis = EcoGoalCatalog.Pick(EcoScenario.Roll(5150, EcoDifficulty.Crisis));
        CollectionAssert.AreEqual(calm.Select(g => g.id).ToArray(), crisis.Select(g => g.id).ToArray());
        for (int i = 0; i < calm.Count; i++)
        {
            var spec = calm[i].Spec;
            if (spec.metric == EcoGoalMetric.Project) continue;
            bool lowerIsHarder = spec.metric == EcoGoalMetric.IndustryAqi || spec.metric == EcoGoalMetric.WorldCarbon || spec.metric == EcoGoalMetric.WasteCleared;
            if (lowerIsHarder) Assert.Less(crisis[i].threshold, calm[i].threshold, spec.id);
            else Assert.Greater(crisis[i].threshold, calm[i].threshold, spec.id);
        }
        Assert.Greater(EcoScenario.DifficultyScale(EcoDifficulty.Crisis), EcoScenario.DifficultyScale(EcoDifficulty.Calm));
    }

    [Test]
    public void NonRepeatableCardsAreNotOfferedAgain()
    {
        var taken = new[] { "wind_survey", "panel_cleaning", "bike_lanes", "science_team" };
        for (int day = 1; day <= 5; day++)
            foreach (var id in EcoDayCards.Draw(77, day, EcoProblem.Smog, taken, 4))
                CollectionAssert.DoesNotContain(taken, id);
    }

    [Test]
    public void TrainingValleyIsFixed()
    {
        var training = EcoScenario.Training();
        Assert.IsTrue(training.training);
        Assert.AreEqual(EcoScenario.TrainingSeed, training.seed);
        CollectionAssert.AreEqual(new[] { "energy_project", "lake_project", "waste_cleared" }, EcoGoalCatalog.Pick(training).Select(g => g.id).ToArray());
        Assert.AreEqual(1f, training.Pollution(EcoRegionInfo.Kind.Industry, 50f) / 50f, 1e-4f);
    }
}
