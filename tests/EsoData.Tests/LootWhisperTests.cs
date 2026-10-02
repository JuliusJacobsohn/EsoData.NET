using EsoData.Addons;
using EsoData.Formats;
using EsoData.Items;

namespace EsoData.Tests;

public class LootWhisperTests
{
    [Theory]
    [InlineData(362)]
    [InlineData(366)]
    public void DraftPreservesExactVariantAndEnchantmentsAndUsesWhisperDelimiter(int subtype)
    {
        var fields = new long[] { 123, subtype, 50, 456, 42, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 1, 0, 1, 0, 0, 99 };
        var observed = new ItemLink(fields, 0);
        var draft = LootWhisper.Create(Event(observed.ToString()), "Example Ice Staff")!;
        var linked = ItemLink.Parse(draft.ItemLink);
        Assert.Equal(fields, linked.Fields);
        Assert.Equal(0, linked.LinkStyle);
        Assert.Equal("Example Ice Staff", linked.Label);
        Assert.StartsWith("/w @Friend, ", draft.Command);
        Assert.Contains(draft.ItemLink, draft.Message);
        Assert.Equal($"/w @Friend, {draft.Message}", draft.Command);
    }

    [Fact]
    public void MissingNameUsesHonestFallbackAndExistingLabelsWin()
    {
        var link = new ItemLink(new long[21].Select((n, i) => i == 0 ? 123L : n));
        Assert.Equal("Item 123", ItemLink.Parse(LootWhisper.Create(Event(link.ToString()))!.ItemLink).Label);
        var named = new ItemLink(link.Fields, 1, "Observed name");
        Assert.Equal(named.ToString(), LootWhisper.Create(Event(named.ToString()), "Catalog name")!.ItemLink);
    }

    [Theory]
    [InlineData("9876", "@Friend")]
    [InlineData("|H0:collectible:1284|h|h", "@Friend")]
    [InlineData("bad link", "@Friend")]
    public void NonItemEventsDoNotInventWhisperableItems(string link, string recipient) =>
        Assert.Null(LootWhisper.Create(Event(link) with { RecipientAccount = recipient }));

    private static LootEvent Event(string link) => new("EU", DateTimeOffset.UnixEpoch, link, 1,
        "@Friend", "Friend", 15, 123, false, true, true);

    [Theory]
    [InlineData("")]
    [InlineData("nil")]
    [InlineData("@Friend,Other")]
    [InlineData("@Friend\nOther")]
    public void UnusableRecipientsHaveNoDraft(string recipient)
    {
        var link = new ItemLink(new long[21].Select((n, i) => i == 0 ? 123L : n)).ToString();
        Assert.Null(LootWhisper.Create(Event(link) with { RecipientAccount = recipient }));
    }
}
