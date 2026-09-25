using EsoData.Accounts;
using EsoData.Catalogs;

namespace EsoData.Builds;

/// <summary>One atomic typed patch. Omitted properties are untouched; null dictionary values remove entries.</summary>
public sealed class BuildPatch
{
    public Dictionary<string, SkillPurchase?>? Skills { get; set; }
    public Dictionary<string, string?>? BarSlots { get; set; }
    public Dictionary<long, int?>? ChampionPoints { get; set; }
    public Dictionary<int, long?>? ChampionSlots { get; set; }
    public Dictionary<int, EquipmentChoice?>? Equipment { get; set; }
    public BuildAttributes? Attributes { get; set; }
    public BuildConstraints? Constraints { get; set; }
    public List<BuildRequirement>? Requirements { get; set; }
    public List<GuideReference>? Guides { get; set; }
    public List<CraftingOrder>? Crafting { get; set; }
    public BuildSections? Sections { get; set; }
}
public static class BuildEditor
{
    public static long ResolveSkill(string selector, GameCatalog catalog)
    {
        if (long.TryParse(selector, out var id) && id != 0) return id;
        var matches = catalog.Skills.Values.Where(s => string.Equals(s.Name, selector, StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => (s.BaseAbilityId ?? s.Id, s.Morph)).ToArray();
        if (matches.Length != 1) throw new ArgumentException($"Skill '{selector}': {matches.Length} exact families matched; resolve a numeric ID first.");
        return matches[0].OrderBy(s => s.Rank ?? int.MaxValue).First().Id;
    }
    public static BuildPlan Apply(BuildPlan original, BuildPatch patch, GameCatalog catalog)
    {
        var plan = AccountJson.Clone(original); var build = plan.Build;
        if (patch.Skills is not null)
        {
            foreach (var (selector, purchase) in patch.Skills)
            {
                var id = ResolveSkill(selector, catalog);
                foreach (var old in build.Skills.Keys.Where(k => BuildAnalysis.Family(k, catalog) == BuildAnalysis.Family(id, catalog)).ToArray()) build.Skills.Remove(old);
                if (purchase is not null)
                {
                    if (purchase.Rank < 1 || purchase.Morph is < 0 or > 2) throw new ArgumentException("Invalid rank or morph.");
                    build.Skills[id] = AccountJson.Clone(purchase);
                }
            }
            build.Sections |= BuildSections.Skills;
        }
        foreach (var (slot, selector) in patch.BarSlots ?? [])
        {
            var parts = slot.Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var index) || index is < 1 or > 6) throw new ArgumentException("Bar slots use front.1 through front.6 or back.1 through back.6.");
            var bar = parts[0] switch { "front" => build.Bars.Front, "back" => build.Bars.Back, _ => throw new ArgumentException("Unknown bar.") };
            bar[index - 1] = selector is null ? null : ResolveSkill(selector, catalog);
            build.Sections |= BuildSections.Bars;
        }
        foreach (var (id, points) in patch.ChampionPoints ?? [])
        {
            if (id <= 0 || points < 0) throw new ArgumentException("Invalid champion allocation.");
            if (points is null) build.ChampionPoints.Remove(id); else build.ChampionPoints[id] = points.Value;
            build.Sections |= BuildSections.ChampionPoints;
        }
        foreach (var (slot, id) in patch.ChampionSlots ?? [])
        {
            if (slot is < 1 or > 12 || id <= 0) throw new ArgumentException("Champion slots use positions 1..12 and positive IDs or null.");
            build.ChampionSlots[slot - 1] = id; build.Sections |= BuildSections.ChampionPoints;
        }
        foreach (var (slot, choice) in patch.Equipment ?? [])
        { if (choice is null) build.Equipment.Remove(slot); else build.Equipment[slot] = AccountJson.Clone(choice); build.Sections |= BuildSections.Equipment; }
        if (patch.Attributes is not null) { build.Attributes = AccountJson.Clone(patch.Attributes); build.Sections |= BuildSections.Attributes; }
        if (patch.Constraints is not null) plan.Constraints = AccountJson.Clone(patch.Constraints);
        if (patch.Requirements is not null) plan.Requirements = AccountJson.Clone(patch.Requirements);
        if (patch.Guides is not null) plan.Guides = AccountJson.Clone(patch.Guides);
        if (patch.Crafting is not null) plan.Crafting = AccountJson.Clone(patch.Crafting);
        if (patch.Sections.HasValue) build.Sections = patch.Sections.Value;
        ValidateRequirements(plan.Requirements);
        return plan;
    }
    public static void ValidateRequirements(IReadOnlyList<BuildRequirement> requirements)
    {
        if (requirements.Any(r => string.IsNullOrWhiteSpace(r.Id)) || requirements.Select(r => r.Id).Distinct().Count() != requirements.Count)
            throw new ArgumentException("Requirement IDs must be nonempty and unique.");
        var byId = requirements.ToDictionary(r => r.Id);
        var visited = new HashSet<string>(); var active = new HashSet<string>();
        void Visit(string id)
        {
            if (!byId.TryGetValue(id, out var r)) throw new ArgumentException($"Missing dependency {id}.");
            if (active.Contains(id)) throw new ArgumentException("Requirement dependencies contain a cycle.");
            if (!visited.Add(id)) return;
            active.Add(id); foreach (var dep in r.DependsOn) Visit(dep); active.Remove(id);
        }
        foreach (var r in requirements) Visit(r.Id);
    }
}
