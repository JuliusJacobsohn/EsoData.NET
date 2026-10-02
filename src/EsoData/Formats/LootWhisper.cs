using EsoData.Addons;
using EsoData.Items;

namespace EsoData.Formats;

/// <summary>A copyable draft. Creating it does not send a message or establish trade eligibility.</summary>
public sealed record LootWhisperDraft(string Recipient, string ItemLink, string Message, string Command);

public static class LootWhisper
{
    /// <summary>Builds an ESO whisper draft using the observed item's complete link payload.</summary>
    public static LootWhisperDraft? Create(LootEvent loot, string? itemName = null)
    {
        var recipient = loot.RecipientAccount;
        if (!recipient.StartsWith('@') || recipient.Length < 2
            || recipient.Any(c => char.IsWhiteSpace(c) || c is ',' or '|')
            || !ItemLink.TryParse(loot.Link, out var item)) return null;

        // Labels are display text only. Never reconstruct the item from its ID or catalog defaults.
        var label = !string.IsNullOrWhiteSpace(item!.Label) ? item.Label
            : !string.IsNullOrWhiteSpace(itemName) ? itemName : $"Item {item.ItemId}";
        label = label.Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ');
        var link = new ItemLink(item.Fields, item.LinkStyle, label).ToString();
        var message = $"Hi! Could I have {link} if you don't need it? Thanks!";
        return new(recipient, link, message, $"/w {recipient}, {message}");
    }
}
