using EsoData.Accounts;
using EsoData.Catalogs;

namespace EsoData.Builds;

public sealed record BuildFinding(string Severity, string Code, string Path, string Message);
public sealed record PointBudget(int? Total, int Desired, int? Remaining, int? CurrentlyUnspent, int CurrentAllocation,
    int IncrementalCost, int Refundable);
public sealed class BuildReport
{
    public PointBudget? SkillPoints { get; set; }
    public List<BuildFinding> Findings { get; set; } = [];
    public bool Valid => Findings.All(f => f.Severity != "error");
    public bool ReadyNow => Findings.All(f => f.Severity is not ("error" or "unmet" or "unknown"));
}

public static class BuildAnalysis
{
    public static long Family(long id, GameCatalog catalog) => catalog.Skills.GetValueOrDefault(id)?.BaseAbilityId ?? id;
    public static BuildReport Validate(EsoCharacter character, CharacterBuild build, GameCatalog catalog,
        BuildConstraints? constraints = null, BuildSections? sections = null)
    {
        constraints ??= new();
        var parts = sections ?? build.Sections;
        var report = new BuildReport();
        void Add(string severity, string code, string path, string message) => report.Findings.Add(new(severity, code, path, message));
        var current = character.Build;
        if (parts.HasFlag(BuildSections.Skills))
        {
            int Cost(SkillPurchase p) => p.IsPassive ? p.Rank : 1 + (p.Morph > 0 ? 1 : 0);
            var existing = current.Skills.GroupBy(s => Family(s.Key, catalog)).ToDictionary(g => g.Key, g => g.Max(s => Cost(s.Value)));
            var desired = build.Skills.GroupBy(s => Family(s.Key, catalog)).ToDictionary(g => g.Key, g => g.Max(s => Cost(s.Value)));
            var incremental = desired.Sum(s => Math.Max(0, s.Value - existing.GetValueOrDefault(s.Key)));
            var refundable = existing.Sum(s => Math.Max(0, s.Value - desired.GetValueOrDefault(s.Key)));
            var total = character.Progress.TotalSkillPoints;
            report.SkillPoints = new(total, build.SkillPointCost, total - build.SkillPointCost,
                character.Progress.UnspentSkillPoints, current.SkillPointCost, incremental, refundable);
            if (total is null) Add("unknown", "skill-budget", "skills", "Total skill-point budget is unobserved.");
            else if (build.SkillPointCost + constraints.ReserveSkillPoints > total)
                Add("error", "skill-budget", "skills", $"Requires {build.SkillPointCost} + {constraints.ReserveSkillPoints} reserved; total is {total}.");
            if (constraints.ReserveSkillPoints < 0) Add("error", "reserve", "constraints", "Reserve must be nonnegative.");
            if (!constraints.FullRespec && incremental > character.Progress.UnspentSkillPoints)
                Add("unmet", "respec-required", "skills", "Incremental purchases exceed unspent points; refund or respec first.");
            foreach (var duplicate in build.Skills.GroupBy(s => Family(s.Key, catalog)).Where(g => g.Count() > 1))
                Add("error", "duplicate-skill", "skills", $"Multiple allocations for skill family {duplicate.Key}.");
            foreach (var (id, purchase) in build.Skills)
            {
                var path = $"skills/{id}";
                if (id <= 0 || purchase.Rank < 1 || purchase.Morph is < 0 or > 2)
                    Add("error", "allocation", path, "Invalid skill ID, rank or morph.");
                var definition = catalog.Skills.GetValueOrDefault(id);
                if (definition is null) Add("unknown", "definition", path, "Skill definition is missing.");
                else
                {
                    if (definition.IsPassive.HasValue && definition.IsPassive != purchase.IsPassive)
                        Add("error", "skill-type", path, "Active/passive allocation does not match definition.");
                    if (purchase.IsPassive)
                    {
                        var max = catalog.Skills.Values.Where(s => Family(s.Id, catalog) == Family(id, catalog)).Max(s => s.Rank);
                        if (max.HasValue && purchase.Rank > max) Add("unknown", "passive-rank", path, "Requested rank exceeds available catalog ranks.");
                    }
                }
                var observed = character.Progress.Skills?.Where(s => Family(s.AbilityId, catalog) == Family(id, catalog)).ToArray() ?? [];
                if (!observed.Any(s => s.IsPassive == purchase.IsPassive && (purchase.IsPassive ? s.Rank >= purchase.Rank
                    : s.Morph == purchase.Morph || (purchase.Morph == 0))))
                {
                    if (!purchase.IsPassive && purchase.Morph > 0 && observed.Any(s => s.Morph == 0 && s.Rank < 4))
                        Add("unmet", "morph-xp", path, "Observed base ability has not reached morph rank.");
                    else Add("unknown", "unlock", path, "Purchase/rank eligibility is not established by the observed skills.");
                }
            }
            if (build.RuntimeClassLines is { Length: > 0 } && build.NativeTemplate is null)
                Add("unknown", "runtime-class-lines", "runtimeClassLines", "Runtime class-line IDs need verified native provenance; catalog line IDs are not interchangeable.");
        }
        if (parts.HasFlag(BuildSections.Bars))
        {
            foreach (var (name, bar) in new[] { ("front", build.Bars.Front), ("back", build.Bars.Back) })
            {
                if (bar.Length != 6) { Add("error", "bar-length", name, "A bar has five skills and one ultimate."); continue; }
                for (var i = 0; i < bar.Length; i++)
                {
                    var path = $"bars/{name}/{i + 1}";
                    if (bar[i] is not long id)
                    { if (constraints.RequireFullBars) Add("error", "empty-slot", path, "Requested full bar has an empty slot."); continue; }
                    if (id < 0)
                    {
                        if (!build.ScribedSkills.Any(s => s.CraftedAbilityId == -id)) Add("unknown", "scribed-ability", path, "Script configuration is unavailable.");
                        continue;
                    }
                    var family = Family(id, catalog);
                    var purchases = parts.HasFlag(BuildSections.Skills) ? build.Skills : current.Skills;
                    if (!purchases.Keys.Any(p => Family(p, catalog) == family)) Add("error", "not-purchased", path, "Slotted ability is absent from the selected allocation.");
                    var observed = character.Progress.Skills?.FirstOrDefault(s => Family(s.AbilityId, catalog) == family);
                    var ultimate = catalog.Skills.GetValueOrDefault(id)?.IsUltimate ?? observed?.IsUltimate;
                    if (ultimate.HasValue && ultimate != (i == 5)) Add("error", "slot-type", path, "Ultimate/ordinary ability is in the wrong slot.");
                    if (ultimate is null) Add("unknown", "slot-type", path, "Ultimate/ordinary classification is missing.");
                    if (constraints.AvoidMaxedMorphs && observed is { Morph: > 0, Rank: >= 4 })
                        Add("error", "maxed-filler", path, "Already-maxed morph violates the leveling constraint.");
                }
            }
        }
        if (parts.HasFlag(BuildSections.Attributes))
        {
            var a = build.Attributes;
            if (a.Health < 0 || a.Magicka < 0 || a.Stamina < 0) Add("error", "attributes", "attributes", "Attributes must be nonnegative.");
            if (current.Sections.HasFlag(BuildSections.Attributes))
            {
                var budget = current.Attributes.Health + current.Attributes.Magicka + current.Attributes.Stamina;
                if (a.Health + a.Magicka + a.Stamina > budget) Add("unknown", "attribute-budget", "attributes", "Proposed allocation exceeds observed allocated attributes; unspent budget is unknown.");
            }
        }
        if (parts.HasFlag(BuildSections.ChampionPoints)) CheckChampion();
        return report;

        void CheckChampion()
        {
            if (build.ChampionSlots.Length != 12) Add("error", "cp-slots", "championSlots", "Twelve CP slot positions are required.");
            if (build.ChampionPoints.Any(p => p.Key <= 0 || p.Value < 0)) Add("error", "cp-points", "championPoints", "CP IDs must be positive and points nonnegative.");
            if (character.Progress.TotalChampionPoints is int total && build.ChampionPoints.Values.Sum() > total)
                Add("error", "cp-total", "championPoints", "Allocation exceeds observed total Champion Points.");
            var disciplines = new[] { "Craft", "Warfare", "Fitness" };
            foreach (var (id, points) in build.ChampionPoints.Where(p => p.Value > 0))
            {
                if (!catalog.ChampionStars.TryGetValue(id, out var star))
                { Add("unknown", "cp-rules", $"championPoints/{id}", "CP discipline/cap/prerequisite rules are not in the catalog."); continue; }
                if (points > star.MaximumPoints) Add("error", "cp-cap", $"championPoints/{id}", "Allocation exceeds star cap.");
                foreach (var need in star.Prerequisites ?? new Dictionary<long, int>())
                    if (build.ChampionPoints.GetValueOrDefault(need.Key) < need.Value) Add("error", "cp-prerequisite", $"championPoints/{id}", $"Requires {need.Value} points in {need.Key}.");
            }
            foreach (var discipline in character.Progress.ChampionBudgets)
                if (build.ChampionPoints.Where(p => catalog.ChampionStars.GetValueOrDefault(p.Key)?.Discipline == discipline.Key).Sum(p => p.Value) > discipline.Value)
                    Add("error", "cp-discipline-budget", "championPoints", $"Exceeds {discipline.Key} budget {discipline.Value}.");
            for (var i = 0; i < Math.Min(12, build.ChampionSlots.Length); i++)
            {
                if (build.ChampionSlots[i] is not long id) continue;
                if (build.ChampionPoints.GetValueOrDefault(id) <= 0) Add("error", "cp-unallocated", $"championSlots/{i}", "Slotted star has no points.");
                if (catalog.ChampionStars.TryGetValue(id, out var star) && (!star.Slottable || star.Discipline != disciplines[i / 4]
                    || build.ChampionPoints.GetValueOrDefault(id) < star.MinimumSlottablePoints))
                    Add("error", "cp-slot", $"championSlots/{i}", "Star cannot be slotted here at the selected allocation.");
            }
            if (build.ChampionSlots.Where(x => x.HasValue).Distinct().Count() != build.ChampionSlots.Count(x => x.HasValue))
                Add("error", "duplicate-cp-slot", "championSlots", "A star is slotted more than once.");
        }
    }
}
