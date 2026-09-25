using EsoData.Accounts;
using EsoData.Catalogs;
using EsoData.Items;

namespace EsoData.Builds;

public sealed record RequirementProgress(string Id, string Description, string Status, bool Optional, int Priority,
    IReadOnlyList<string> BlockedBy);
public sealed record EquipmentMatch(int Slot, string Status, IReadOnlyList<OwnedItem> Matches);
public sealed record MaterialShortage(long ItemId, long Needed, long Owned, long Short);
public sealed record CraftingReport(IReadOnlyList<MaterialShortage> Materials, IReadOnlyList<string> Unknown,
    IReadOnlyList<string> Unmet);
public sealed record BuildDifference(string Path, object? Current, object? Desired);

public static class PlanAnalysis
{
    public static IReadOnlyList<RequirementProgress> Requirements(EsoAccount account, BuildPlan plan, GameCatalog catalog)
    {
        BuildEditor.ValidateRequirements(plan.Requirements);
        var character = account.Character(plan.CharacterId);
        string Status(bool? value) => value switch { true => "satisfied", false => "unmet", _ => "unknown" };
        var states = plan.Requirements.ToDictionary(r => r.Id, r => Status(r.Kind switch
        {
            RequirementKind.Manual => r.Completed,
            RequirementKind.Skill => character.Progress.Skills is null ? null : character.Progress.Skills.Any(s =>
                BuildAnalysis.Family(s.AbilityId, catalog) == BuildAnalysis.Family(r.GameId ?? 0, catalog) && s.Rank >= r.Amount) ? true : null,
            RequirementKind.Allocation => character.Build.Sections.HasFlag(BuildSections.Skills)
                ? character.Build.Skills.Any(s => BuildAnalysis.Family(s.Key, catalog) == BuildAnalysis.Family(r.GameId ?? 0, catalog) && s.Value.Rank >= r.Amount) : null,
            RequirementKind.SkillLine => character.Progress.SkillLines?.TryGetValue(r.Category ?? "", out var rank) == true ? rank >= r.Amount : null,
            RequirementKind.Knowledge => character.Progress.Knowledge.TryGetValue(r.Category ?? "", out var knowledge)
                ? knowledge.GetValueOrDefault(r.GameId ?? 0) : null,
            RequirementKind.Item => account.Inventory.Where(i => i.ItemId == r.GameId).Sum(i => i.Count) >= r.Amount ? true : null,
            _ => null
        }));
        return plan.Requirements.OrderBy(r => r.Priority).Select(r => new RequirementProgress(r.Id, r.Description,
            states[r.Id], r.Optional, r.Priority, r.DependsOn.Where(d => states[d] != "satisfied").ToArray())).ToArray();
    }

    public static IReadOnlyList<EquipmentMatch> Equipment(EsoAccount account, CharacterBuild target)
    {
        return target.Equipment.Select(pair =>
        {
            var choice = pair.Value;
            bool Match(OwnedItem i) => (choice.ItemId is null || i.ItemId == choice.ItemId)
                && (choice.SetId is null || i.SetId == choice.SetId) && (choice.Trait is null || i.Trait == choice.Trait)
                && (choice.Quality is null || i.Quality == choice.Quality)
                && (choice.Type is null || i.ArmorType == choice.Type || i.WeaponType == choice.Type)
                && (choice.Link is null || ItemLink.Parse(i.Link).WithoutLabel() == ItemLink.Parse(choice.Link).WithoutLabel())
                && (choice.OwnedReference is null || i.Reference == choice.OwnedReference);
            var matches = account.Inventory.Where(Match).ToArray();
            // Ownership does not establish binding/transfer or exact enchantment-effect compatibility.
            return new EquipmentMatch(pair.Key, matches.Length > 0 ? "owned-candidates" : "not-observed", matches);
        }).ToArray();
    }

