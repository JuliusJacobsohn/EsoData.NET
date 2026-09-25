using EsoData.Accounts;
using EsoData.Builds;
using EsoData.Catalogs;

namespace EsoData.Tests;

public class GuideSetupTests
{
    [Fact]
    public void GuideRoundtripPreservesChoicesScriptsAndUnspecifiedPoints()
    {
        var target = new GuideSetup { Variant = "Dungeon boss", Abilities = [new("front.1", "Scribed", Scripts: new("Taunt", "Heal", "Maim"))],
            Equipment = [new(0, "Set", "Medium", ["Sturdy", "Divines"], "Tri-Stat")], Champion = [new("Warfare", "Star")] };
        var original = new BuildPlan();
        var edited = BuildEditor.Apply(original, new() { Target = target }, new());
        target.Equipment.Clear();
        Assert.Null(original.Target);
        Assert.Equal(2, Assert.Single(edited.Target!.Equipment).Traits.Length);
        Assert.Null(Assert.Single(edited.Target.Champion).Points);
        Assert.Equal("Taunt", Assert.Single(edited.Target.Abilities).Scripts!.Focus);
        Assert.Throws<ArgumentException>(() => BuildEditor.Apply(original, new() { Target = new() { Variant = "Invalid", Abilities = [new("front.7", "Skill")] } }, new()));
    }
    [Fact]
    public void GuideComparisonUsesExactMorphAndDoesNotClaimUnknownRecipesOrRanks()
    {
        var account = new EsoAccount { Characters = [new() { Id = "1", Race = "Argonian", Progress = new() { Skills = [new(1, "Base", 4, false)] },
            Build = new() { Sections = BuildSections.Bars | BuildSections.Equipment, Bars = new() { Front = [1, null, null, null, null, null] },
                Equipment = new() { [0] = new() { SetId = 10 } } } }] };
        var catalog = new GameCatalog { Skills = new() { [1] = new(1, "Base", 1, 4, 0, false) },
            Sets = new() { [10] = new(10, new Dictionary<string, string> { ["en"] = "Set" }, []) } };
        var target = new GuideSetup { Variant = "Boss", PreferredRace = "Nord", Abilities = [new("front.1", "Morph", TargetRank: 4, Scripts: new("A", "B", "C"))],
            Passives = [new("Line", "Passive", "High")], Equipment = [new(0, "Set", "Heavy", ["Sturdy"], "Health")] };
        var report = GuideAnalysis.Compare(account, "1", target, catalog);
        Assert.Equal("different", report.Single(r => r.Path == "abilities/front.1/bar").Status);
        Assert.Equal("unknown", report.Single(r => r.Path == "abilities/front.1/progress").Status);
        Assert.Equal("unknown", report.Single(r => r.Path == "abilities/front.1/scripts").Status);
        Assert.Equal("partial", report.Single(r => r.Path == "equipment/0").Status);
        Assert.Equal("advisory", report.Single(r => r.Path == "preferredRace").Status);
    }
}
