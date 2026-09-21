using System.Text.RegularExpressions;
using EsoData.Items;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

/// <summary>Reads uespLog character, bank, craft bag and house storage observations.</summary>
public static partial class UespLogReader
{
    public static AddonData Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("uespLog", path));
    public static AddonData Parse(LuaTable document, SourceInfo? source = null)
    {
        var result = new AddonData { Source = source ?? new("uespLog"), Raw = document };
        foreach (var data in ReaderSupport.Descendants(document))
        {
            var characterName = data.String("CharName");
            var isBank = data.Integer("IsBank") == 1;
            var isCraftBag = data.Integer("IsCraftBag") == 1;
            if (characterName is null && !isBank && !isCraftBag) continue;
            var unique = data.String("UniqueAccountName") ?? "";
            var account = data.String("AccountName") ?? unique;
            var server = data.String("Server") ?? data.String("WorldName") ?? "";
            var characterId = characterName is null ? null : ReaderSupport.Text(data["CharId"]);
            var timestamp = ReaderSupport.Time(data.Integer("TimeStamp"));
            if (characterName is not null)
            {
                var character = new CharacterReference(server, account, characterId!, characterName, unique);
                result.Characters.Add(character);
                var skills = new List<SkillAllocation>();
                foreach (var skill in data.Table("Skills")?.Tables() ?? [])
                    if (skill.Value.Integer("id") is long id)
                        skills.Add(new(id, ReaderSupport.Int(skill.Value, "rank") ?? 0,
                            skill.Value.String("type") == "passive", skill.Value.String("name")));
                var cp = new List<ChampionAllocation>();
                foreach (var star in data.Table("ChampionPoints2")?.Tables() ?? [])
                    if (star.Value.Integer("skillId") is long id && ReaderSupport.Int(star.Value, "points") is int points)
                        cp.Add(new(id, points));
                var equipment = new Dictionary<string, ItemLink>();
                foreach (var slot in data.Table("EquipSlots")?.Tables() ?? [])
                    if (ItemLink.TryParse(slot.Value.String("link") ?? "", out var link)) equipment[slot.Key.Value] = link!;
                result.CharacterStates.Add(new()
                {
                    Character = character, ObservedAt = timestamp, Raw = data,
                    ApiVersion = data.Integer("APIVersion"), Level = ReaderSupport.Int(data, "Level"),
                    Class = data.String("Class"), Race = data.String("Race"),
                    UnspentSkillPoints = ReaderSupport.Int(data, "SkillPointsUnused"), TotalSkillPoints = ReaderSupport.Int(data, "SkillPointsTotal"),
                    ChampionPoints = ReaderSupport.Int(data, "ChampionPointsEarned"),
                    Attributes = data.Integer("AttributesHealth") is not null ? new(ReaderSupport.Int(data, "AttributesHealth") ?? 0,
                        ReaderSupport.Int(data, "AttributesMagicka") ?? 0, ReaderSupport.Int(data, "AttributesStamina") ?? 0) : null,
                    Skills = skills, ChampionAllocations = cp, Equipment = equipment,
                    Champion = UespCharacterDetails.Champion(data.Table("ChampionPoints2")),
                    Research = UespCharacterDetails.Research(data.Table("Research")),
                    Statistics = UespCharacterDetails.Statistics(data),
                    SkillLineRanks = UespCharacterDetails.SkillLines(data.Table("Skills"))
                });
            }
            if (data.Table("Inventory") is LuaTable inventory)
                result.Inventories.Add(new(server, account, characterId, timestamp,
                    ReadItems(inventory, isBank ? "Bank" : isCraftBag ? "CraftBag" : "Backpack", result.Diagnostics), unique));
            if (data.Table("HouseStorage") is LuaTable storage)
                foreach (var bag in storage.Tables().Where(x => x.Key.IsNumeric))
                    result.Inventories.Add(new(server, account, null, ReaderSupport.Time(storage.Integer("TimeStamp")),
                        ReadItems(bag.Value, "HouseStorage:" + bag.Key.Value, result.Diagnostics), unique));
        }
        if (result.CharacterStates.Count == 0 && result.Inventories.Count == 0)
            result.Diagnostics.Add("No character or account inventory observations found; enable uespLog character data saving in-game.");
        return result;
    }
    private static List<ItemStack> ReadItems(LuaTable inventory, string location, List<string> diagnostics)
    {
        var items = new List<ItemStack>();
        foreach (var row in inventory.Where(x => x.Key.IsNumeric))
        {
            if (row.Value is not string text) continue;
            var match = InventoryRow().Match(text);
            var link = ItemLink.FindAll(text).FirstOrDefault();
            if (!match.Success || link is null) { diagnostics.Add($"Unreadable inventory row at {location}/{row.Key.Value}."); continue; }
            // UESP compacts nonempty bag slots into an array; its row number is not a physical bag slot.
            items.Add(new(link, long.Parse(match.Groups[1].Value), location, Name: link.Label, SourceIndex: long.Parse(row.Key.Value)));
        }
        return items;
    }
    [GeneratedRegex(@"^\s*(\d+)\s+\|H")]
    private static partial Regex InventoryRow();
}
