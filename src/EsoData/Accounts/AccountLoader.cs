using EsoData.Addons;
using EsoData.Builds;
using EsoData.Catalogs;
using EsoData.Items;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Accounts;

/// <summary>Reads all configured sources and assembles independent mutable account graphs. Never writes game files.</summary>
public static class AccountLoader
{
    public static AccountLoadResult Load(IEnumerable<AccountInput> inputs, GameCatalog? catalog = null)
    {
        var result = new AccountLoadResult();
        var winners = new Dictionary<string, (DateTimeOffset? Scan, DateTimeOffset? File, int Priority)>();
        foreach (var input in inputs)
        {
            Read("IIfA.lua", path => AddInventory(IifaReader.Read(path), 100));
            Read("uespLog.lua", path =>
            {
                var data = UespLogReader.Read(path);
                foreach (var state in data.CharacterStates)
                {
                    var account = Account(state.Character.Server, state.Character.Account, state.Character.Id);
                    var character = Character(account, state.Character);
                    var source = Source(account, data.Source);
                    if (!Choose(account, "character", character.Id, state.ObservedAt, source, 50)) continue;
                    character.Progress = new();
                    character.Build = new();
                    foreach (var priorSource in account.Sources)
                        priorSource.Coverage.RemoveAll(c => c.CharacterId == character.Id && c.Section is "character" or "skills" or "bars" or "championPoints" or "equipment");
                    character.Level = state.Level; character.Class = state.Class; character.Race = state.Race;
                    character.Progress.TotalSkillPoints = state.TotalSkillPoints;
                    character.Progress.UnspentSkillPoints = state.UnspentSkillPoints;
                    character.Progress.TotalChampionPoints = state.ChampionPoints;
                    character.Progress.SkillLines = state.SkillLineRanks?.ToDictionary(x => x.Key, x => x.Value);
                    character.Progress.Research = state.Research;
                    character.RecordedStatistics = state.Statistics;
                    source.Coverage.Add(new("character", character.Id, null, state.ObservedAt, true));
                    var build = character.Build;
                    if (state.Raw.Table("Skills") is LuaTable skillData)
                    {
                        character.Progress.Skills = [];
                        foreach (var row in skillData.Tables())
                        {
                            var skill = row.Value;
                            if (skill.Integer("id") is not > 0) continue;
                            var id = skill.Integer("id")!.Value;
                            var encoded = (int)(skill.Integer("rank") ?? 0);
                            var passive = skill.String("type") == "passive";
                            var morph = passive || encoded < 1 ? 0 : (encoded - 1) / 4;
                            var rank = passive || encoded < 1 ? encoded : (encoded - 1) % 4 + 1;
                            character.Progress.Skills.Add(new(id, skill.String("name"), rank, passive, true, morph,
                                skill.String("type") == "ultimate"));
                            // All entries emitted by this UESP section are purchased, including rank-zero special skills.
                            build.Skills[id] = new() { IsPassive = passive, Rank = passive ? Math.Max(rank, 1) : 1, Morph = morph };
                        }
                        build.Sections |= BuildSections.Skills;
                        source.Coverage.Add(new("skills", character.Id, null, state.ObservedAt, false));
                    }
                    if (state.Attributes is { } attributes)
                    {
                        build.Attributes = new() { Health = attributes.Health, Magicka = attributes.Magicka, Stamina = attributes.Stamina };
                        build.Sections |= BuildSections.Attributes;
                    }
                    if (state.Raw.Table("ActionBar") is LuaTable bars)
                    {
                        build.Bars.Front = ReadBar(bars, 0); build.Bars.Back = ReadBar(bars, 100);
                        build.Sections |= BuildSections.Bars;
                        source.Coverage.Add(new("bars", character.Id, null, state.ObservedAt, true));
                    }
                    if (state.Champion is { } cp)
                    {
                        build.ChampionPoints = cp.Stars.ToDictionary(s => s.SkillId, s => s.Points);
                        build.ChampionSlots = Enumerable.Range(1, 12).Select(i => cp.Slots?.GetValueOrDefault(i) is > 0
                            ? cp.Slots[i] : (long?)null).ToArray();
                        foreach (var d in cp.Disciplines)
                            if (d.SpentPoints.HasValue && d.UnspentPoints.HasValue)
                                character.Progress.ChampionBudgets[d.Name] = d.SpentPoints.Value + d.UnspentPoints.Value;
                        build.Sections |= BuildSections.ChampionPoints;
                        source.Coverage.Add(new("championPoints", character.Id, null, state.ObservedAt, cp.Slots is not null));
                    }
                    if (state.Raw.Table("EquipSlots") is not null)
                    {
                        foreach (var slot in state.Equipment)
                            if (int.TryParse(slot.Key, out var index))
                            {
                                var def = catalog?.Items.GetValueOrDefault(slot.Value.ItemId);
                                build.Equipment[index] = new() { Link = slot.Value.ToString(), ItemId = slot.Value.ItemId,
                                    SetId = def?.SetId, Trait = def?.Trait, Type = def?.WeaponType is > 0 ? def.WeaponType : def?.ArmorType };
                            }
                        build.Sections |= BuildSections.Equipment;
                        source.Coverage.Add(new("equipment", character.Id, null, state.ObservedAt, true));
                    }
                }
                AddInventory(data, 50);
            });
            Read("LibCharacterKnowledge.lua", path =>
            {
                var data = CharacterKnowledgeReader.Read(path);
                foreach (var knowledge in data.Characters)
                {
                    var account = Account(knowledge.Character.Server, knowledge.Character.Account, knowledge.Character.Id);
                    var character = Character(account, knowledge.Character);
                    var source = Source(account, data.Source);
                    source.Diagnostics.AddRange(data.Diagnostics);
                    foreach (var category in knowledge.Categories)
                    {
                        if (!Choose(account, "knowledge/" + category.Key, character.Id, knowledge.ObservedAt, source, 100)) continue;
                        character.Progress.Knowledge[category.Key] = category.Value.Where(e => e.ItemId > 0).GroupBy(e => e.ItemId)
                            .ToDictionary(g => g.Key, g => g.Select(e => e.Known).Distinct().Count() == 1 ? g.First().Known : null);
                        source.Coverage.Add(new("knowledge/" + category.Key, character.Id, null, knowledge.ObservedAt,
                            category.Value.All(e => e.Known.HasValue)));
                    }
                    if (knowledge.Research is not null && Choose(account, "researchKnowledge", character.Id, knowledge.ObservedAt, source, 100))
                    {
                        character.Progress.ResearchKnowledge = knowledge.Research;
                        source.Coverage.Add(new("researchKnowledge", character.Id, null, knowledge.ObservedAt, true));
                    }
                }
            });
            Read("CarosSkillPointSaver.lua", path =>
            {
                var data = CspsReader.Read(path);
                foreach (var profile in data.Profiles)
                {
                    var account = Account(profile.Character.Server, profile.Character.Account, profile.Character.Id);
                    var character = Character(account, profile.Character);
                    var source = Source(account, data.Source);
                    if (!Choose(account, "savedBuild/" + profile.ProfileId, character.Id, profile.SavedAt, source, 100)) continue;
                    character.SavedBuilds.RemoveAll(p => p.Id == profile.ProfileId);
                    character.SavedBuilds.Add(new(profile.ProfileId, profile.Name, profile.SavedAt, BuildCodec.Import(profile.Build)));
                    source.Coverage.Add(new("savedBuild/" + profile.ProfileId, character.Id, null, profile.SavedAt, true));
                }
            });
            Read("LibMultiAccountSets.lua", path =>
            {
                var data = SetCollectionReader.Read(path);
                foreach (var collection in data.Accounts)
                {
                    var account = Account(collection.Server, collection.Account);
                    var source = Source(account, data.Source);
                    if (!Choose(account, "collections", "", collection.ObservedAt, source, 100)) continue;
                    account.SetCollections = new(collection.SetMasks);
                    source.Coverage.Add(new("collections", null, null, collection.ObservedAt, true));
                }
            });

            void Read(string name, Action<string> action)
            {
                var path = Path.Combine(input.SavedVariablesPath, name);
                if (!File.Exists(path)) { result.Diagnostics.Add($"Missing source: {name}"); return; }
                var stamp = File.GetLastWriteTimeUtc(path);
                action(path);
                if (File.GetLastWriteTimeUtc(path) != stamp)
                    throw new IOException($"Source changed during read: {path}. Retry after saving finishes.");
            }
            EsoAccount Account(string world, string name, string? characterId = null)
            {
                var server = NormalizeServer(world);
                // UESP shared-storage records may identify the account by its world-qualified name.
                var at = name.IndexOf('@');
                if (at > 0 && (name.StartsWith("EU Megaserver-PC", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("NA Megaserver-PC", StringComparison.OrdinalIgnoreCase)))
                { if (server.Length == 0) server = name[..2].ToUpperInvariant(); name = name[at..]; }
                if (string.IsNullOrWhiteSpace(server))
                {
                    var matches = result.Accounts.Where(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
                        && (characterId is null || a.Characters.Any(c => c.Id == characterId))).ToArray();
                    server = input.DefaultServer is not null ? NormalizeServer(input.DefaultServer)
                        : matches.Length == 1 ? matches[0].Server : "unknown";
                }
                var existing = result.Accounts.SingleOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
                    && a.Server == server);
                if (existing is not null) return existing;
                var created = new EsoAccount { Name = name, Server = server }; result.Accounts.Add(created); return created;
            }
            void AddInventory(AddonData data, int priority)
            {
                foreach (var c in data.Characters) Character(Account(c.Server, c.Account, c.Id), c);
                foreach (var inventory in data.Inventories)
                {
                    var account = Account(inventory.Server, inventory.Account, inventory.CharacterId);
                    var source = Source(account, data.Source);
                    source.Diagnostics.AddRange(data.Diagnostics.Except(source.Diagnostics));
                    foreach (var group in inventory.Items.GroupBy(i => Location(i, inventory.CharacterId)))
                    {
                        var location = group.Key;
                        var scope = inventory.CharacterId + "/" + location;
                        if (!Choose(account, "inventory", scope, inventory.ObservedAt, source, priority)) continue;
                        var storage = new Storage { Location = location, CharacterId = inventory.CharacterId };
                        var index = 0;
                        foreach (var item in group)
                        {
                            var def = catalog?.Items.GetValueOrDefault(item.Link.ItemId);
                            storage.Items.Add(new() { Reference = scope + "/" + (item.Slot?.ToString() ?? "row" + index++),
                                ItemId = item.Link.ItemId, Link = item.Link.ToString(), Count = item.Count, Name = item.Name ?? def?.Name,
                                Location = location, CharacterId = inventory.CharacterId, Slot = item.Slot, Quality = item.Quality,
                                SetId = def?.SetId, Trait = def?.Trait, ArmorType = def?.ArmorType, WeaponType = def?.WeaponType, EquipType = def?.EquipType });
                        }
                        var locations = inventory.CharacterId is null ? account.SharedStorage :
                            Character(account, new(account.Server, account.Name, inventory.CharacterId)).Storage;
                        locations.RemoveAll(s => s.Location == location); locations.Add(storage);
                        source.Coverage.Add(new("inventory", inventory.CharacterId, location, inventory.ObservedAt, data.Diagnostics.Count == 0));
                    }
                }
            }
        }
        return result;

        bool Choose(EsoAccount account, string section, string scope, DateTimeOffset? scan, AccountSource source, int priority)
        {
            var key = account.Key + "/" + section + "/" + scope;
            if (winners.TryGetValue(key, out var previous))
            {
                // Comparable scan times win; otherwise prefer the provider with the more suitable representation.
                if (scan.HasValue && previous.Scan.HasValue && scan != previous.Scan)
                { if (scan < previous.Scan) return false; }
                else if (priority < previous.Priority || (priority == previous.Priority && source.FileWrittenAt < previous.File)) return false;
            }
            winners[key] = (scan, source.FileWrittenAt, priority); return true;
        }
    }
    public static string NormalizeServer(string world) => world.Trim().ToUpperInvariant() switch
    { "EU MEGASERVER" => "EU", "NA MEGASERVER" => "NA", var other => other };
    private static EsoCharacter Character(EsoAccount account, CharacterReference reference)
    {
        var character = account.Characters.SingleOrDefault(c => c.Id == reference.Id);
        if (character is null) { character = new() { Id = reference.Id }; account.Characters.Add(character); }
        if (!string.IsNullOrWhiteSpace(reference.Name)) character.Name = reference.Name;
        return character;
    }
    private static AccountSource Source(EsoAccount account, SourceInfo info)
    {
        var source = account.Sources.SingleOrDefault(s => s.Path == info.Path && s.Provider == info.Addon);
        if (source is not null) return source;
        source = new() { Provider = info.Addon, Path = info.Path, FileWrittenAt = info.FileWrittenAt, ReadAt = DateTimeOffset.UtcNow };
        account.Sources.Add(source); return source;
    }
    private static long?[] ReadBar(LuaTable bars, int offset) => Enumerable.Range(3, 6).Select(i =>
        (bars[(long)(i + offset)] as LuaTable)?.Integer("id") is long id && id > 0 ? id : (long?)null).ToArray();
    private static string Location(ItemStack item, string? character) => item.BagId switch
    {
        0 => "Equipped", 1 => "Backpack", 2 or 6 => "Bank", 5 => "CraftBag",
        _ => character is not null && item.Location == character ? "Backpack" : item.Location
    };
}
