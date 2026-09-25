using EsoData.Accounts;
using EsoData.Builds;
using EsoData.Catalogs;
using EsoData.Formats;

namespace EsoData.Tests;

public class AccountBuildTests
{
    private static GameCatalog Catalog() => new()
    {
        Skills = new() { [10] = new(10, "Base", 10, 1, 0, false, IsUltimate: false),
            [11] = new(11, "Morph", 10, 1, 1, false, IsUltimate: false),
            [12] = new(12, "Morph", 10, 4, 1, false, IsUltimate: false),
            [20] = new(20, "Passive", 20, 3, 0, true), [30] = new(30, "Ultimate", 30, 1, 0, false, IsUltimate: true) }
    };
    [Fact]
    public void LoaderDecodesMorphRankAndBarsAndDoesNotTreatProfilesAsApplied()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "uespLog.lua"), """
                uespLogSavedVars={data={CharName="Example",CharId="1",AccountName="@Example",Server="EU",TimeStamp=100,
                SkillPointsTotal=90,SkillPointsUnused=10,Skills={ ["Class:Line"]=50,
                ["Class:Line:Morph"]={id=12,rank=8,type="skill",name="Morph"}},
                ActionBar={[3]={id=12},[8]={id=30},[103]={id=10}}}}
                """);
            var account = Assert.Single(AccountLoader.Load([new(folder)], Catalog()).Accounts);
            var c = Assert.Single(account.Characters);
            var skill = Assert.Single(c.Progress.Skills!);
            Assert.Equal(1, skill.Morph); Assert.Equal(4, skill.Rank);
            Assert.Equal(30, c.Build.Bars.Front[5]); Assert.Equal(10, c.Build.Bars.Back[0]);
            var copy = account.DeepClone(); copy.Characters[0].Build.Skills.Clear();
            Assert.Single(c.Build.Skills);
            Assert.Contains(account.Sources[0].Coverage, x => x.Section == "skills" && !x.Complete);
        }
        finally { Directory.Delete(folder, true); }
    }
    [Fact]
    public void EditIsAtomicResolvesNamesAndExportsRankOneWithBaseBars()
    {
        var original = new BuildPlan();
        var edited = BuildEditor.Apply(original, new() { Skills = new() { ["Morph"] = new() { Morph = 1 } },
            BarSlots = new() { ["front.1"] = "Morph" } }, Catalog());
        Assert.Empty(original.Build.Skills);
        var native = CspsBuild.Parse(BuildCodec.Export(edited.Build, Catalog(), BuildSections.Skills | BuildSections.Bars));
        Assert.Equal(11, Assert.Single(native.Skills!.Active).AbilityId);
        Assert.Equal(10, native.Bars![0][0]!.AbilityId);
        Assert.Empty(native.Skills.Subclasses!);
        Assert.Throws<ArgumentException>(() => BuildEditor.Apply(original, new() {
            Skills = new() { ["Base"] = new() }, BarSlots = new() { ["front.9"] = "Base" } }, Catalog()));
        Assert.Empty(original.Build.Skills);
    }
    [Fact]
    public void FullRespecUsesTotalBudgetAndCpUsesDisciplineBudget()
    {
        var c = new EsoCharacter { Progress = new() { TotalSkillPoints = 20, UnspentSkillPoints = 0,
            TotalChampionPoints = 90, ChampionBudgets = new() { ["Craft"] = 30 } } };
        var build = new CharacterBuild { Sections = BuildSections.Skills | BuildSections.ChampionPoints,
            Skills = new() { [20] = new() { IsPassive = true, Rank = 3 } }, ChampionPoints = new() { [1] = 40 } };
        var catalog = Catalog(); catalog.ChampionStars[1] = new(1, "Test", "Craft", 50, false);
        var report = BuildAnalysis.Validate(c, build, catalog);
        Assert.Equal(17, report.SkillPoints!.Remaining);
        Assert.DoesNotContain(report.Findings, f => f.Code == "skill-budget");
        Assert.Contains(report.Findings, f => f.Code == "cp-discipline-budget");
    }
    [Fact]
    public void ConstraintsCatchEmptyBarsAndMaxedMorphs()
    {
        var c = new EsoCharacter { Progress = new() { Skills = [new(12, "Morph", 4, false, true, 1, false)] } };
        var build = new CharacterBuild { Sections = BuildSections.Skills | BuildSections.Bars,
            Skills = new() { [11] = new() { Morph = 1 } } };
        build.Bars.Front[0] = 11;
        var report = BuildAnalysis.Validate(c, build, Catalog(), new() { RequireFullBars = true, AvoidMaxedMorphs = true });
        Assert.Contains(report.Findings, f => f.Code == "empty-slot");
        Assert.Contains(report.Findings, f => f.Code == "maxed-filler");
    }
    [Fact]
    public void RequirementCycleAndAmbiguousSkillNamesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => BuildEditor.ValidateRequirements([
            new() { Id = "a", DependsOn = ["b"] }, new() { Id = "b", DependsOn = ["a"] }]));
        var catalog = Catalog(); catalog.Skills[90] = new(90, "Base", 90, 1, 0, false);
        Assert.Throws<ArgumentException>(() => BuildEditor.ResolveSkill("Base", catalog));
    }
    [Fact]
    public void CraftingCostsMatchExactQualityAndAccountMaterials()
    {
        var account = new EsoAccount { Characters = [new() { Id = "1" }], SharedStorage = [new() {
            Items = [new() { ItemId = 99, Count = 4 }] }] };
        var catalog = Catalog(); catalog.CraftingRecipes.Add(new(100, 50, 160, 4, 1, 0, null, new Dictionary<long, long> { [99] = 3 }));
        var report = PlanAnalysis.Crafting(account, [new() { ItemId = 100, Quantity = 2 }], "1", catalog);
        Assert.Equal(2, Assert.Single(report.Materials).Short); Assert.Empty(report.Unknown);
        Assert.NotEmpty(PlanAnalysis.Crafting(account, [new() { ItemId = 100, Quality = 5 }], "1", catalog).Unknown);
    }
}
