using System.Linq;
using NUnit.Framework;

/// <summary>Valley layouts are pure functions of the run seed and always hold the tiles a run needs.</summary>
public sealed class EcoValleyTests
{
    [Test]
    public void SameSeedGivesSameLayout()
    {
        for (int seed = 0; seed < 50; seed++)
            CollectionAssert.AreEqual(EcoValley.Layout(seed * 7919, false), EcoValley.Layout(seed * 7919, false));
    }

    [Test]
    public void EveryLayoutHasFourDistinctTilesWithDirtyLakeAndFactories()
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var layout = EcoValley.Layout(seed, false);
            Assert.AreEqual(4, layout.Length);
            Assert.AreEqual(4, layout.Distinct().Count(), EcoValley.Code(layout));
            Assert.Contains(EcoTile.DirtyLake, layout);
            Assert.Contains(EcoTile.Industry, layout);
        }
    }

    [Test]
    public void SeedsProduceManyLayouts()
    {
        int distinct = Enumerable.Range(0, 300).Select(s => EcoValley.Code(EcoValley.Layout(s, false))).Distinct().Count();
        Assert.Greater(distinct, 30);
    }

    [Test]
    public void CodeRoundTrips()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var layout = EcoValley.Layout(seed, false);
            CollectionAssert.AreEqual(layout, EcoValley.Parse(EcoValley.Code(layout)));
        }
        Assert.IsNull(EcoValley.Parse(""));
        Assert.IsNull(EcoValley.Parse("Lake-Grove"));
        Assert.IsNull(EcoValley.Parse("Lake-Grove-Volcano-Industry"));
    }

    [Test]
    public void TrainingValleyIsFixed()
    {
        CollectionAssert.AreEqual(EcoValley.TrainingLayout, EcoValley.Layout(12345, true));
        CollectionAssert.AreEqual(EcoValley.TrainingLayout, EcoValley.Layout(EcoScenario.Training()));
    }

    [Test]
    public void KindsCoverFixedAndRolledTiles()
    {
        var layout = new[] { EcoTile.Wind, EcoTile.DirtyLake, EcoTile.Industry, EcoTile.Grove };
        var kinds = EcoValley.Kinds(layout);
        Assert.IsTrue(kinds.Contains(EcoRegionInfo.Kind.Glacier) && kinds.Contains(EcoRegionInfo.Kind.Sun) && kinds.Contains(EcoRegionInfo.Kind.City));
        Assert.IsTrue(kinds.Contains(EcoRegionInfo.Kind.Wind) && kinds.Contains(EcoRegionInfo.Kind.Grove));
        Assert.IsTrue(kinds.Contains(EcoRegionInfo.Kind.Industry) && kinds.Contains(EcoRegionInfo.Kind.Lake));
        Assert.AreEqual(7, EcoValley.Rows(layout).Count);
    }

    [Test]
    public void GoalsOnlyUseKindsTheValleyHas()
    {
        for (int seed = 1; seed < 200; seed++)
        {
            var scenario = EcoScenario.Roll(seed, EcoDifficulty.Normal);
            var kinds = EcoValley.Kinds(EcoValley.Layout(scenario));
            foreach (var goal in EcoGoalCatalog.Pick(scenario, kinds))
            {
                var def = EcoGoalCatalog.Find(goal.id);
                Assert.IsNotNull(def, goal.id);
                Assert.IsTrue(kinds.Contains(def.kind), seed + ": " + goal.id);
            }
        }
    }
}
