using EsoData.Addons;
using EsoData.Items;

namespace EsoData.Formats;

/// <summary>A copyable draft. Creating it does not send a message or establish trade eligibility.</summary>
public sealed record LootWhisperDraft(string Recipient, string ItemLink, string Message, string Command);
public sealed record LootWhisperMessage(string Recipient, IReadOnlyList<string> ItemLinks, string Message, string Command);

public static class LootWhisper
{
    /// <summary>Builds an ESO whisper draft using the observed item's complete link payload.</summary>
    public static LootWhisperDraft? Create(LootEvent loot, string? itemName = null)
    {
        var recipient = Recipient(loot);
        if (recipient is null || !ItemLink.TryParse(loot.Link, out var item)) return null;

        // Labels are display text only. Never reconstruct the item from its ID or catalog defaults.
        var label = !string.IsNullOrWhiteSpace(item!.Label) ? item.Label
            : !string.IsNullOrWhiteSpace(itemName) ? itemName : $"Item {item.ItemId}";
        label = label.Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ');
        var link = new ItemLink(item.Fields, item.LinkStyle, label).ToString();
        var message = $"Hi! Could I have {link} if you don't need it? Thanks!";
        return new(recipient, link, message, Prefix(recipient) + message);
    }

    /// <summary>Combines one player's drops into pasteable messages without splitting any item link.</summary>
    public static IReadOnlyList<LootWhisperMessage> CreateMany(IEnumerable<LootEvent> drops,
        Func<LootEvent, string?>? itemName = null, int maximumCommandLength = 350)
    {
        var entries = drops.OrderByDescending(e => e.ReceivedAt).ToArray();
        if (entries.Select(e => (e.Server.ToUpperInvariant(), e.RecipientAccount.ToUpperInvariant())).Distinct().Count() > 1)
            throw new ArgumentException("Group drops by player and server before creating whispers.", nameof(drops));
        var recipient = entries.Select(Recipient).FirstOrDefault(r => r is not null);
        if (recipient is null) return [];
        var result = new List<LootWhisperMessage>();
        var links = new List<string>();
        foreach (var entry in entries)
        {
            var draft = Create(entry, itemName?.Invoke(entry));
            if (draft is null) continue;
            var next = Message(links.Append(draft.ItemLink));
            if ((Prefix(recipient) + next).Length > maximumCommandLength)
            {
                if (links.Count > 0) { Add(); links.Clear(); }
                if ((Prefix(recipient) + Message([draft.ItemLink])).Length > maximumCommandLength)
                    throw new ArgumentException("An individual item link cannot fit the requested command length.", nameof(maximumCommandLength));
            }
            links.Add(draft.ItemLink);
        }
        if (links.Count > 0) Add();
        return result;

        void Add()
        {
            var message = Message(links);
            result.Add(new(recipient, links.ToArray(), message, Prefix(recipient) + message));
        }
    }

    private static string Message(IEnumerable<string> links) =>
        $"Hi! Could I have {string.Join(' ', links)} for my collection if you don't need them? Thanks!";

    private static string? Recipient(LootEvent loot)
    {
        var account = loot.RecipientAccount;
        if (account.StartsWith('@') && account.Length > 1
            && !account.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is ',' or '|')) return account;
        var character = loot.RecipientCharacter.Trim();
        if (character.Length > 0 && !character.StartsWith('@') && character != "nil"
            && !character.Any(c => char.IsControl(c) || c is ',' or '|')) return character;
        return null;
    }

    // ESO's documented recipient separators differ for character names and account IDs.
    private static string Prefix(string recipient) => recipient.StartsWith('@') ? $"/w {recipient} " : $"/w {recipient}, ";
}