    public static CraftingReport Crafting(EsoAccount account, IReadOnlyList<CraftingOrder> orders,
        string crafterId, GameCatalog catalog)
    {
        var crafter = account.Character(crafterId);
        var required = new Dictionary<long, long>(); var unknown = new List<string>(); var unmet = new List<string>();
        foreach (var o in orders)
        {
            _ = BuildCodec.Crafting([o]); // Validate level, quality, glyph and quantity before costing.
            var matches = catalog.CraftingRecipes.Where(r => r.ItemId == o.ItemId && r.Level == o.Level && r.ChampionPoints == o.ChampionPoints
                && r.Quality == o.Quality && r.StyleId == o.StyleId && r.EnchantmentItemId == o.EnchantmentItemId
                && r.EnchantmentQuality == o.EnchantmentQuality && (r.CrafterId is null || r.CrafterId == crafter.Id)).ToArray();
            if (matches.Length != 1) { unknown.Add($"Exact material/knowledge recipe for item {o.ItemId} at requested variant is missing or ambiguous."); continue; }
            var recipe = matches[0];
            foreach (var material in recipe.Materials)
                required[material.Key] = checked(required.GetValueOrDefault(material.Key) + material.Value * o.Quantity);
            if (recipe.KnowledgeCategory is not null && recipe.KnowledgeItemId is long knownId)
            {
                var known = crafter.Progress.Knowledge.GetValueOrDefault(recipe.KnowledgeCategory)?.GetValueOrDefault(knownId);
                if (known == false) unmet.Add($"Crafter has not learned {recipe.KnowledgeCategory}/{knownId}.");
                else if (known is null) unknown.Add($"Crafter knowledge unobserved for {recipe.KnowledgeCategory}/{knownId}.");
            }
        }
        var materials = required.Select(m => { var owned = account.Inventory.Where(i => i.ItemId == m.Key).Sum(i => i.Count);
            return new MaterialShortage(m.Key, m.Value, owned, Math.Max(0, m.Value - owned)); }).ToArray();
        return new(materials, unknown, unmet);
    }

    public static IReadOnlyList<BuildDifference> Compare(CharacterBuild current, CharacterBuild desired,
        BuildSections sections, GameCatalog catalog)
    {
        var differences = new List<BuildDifference>();
        void Add(string path, object? a, object? b) { if (AccountJson.Write(a) != AccountJson.Write(b)) differences.Add(new(path, a, b)); }
        foreach (var section in new[] { BuildSections.Skills, BuildSections.Bars, BuildSections.Attributes, BuildSections.ChampionPoints, BuildSections.Equipment, BuildSections.Mundus })
        {
            if (!sections.HasFlag(section)) continue;
            if (!current.Sections.HasFlag(section)) { differences.Add(new(section.ToString(), "unobserved", "requested")); continue; }
            switch (section)
            {
                case BuildSections.Skills:
                    Dictionary<long, SkillPurchase> Map(CharacterBuild b) => b.Skills.GroupBy(s => BuildAnalysis.Family(s.Key, catalog)).ToDictionary(g => g.Key, g => g.First().Value);
                    var a = Map(current); var b = Map(desired);
                    foreach (var id in a.Keys.Union(b.Keys)) Add($"skills/{id}", a.GetValueOrDefault(id), b.GetValueOrDefault(id));
                    break;
                case BuildSections.Bars:
                    foreach (var (name, oldBar, newBar) in new[] { ("front", current.Bars.Front, desired.Bars.Front), ("back", current.Bars.Back, desired.Bars.Back) })
                        for (var i = 0; i < Math.Max(oldBar.Length, newBar.Length); i++)
                        {
                            long? Normal(long? id) => id.HasValue ? BuildAnalysis.Family(id.Value, catalog) : null;
                            Add($"bars/{name}/{i + 1}", Normal(oldBar.ElementAtOrDefault(i)), Normal(newBar.ElementAtOrDefault(i)));
                        }
                    break;
                case BuildSections.Attributes: Add("attributes", current.Attributes, desired.Attributes); break;
                case BuildSections.ChampionPoints:
                    foreach (var id in current.ChampionPoints.Keys.Union(desired.ChampionPoints.Keys))
                        Add($"championPoints/{id}", current.ChampionPoints.GetValueOrDefault(id), desired.ChampionPoints.GetValueOrDefault(id));
                    Add("championSlots", current.ChampionSlots, desired.ChampionSlots); break;
                case BuildSections.Mundus: Add("mundus", current.Mundus, desired.Mundus); break;
                case BuildSections.Equipment:
                    foreach (var (slot, target) in desired.Equipment)
                    {
                        var actual = current.Equipment.GetValueOrDefault(slot);
                        if (actual is null) { Add($"equipment/{slot}", null, target); continue; }
                        if (target.ItemId.HasValue) Add($"equipment/{slot}/itemId", actual.ItemId, target.ItemId);
                        if (target.SetId.HasValue) Add($"equipment/{slot}/setId", actual.SetId, target.SetId);
                        if (target.Trait.HasValue) Add($"equipment/{slot}/trait", actual.Trait, target.Trait);
                        if (target.Quality.HasValue) Add($"equipment/{slot}/quality", actual.Quality, target.Quality);
                        if (target.EnchantmentEffectId.HasValue) Add($"equipment/{slot}/enchantment", actual.EnchantmentEffectId, target.EnchantmentEffectId);
                    }
                    break;
            }
        }
        return differences;
    }
}
