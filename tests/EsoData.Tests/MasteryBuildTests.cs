using EsoData.Accounts;
using EsoData.Builds;
using EsoData.Catalogs;

namespace EsoData.Tests;

public sealed class MasteryBuildTests
{
    [Fact]
    public void CatalogDistinguishesMasteriesFromUnknownNegativeSentinels()
    {
        var catalog = UespCatalog.Parse("""
            {"minedSkills":[{"id":1,"isMastery":1},{"id":2,"isMastery":-1},{"id":3,"isMastery":0}]}
            """);
        Assert.True(catalog.Skills[1].IsMastery); Assert.Null(catalog.Skills[2].IsMastery);
        Assert.False(catalog.Skills[3].IsMastery);
        Assert.True(GameCatalog.FromJson(catalog.ToJson()).Skills[1].IsMastery);
    }

    [Fact]
    public void MasterySwapPreservesObservedPointBudgetEvenWhenAutomaticSkillsDoNotReconcile()
    {
        var catalog = new GameCatalog();
        foreach (var id in new long[] { 1, 2, 3, 4 }) catalog.Skills[id] = new(id, BaseAbilityId: id, Rank: 1, IsPassive: true, IsMastery: true);
        var character = new EsoCharacter { Progress = new() { TotalSkillPoints = 10, UnspentSkillPoints = 2 } };
        character.Build.Skills = new() { [1] = new() { IsPassive = true }, [2] = new() { IsPassive = true }, [99] = new() { IsPassive = true, Rank = 20 } };
        var build = character.Build.DeepClone(); build.Sections = BuildSections.Skills;
        build.Skills.Remove(2); build.Skills[3] = new() { IsPassive = true };
        var report = BuildAnalysis.Validate(character, build, catalog);
        Assert.Equal(8, report.SkillPoints!.Desired); Assert.Equal(0, report.SkillPoints.IncrementalCost);
        Assert.DoesNotContain(report.Findings, f => f.Code is "skill-budget" or "point-cost-coverage" or "mastery-budget");
        build.Skills[4] = new() { IsPassive = true };
        Assert.Contains(BuildAnalysis.Validate(character, build, catalog).Findings, f => f.Code == "mastery-budget");
    }
}
