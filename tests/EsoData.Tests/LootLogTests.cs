using EsoData.Accounts;
using EsoData.Addons;
using EsoData.Lua;

namespace EsoData.Tests;

public class LootLogTests
{
    [Fact]
    public void ReadsPackedLegacyAndNonItemEventsWithoutTreatingPrimeFlagsAsBits()
    {
        var data = LootLogReader.Parse(SavedVariables.Parse("""
            LootLogHistory={EU={[100]={
              "360001;|H0:item:123:362:50:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0|h|h;1;@Friend;Friend;15",
              {360002,"|H0:collectible:1284|h|h",1,"@Self","Self",6},
              "360003;9876;2;@Self;Self;2", "bad entry"
            }}}
            """));
        Assert.Equal(3, data.Events.Count);
        var gear = data.Events.Single(e => e.ItemId == 123);
        Assert.Equal("@Friend", gear.RecipientAccount);
        Assert.True(gear.SetItem); Assert.True(gear.Notable); Assert.False(gear.Personal);
        Assert.Null(data.Events[0].ItemId);
        Assert.True(data.Events[1].Personal);
        Assert.Single(data.Diagnostics);
        var clone = AccountJson.Clone(new EsoAccount { LootHistory = data });
        Assert.Equal(data.Events, clone.LootHistory!.Events);
        Assert.Empty(clone.Inventory);
    }
}
