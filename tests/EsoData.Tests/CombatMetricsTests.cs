using EsoData.Accounts;
using EsoData.Addons;
using EsoData.Lua;

namespace EsoData.Tests;

public class CombatMetricsTests
{
    [Fact]
    public void ReadsPlainSummaryFromEncodedAndLegacyFightsWithoutExecutingOrDecodingPayloads()
    {
        var report = CombatMetricsReader.Parse(SavedVariables.Parse("""
            CombatMetricsFightDataSV={version=999,
              [1]={charData={name='Example^Mx'},fightlabel='Trial dummy',zone='House',date=100,time='12:00',
                dpstime=12.5,hpstime=8,calculated={DPSOut=90001.5,HPSOut=0},encodedStrings={'opaque'},log=true},
              [2]={char='Other',date=200,calculated={DPSIn=123}},[3]='malformed'}
            """));
        Assert.Equal(999, report.FormatVersion);
        var fight = report.Fights[0];
        Assert.Equal("Example", fight.CharacterName);
        Assert.Equal(90001.5, fight.DamagePerSecond);
        Assert.Equal(0, fight.HealingPerSecond);
        Assert.Null(fight.IncomingDamagePerSecond);
        Assert.Equal(12.5, fight.DamageDurationSeconds);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(100), fight.StartedAt);
        Assert.True(fight.HasEncodedDetails); Assert.True(fight.HasCombatLog);
        Assert.False(report.Fights[1].HasEncodedDetails);
        Assert.Single(report.Diagnostics);
        Assert.Equal(fight, AccountJson.Clone(report).Fights[0]);
    }

    [Fact]
    public void EmptySavedHistoryIsDistinctFromMissingRoot()
    {
        Assert.Empty(CombatMetricsReader.Parse(SavedVariables.Parse("CombatMetricsFightDataSV={version=22}")).Fights);
        Assert.Throws<FormatException>(() => CombatMetricsReader.Parse(SavedVariables.Parse("Settings={}")));
    }
}
