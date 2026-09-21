using EsoData.Formats;
using EsoData.Items;
using EsoData.Lua;

namespace EsoData.Tests;

public class FormatTests
{
    [Fact]
    public void LuaPreservesIdentifiersKeysEscapesAndChunksWithoutExecutingCode()
    {
        var root = SavedVariables.Parse("""
            -- a comment
            Example = { [1] = 8798292069151066, ["1"] = "\195\169", bare = true,
              text = [=[
            long string]=], array = {"a", "b"}, removed = nil, exponent = 1.5e2 }
            Other = { --[=[ comment ]=]
              ["quote"] = "a\"b\\c", ["hex"] = 0xFF }
            """);
        var table = root.Table("Example")!;
        Assert.Equal(8798292069151066L, table[1]);
        Assert.Equal("é", table["1"]);
        Assert.Equal(true, table.Boolean("bare"));
        Assert.Equal("long string", table.String("text"));
        Assert.Equal(["a", "b"], table.Table("array")!.ArrayValues());
        Assert.Null(table["removed"]);
        Assert.Equal(150, table.Integer("exponent"));
        Assert.Equal("a\"b\\c", root.Table("Other")!.String("quote"));
        Assert.Equal(255, root.Table("Other")!.Integer("hex"));
        Assert.Throws<FormatException>(() => SavedVariables.Parse("x = os.execute('no')"));
        Assert.Throws<FormatException>(() => SavedVariables.ParseTable("{}; print('no')"));
    }

    [Theory]
    [InlineData(0, 23)] [InlineData(10, 155)] [InlineData(100, 164)]
    [InlineData(110, 239)] [InlineData(150, 311)] [InlineData(160, 369)]
    public void CraftedEpicSubtypeUsesGameEncoding(int cp, int subtype)
        => Assert.Equal(subtype, CraftedItem.Subtype(ItemQuality.Epic, cp));

    [Fact]
    public void Level32PurpleLinksKeepEnchantmentsAndDuplicateQueueItems()
    {
        var item = CraftedItem.Create(900001, 32, ItemQuality.Epic, enchantmentItemId: 900002);
        Assert.Equal(32, item.Level);
        Assert.Equal(23, item.Subtype);
        Assert.Equal(900002, item.EnchantmentItemId);
        Assert.True(item.IsCrafted);
        Assert.Equal(32, CraftedItem.NormalizeLevel(33));
        Assert.Throws<ArgumentException>(() => CraftedItem.Create(900001, 33, ItemQuality.Epic));
        var queue = CraftingQueue.Read(CraftingQueue.Write([item, item]));
        Assert.Equal(2, queue.Count);
        Assert.All(queue, x => Assert.Equal(item.ToString(), x.ToString()));
    }

    [Fact]
    public void ItemLinksPreserveFutureFieldsAndLabels()
    {
        var fields = Enumerable.Range(1, 23).Select(x => (long)x).ToArray();
        var link = new ItemLink(fields, 1, "Example");
        Assert.Equal(fields, ItemLink.Parse(link.ToString()).Fields);
        Assert.False(ItemLink.TryParse("|H1:item:1:2|h|h", out _));
    }

    [Fact]
    public void NativeCspsKeepsMorphsPassivesAndFutureSections()
    {
        const string input = "900102:2*900200:1*-*-*-#900100,-,-,-,-,-;-,-,-,-,-,-#0;64;0#-#-*-,-,-,-;-,-,-,-;-,-,-,-#-#qs;opaque#outfit;opaque#1#future";
        var build = CspsBuild.Parse(input);
        Assert.Equal(input, build.ToString());
        Assert.Equal(new ActiveSkill(900102, 2), Assert.Single(build.Skills!.Active));
        Assert.Equal(new PassiveSkill(900200, 1), Assert.Single(build.Skills.Passive));
        Assert.Equal(900100, build.Bars![0][0]!.AbilityId);
        Assert.Equal(64, build.Attributes!.Magicka);
        build.Attributes = new(64, 0, 0);
        Assert.EndsWith("#future", build.ToString());
        Assert.Equal("qs;opaque", build.QuickslotsText);
        Assert.Null(CspsBuild.Parse("-#-#-#-#-#-#-#-#-").Skills);
    }

    [Fact]
    public void NativeCspsWritesFullAllocation()
    {
        var build = new CspsBuild
        {
            Skills = new([new(900102, 2)], [new(900200, 1)], [new(12, 13, 14, 15)]),
            Bars = [[new(900100), new(12, true), null, null, null, null]],
            ChampionPoints = new([new(100, 20)], [[100, null, null, null], [null, null, null, null], [null, null, null, null]]),
            Gear = [new(1, 2, 3, 4, 5), null, null, null, null, null, null, null, null, null, null, null, null, null, new(PoisonItemId: 6, PoisonVariant: 7), null]
        };
        var parsed = CspsBuild.Parse(build.ToString());
        Assert.Equal(1, parsed.Skills!.Passive[0].Rank);
        Assert.True(parsed.Bars![0][1]!.IsCrafted);
        Assert.Equal(20, parsed.ChampionPoints!.Allocations[0].Points);
        Assert.Equal(5, parsed.Gear![0]!.EnchantmentEffectId);
        Assert.Equal(6, parsed.Gear[14]!.PoisonItemId);
    }

    [Fact]
    public void NativeCspsUsesAddonPlaceholdersForAbsentSkillGroups()
    {
        var build = new CspsBuild { Skills = new([new(900102, 2)], []) };
        Assert.StartsWith("900102:2*-*-*-*-#", build.ToString());
    }

    [Fact]
    public void HubUrlHandlesOtherQueryParametersAndExplicitCpPoints()
    {
        var build = new HubBuild
        {
            ClassId = 1, RaceIds = [6], Attributes = new(64, 0, 0),
            FrontBar = [new(900102), new(900300, [1, 2, 3]), null, null, null, null],
            ExtraAbilities = [900200],
            SlottedChampionPoints = [new(100, 25), null, null, null, null, null, null, null, null, null, null, null],
            Gear = [new(0, 3, 4, 5, 6)], Food = [new(7, 8)]
        };
        var parsed = HubBuild.Parse(build.ToUrl() + "&unrelated=yes#fragment");
        Assert.Equal(build.ToString(), parsed.ToString());
        Assert.Equal(1, parsed.ClassId);
        Assert.Equal(25, parsed.SlottedChampionPoints[0]!.Points);
        Assert.Equal(900102, parsed.FrontBar[0]!.AbilityId);
        Assert.Equal(new long[] { 1, 2, 3 }, parsed.FrontBar[1]!.Scripts);
        Assert.Equal(6, parsed.Gear[0].GlyphItemId);
        Assert.Equal(8, parsed.Food[0].VariantId);
    }
}
