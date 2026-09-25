using EsoData.Catalogs;
using EsoData.Formats;
using EsoData.Items;

namespace EsoData.Builds;

public static class BuildCodec
{
    private static readonly int[] EquipmentSlots = [0, 3, 2, 16, 6, 8, 9, 1, 11, 12, 4, 5, 20, 21];
    public static CharacterBuild Import(CspsBuild native)
    {
        var build = new CharacterBuild { NativeTemplate = native.ToString() };
        if (native.Skills is { } skills)
        {
            foreach (var s in skills.Active) build.Skills[s.AbilityId] = new() { Morph = s.Morph };
            foreach (var s in skills.Passive) build.Skills[s.AbilityId] = new() { Rank = s.Rank, IsPassive = true };
            build.ScribedSkills = skills.Crafted?.ToList() ?? [];
            build.SkillStyles = skills.Styles?.ToList() ?? [];
            build.RuntimeClassLines = skills.Subclasses?.ToArray();
            build.Sections |= BuildSections.Skills;
        }
        if (native.Bars is { } bars)
        {
            // Negative IDs identify native crafted abilities; ordinary IDs retain the native base ID.
            long?[] Bar(int i) => i < bars.Count ? bars[i].Select(s => s is null ? (long?)null : s.IsCrafted ? -s.AbilityId : s.AbilityId).ToArray() : new long?[6];
            build.Bars = new() { Front = Bar(0), Back = Bar(1), Overload = bars.Count > 2 ? Bar(2) : null };
            build.Sections |= BuildSections.Bars;
        }
        if (native.Attributes is { } a)
        { build.Attributes = new() { Health = a.Health, Magicka = a.Magicka, Stamina = a.Stamina }; build.Sections |= BuildSections.Attributes; }
        if (native.ChampionPoints is { } cp)
        {
            build.ChampionPoints = cp.Allocations.ToDictionary(x => x.Id, x => x.Points ?? 0);
            build.ChampionSlots = cp.Bars.SelectMany(x => x).ToArray(); build.Sections |= BuildSections.ChampionPoints;
        }
        if (native.Gear is { } gear)
        {
            for (var i = 0; i < Math.Min(EquipmentSlots.Length, gear.Count); i++)
                if (gear[i] is { SetId: > 0 } g) build.Equipment[EquipmentSlots[i]] = new()
                { SetId = g.SetId, Type = g.Type, Trait = g.Trait, Quality = g.Quality, EnchantmentEffectId = g.EnchantmentEffectId };
            build.Sections |= BuildSections.Equipment;
        }
        if (native.Mundus.HasValue) { build.Mundus = native.Mundus; build.Sections |= BuildSections.Mundus; }
        return build;
    }

    public static string Export(CharacterBuild build, GameCatalog catalog, BuildSections sections)
    {
        if ((sections & ~BuildSections.All) != 0 || sections == BuildSections.None) throw new ArgumentException("Choose supported build sections.");
        if ((sections & ~build.Sections) != 0) throw new ArgumentException($"Build does not contain sections: {sections & ~build.Sections}.");
        var native = new CspsBuild();
        if (sections.HasFlag(BuildSections.Skills))
        {
            var active = new List<ActiveSkill>(); var passive = new List<PassiveSkill>();
            foreach (var (id, purchase) in build.Skills)
            {
                if (purchase.IsPassive)
                {
                    var definition = catalog.Skills.GetValueOrDefault(id);
                    var baseId = definition?.BaseAbilityId ?? (build.NativeTemplate is not null ? id : (long?)null);
                    if (baseId is not > 0) throw new InvalidOperationException($"Passive base mapping missing for {id}.");
                    passive.Add(new(baseId.Value, purchase.Rank));
                }
                else
                {
                    var definition = catalog.Skills.GetValueOrDefault(id);
                    var family = definition?.BaseAbilityId ?? id;
                    var selected = catalog.Skills.Values.FirstOrDefault(s => s.BaseAbilityId == family && s.Morph == purchase.Morph && s.Rank == 1);
                    if (selected is not null) active.Add(catalog.ToActiveSkill(selected.Id));
                    else if (build.NativeTemplate is not null && CspsBuild.Parse(build.NativeTemplate).Skills?.Active
                        .Any(s => s.AbilityId == id && s.Morph == purchase.Morph) == true) active.Add(new(id, purchase.Morph));
                    else throw new InvalidOperationException($"Selected morph rank-one mapping missing for {id}, morph {purchase.Morph}.");
                }
            }
            native.Skills = new(active, passive, build.ScribedSkills, build.SkillStyles, build.RuntimeClassLines);
        }
        if (sections.HasFlag(BuildSections.Bars))
        {
            BarSlot? Slot(long? id) => id is null ? null : id < 0 ? new(-id.Value, true) : catalog.ToBarSlot(id.Value);
            var bars = new List<IReadOnlyList<BarSlot?>> { build.Bars.Front.Select(Slot).ToArray(), build.Bars.Back.Select(Slot).ToArray() };
            if (build.Bars.Overload is not null) bars.Add(build.Bars.Overload.Select(Slot).ToArray());
            native.Bars = bars;
        }
        if (sections.HasFlag(BuildSections.Attributes)) native.Attributes = new(build.Attributes.Health, build.Attributes.Magicka, build.Attributes.Stamina);
        if (sections.HasFlag(BuildSections.ChampionPoints)) native.ChampionPoints = new(
            build.ChampionPoints.Select(x => new ChampionStar(x.Key, x.Value)).ToArray(),
            build.ChampionSlots.Chunk(4).Select(x => (IReadOnlyList<long?>)x).ToArray());
        if (sections.HasFlag(BuildSections.Equipment))
        {
            var slots = new CspsGearSlot?[16];
            foreach (var (slot, choice) in build.Equipment)
            {
                var i = Array.IndexOf(EquipmentSlots, slot);
                if (i < 0) throw new ArgumentException($"Unsupported equipment slot {slot}.");
                if (choice.SetId is not > 0 || choice.Type is null || choice.Trait is null || choice.Quality is null || choice.EnchantmentEffectId is null)
                    throw new InvalidOperationException($"Equipment slot {slot} requires resolved set, type, trait, quality and enchantment effect.");
                slots[i] = new(choice.SetId.Value, choice.Type.Value, choice.Trait.Value, choice.Quality.Value, choice.EnchantmentEffectId.Value);
            }
            native.Gear = slots;
        }
        if (sections.HasFlag(BuildSections.Mundus)) native.Mundus = build.Mundus;
        return native.ToString();
    }
    public static string Crafting(IEnumerable<CraftingOrder> orders) => CraftingQueue.Write(orders.SelectMany(o =>
    {
        if (o.Quantity is < 1 or > 100) throw new ArgumentException("Crafting quantity must be 1..100 per order.");
        return Enumerable.Repeat(CraftedItem.Create(o.ItemId, o.Level, (ItemQuality)o.Quality, o.StyleId,
            o.ChampionPoints, o.EnchantmentItemId, o.EnchantmentQuality is int q ? (ItemQuality)q : null), o.Quantity);
    }));
}
