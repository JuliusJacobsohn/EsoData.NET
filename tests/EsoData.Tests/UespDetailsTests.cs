using EsoData.Addons;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Tests;

public sealed class UespDetailsTests
{
    private static CharacterState Read(string fields) => Assert.Single(UespLogReader.Parse(
        SavedVariables.Parse("uespLogSavedVars={CharName='Example',CharId='123',TimeStamp=200," + fields + "}")).CharacterStates);

    [Fact]
    public void ResearchUsesCountsKeepsLocalizedLabelsAndRetainsTimerObservation()
    {
        var state = Read("""
            Research={Timestamp=100,['Blacksmithing:Trait:Known']=1,['Blacksmithing:Trait:Total']=18,
              ['Blacksmithing:Open']=0,['Blacksmithing:Trait:Épée:Fine']='Charged, [Training] (1/9)',
              ['Blacksmithing:Trait:Épée:Fine:Known']=1,['Blacksmithing:Trait:Épée:Fine:Total']=9,
              ['Blacksmithing:Trait:Épée:Fine:Unknown']='[Training] (8/9)',
              ['Blacksmithing:Trait:Axe']='No traits known',['Blacksmithing:Trait:Axe:Known']=0,
              ['Blacksmithing:Trait:Axe:Total']=9,['Blacksmithing:Trait1']='Training',
              ['Blacksmithing:Item1']='Épée:Fine',['Blacksmithing:Time1']=12.5,
              ['Jewelry:Trait:Known']=18,['Jewelry:Trait:Total']=18}
            """);
        Assert.Equal(100, state.Research!.ObservedAt!.Value.ToUnixTimeSeconds());
        Assert.Equal(200, state.ObservedAt!.Value.ToUnixTimeSeconds());
        var craft = Assert.Single(state.Research.Crafts, x => x.Craft == "Blacksmithing");
        Assert.Equal(1, craft.KnownTraits);
        Assert.Equal(0, craft.OpenSlots);
        var line = Assert.Single(craft.Lines, x => x.Name == "Épée:Fine");
        Assert.Equal(1, line.KnownTraits); // Bracketed researching trait must not count as learned.
        Assert.Contains("[Training]", line.KnownDisplay);
        Assert.Equal(0, Assert.Single(craft.Lines, x => x.Name == "Axe").KnownTraits);
        Assert.Equal(12.5, Assert.Single(craft.Active).RemainingSeconds);
        Assert.Null(Assert.Single(state.Research.Crafts, x => x.Craft == "Jewelry").OpenSlots);
    }

    [Fact]
    public void ChampionRetainsEmptySlotsUnspentPointsAndBothIdKinds()
    {
        var state = Read("""
            ChampionPoints2={['Craft:Points']=50,['Craft:Unspent']=0,['Future:Unspent']=3,
              ['Total:Spent']=50,['Total:Unspent']=3,Slots={[1]=66,[2]=0},
              ['Craft:Example:Star']={skillId=66,id=900123,points=50,slot=1,desc='Description'},
              ['Craft:Passive']={skillId=70,points=10,slot=-1}}
            """);
        var cp = state.Champion!;
        Assert.Equal(2, cp.Disciplines.Count);
        Assert.Equal(0, Assert.Single(cp.Disciplines, x => x.Name == "Craft").UnspentPoints);
        Assert.Null(Assert.Single(cp.Disciplines, x => x.Name == "Future").SpentPoints);
        Assert.Equal(3, cp.UnspentPoints);
        Assert.Equal(0, cp.Slots![2]);
        Assert.False(cp.Slots.ContainsKey(3));
        var star = Assert.Single(cp.Stars, x => x.SkillId == 66);
        Assert.Equal(900123, star.AbilityId);
        Assert.Equal("Craft:Example:Star", star.DisplayName);
        Assert.Equal(1, star.Slot);
        Assert.Equal(-1, Assert.Single(cp.Stars, x => x.SkillId == 70).Slot);
    }

    [Fact]
    public void StatisticsKeepSourceUnitsBarContextsAndUnknownNames()
    {
        var state = Read("""
            ActiveWeaponBar=2,ActiveAbilityBar=1,
            Stats={Health=40000,['Bar1:Health']=39000,['Bar2:Health']=40000,
              ['Computed:WeaponCritDamage']=0.5,['Computed:Bar2:WeaponCritDamage']=0.6,
              FutureStat=42,['Bar3:FutureStat']=0},
            AdvancedStats={['Block Cost']={statId=1,category='Core Abilities',flatValue=656,formatType=1},
              ['Block Mitigation']={statId=7,percentValue=87,formatType=2}},
            Skills={['Craft:Clothing']=50,['Class:Future:Line']=1,['Class:Skill']={id=100,rank=2}}
            """);
        var stats = state.Statistics!;
        Assert.Equal(2, stats.ActiveWeaponBar);
        Assert.Equal(40000, stats.Current!.Values["Health"]);
        Assert.Equal(39000, stats.Bars[1].Values["Health"]);
        Assert.Equal(0, stats.Bars[3].Values["FutureStat"]);
        Assert.Equal(0.5, stats.Current.ComputedValues["WeaponCritDamage"]);
        Assert.Equal(0.6, stats.Bars[2].ComputedValues["WeaponCritDamage"]);
        Assert.Equal(42, stats.Current.Values["FutureStat"]);
        Assert.Null(Assert.Single(stats.Advanced!, x => x.StatId == 1).PercentValue);
        Assert.Equal(87, Assert.Single(stats.Advanced!, x => x.StatId == 7).PercentValue);
        Assert.Equal(50, state.SkillLineRanks!["Craft:Clothing"]);
        Assert.Equal(2, state.SkillLineRanks.Count);
    }

    [Fact]
    public void MissingSectionsStayUnknownRatherThanZero()
    {
        var state = Read("");
        Assert.Null(state.Research);
        Assert.Null(state.Champion);
        Assert.Null(state.Statistics);
        Assert.Null(state.SkillLineRanks);
        Assert.Null(Read("ChampionPoints2={}").Champion!.Slots);
        Assert.Null(Read("AdvancedStats={}").Statistics!.Current);
        Assert.Empty(Read("Research={}").Research!.Crafts);
    }
}
