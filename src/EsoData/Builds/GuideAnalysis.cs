using EsoData.Accounts;
using EsoData.Catalogs;

namespace EsoData.Builds;

/// <summary>Compare a declarative guide target without inventing IDs, ranks or allocations absent from the guide.</summary>
public static class GuideAnalysis
{
    public static IReadOnlyList<GuideFinding> Compare(EsoAccount account, string characterId, GuideSetup target, GameCatalog catalog)
    {
        GuideSetupValidation.Validate(target);
        var c = account.Character(characterId);
        var rows = new List<GuideFinding>();
        bool Same(string? a, string? b) => a is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        if (target.Class is not null) rows.Add(new("class", target.Class, c.Class is null ? "unknown" : Same(c.Class, target.Class) ? "satisfied" : "different", c.Class));
        if (target.PureClass) rows.Add(new("pureClass", "Native class skill lines", "unknown", Detail: "Active subclass coverage is unavailable."));
        if (target.PreferredRace is not null) rows.Add(new("preferredRace", target.PreferredRace,
            c.Race is null ? "unknown" : Same(c.Race, target.PreferredRace) ? "satisfied" : "advisory", c.Race, "Preference, not a mandatory race change."));
        foreach (var mastery in target.Masteries) rows.Add(new("mastery", mastery, "unknown", Detail: "Mastery allocation is not observed by this account model."));
        foreach (var a in target.Abilities)
        {
            var found = c.Progress.Skills?.Where(s => Same(s.Name, a.Name)).ToArray();
            var progress = found?.OrderByDescending(s => s.Rank).FirstOrDefault();
            rows.Add(new($"abilities/{a.Slot}/progress", a.Name + (a.TargetRank is int rank ? $" rank {rank}" : ""),
                progress is null ? "unknown" : a.TargetRank.HasValue && progress.Rank < a.TargetRank ? "unmet" : "satisfied",
                progress is null ? null : $"rank {progress.Rank}", progress is null ? "This exact ability/morph is not observed; absence does not establish its unlock state." : null));
            var parts = a.Slot.Split('.'); var index = int.Parse(parts[1]) - 1;
            var id = (parts[0] == "front" ? c.Build.Bars.Front : c.Build.Bars.Back).ElementAtOrDefault(index);
            var name = id.HasValue ? catalog.Skills.GetValueOrDefault(id.Value)?.Name
                ?? c.Progress.Skills?.FirstOrDefault(s => s.AbilityId == id)?.Name : null;
            rows.Add(new($"abilities/{a.Slot}/bar", a.Name, !c.Build.Sections.HasFlag(BuildSections.Bars) ? "unknown"
                : id is null ? "different" : name is null ? "unknown" : Same(name, a.Name) ? "satisfied" : "different", name));
            if (a.Scripts is not null) rows.Add(new($"abilities/{a.Slot}/scripts", $"{a.Scripts.Focus} / {a.Scripts.Signature} / {a.Scripts.Affix}",
                "unknown", Detail: "Observed scribing configuration and script-name mappings are required to verify this recipe."));
        }
        foreach (var p in target.Passives)
        {
            // Some guide entries apply to a different bar variant and are retained without making them mandatory.
            if (p.Condition is not null) { rows.Add(new($"passives/{p.SkillLine}/{p.Name}", p.Name, "conditional", Detail: p.Condition)); continue; }
            var found = c.Progress.Skills?.Where(s => s.IsPassive && Same(s.Name, p.Name)).OrderByDescending(s => s.Rank).FirstOrDefault();
            var amount = p.Excluded ? 0 : p.Rank;
            var allocation = c.Build.Skills.Where(s => Same(catalog.Skills.GetValueOrDefault(s.Key)?.Name, p.Name)).Select(s => s.Value.Rank).DefaultIfEmpty(0).Max();
            var status = p.Excluded ? allocation > 0 ? "different" : "unknown"
                : found is null ? "unknown" : amount.HasValue ? found.Rank >= amount ? "satisfied" : "unmet" : "unknown";
            rows.Add(new($"passives/{p.SkillLine}/{p.Name}", p.Excluded ? "Do not purchase" : p.Name + (amount is int r ? $" rank {r}" : " maximum rank"),
                status, found is null ? null : $"observed rank {found.Rank}; allocated {allocation}",
                !p.Excluded && amount is null ? "Guide requests maximum rank; numeric maximum is unspecified." : null));
        }
        foreach (var item in target.Equipment)
        {
            var sets = catalog.Sets.Values.Where(s => s.Names.Values.Any(n => Same(n, item.Set))).ToArray();
            var actual = c.Build.Equipment.GetValueOrDefault(item.Slot);
            var actualName = actual?.SetId is long setId ? catalog.Sets.GetValueOrDefault(setId)?.Names.GetValueOrDefault("en") : null;
            var owned = sets.Length == 1 ? account.Inventory.Where(i => i.SetId == sets[0].Id).Sum(i => i.Count) : (long?)null;
            var status = !c.Build.Sections.HasFlag(BuildSections.Equipment) || sets.Length != 1 ? "unknown"
                : actual is null || actual.SetId != sets[0].Id ? "different" : "partial";
            rows.Add(new($"equipment/{item.Slot}", $"{item.Set}; {item.Type}; {string.Join(" or ", item.Traits)}; {item.Enchantment}", status,
                actualName, $"Account-wide pieces of this set: {owned?.ToString() ?? "unknown"}. Matching the set alone does not verify slot, trait, enchantment or quality."));
        }
        foreach (var cp in target.Champion)
        {
            if (cp.Condition is not null) { rows.Add(new($"champion/{cp.Discipline}/{cp.Name}", cp.Name, "conditional", Detail: cp.Condition)); continue; }
            var definitions = catalog.ChampionStars.Values.Where(s => Same(s.Name, cp.Name)).ToArray();
            var definition = definitions.Length == 1 ? definitions[0] : null;
            var slotted = definition is not null && c.Build.ChampionSlots.Contains(definition.Id);
            rows.Add(new($"champion/{cp.Discipline}/{cp.Name}", cp.Name, definition is null || !c.Build.Sections.HasFlag(BuildSections.ChampionPoints) ? "unknown"
                : !slotted ? "different" : cp.Points.HasValue && c.Build.ChampionPoints.GetValueOrDefault(definition.Id) < cp.Points ? "unmet" : "satisfied",
                definition is null ? null : $"slotted={slotted}; points={c.Build.ChampionPoints.GetValueOrDefault(definition.Id)}",
                cp.Points is null ? "Guide specifies a slotted star, not an exact point allocation." : null));
        }
        if (target.Attributes is not null) rows.Add(new("attributes", AccountJson.Write(target.Attributes),
            !c.Build.Sections.HasFlag(BuildSections.Attributes) ? "unknown" : AccountJson.Write(c.Build.Attributes) == AccountJson.Write(target.Attributes) ? "satisfied" : "different",
            AccountJson.Write(c.Build.Attributes), target.AttributeCondition));
        if (target.Mundus is not null) rows.Add(new("mundus", target.Mundus, "unknown", Detail: "Named Mundus mapping is required."));
        if (target.Food is not null) rows.Add(new("food", target.Food, "unknown", Detail: "Active food is not observed; inventory ownership alone does not establish consumption."));
        foreach (var potion in target.Potions) rows.Add(new("potion", potion.Name, "unknown", Detail: string.Join(", ", potion.Effects)));
        return rows;
    }
}
