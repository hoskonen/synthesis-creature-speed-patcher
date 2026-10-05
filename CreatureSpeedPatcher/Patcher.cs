using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace CreatureSpeedPatcher;

public sealed class GroupReport
{
    public int Matched { get; internal set; }
    public int WouldChange { get; internal set; }
    public int Written { get; internal set; }
    public int AlreadyCorrect { get; internal set; }
    public int Excluded { get; internal set; }
    public int Inherited { get; internal set; }
    public int Disabled { get; internal set; }
}

public sealed class PatchReport
{
    public int Inspected { get; internal set; }
    public int Deleted { get; internal set; }
    public int TemplateSkips { get; internal set; }
    public int Unmatched { get; internal set; }
    public Dictionary<string, GroupReport> Groups { get; } =
        CreatureGroupCatalog.Groups.ToDictionary(g => g.Id, _ => new GroupReport());
    public int TotalWritten => Groups.Values.Sum(g => g.Written);
    public int TotalWouldChange => Groups.Values.Sum(g => g.WouldChange);
}

public static class Patcher
{
    private sealed record PlannedChange(INpcGetter Npc, CreatureGroup Group, short Before, short Speed);

    public static PatchReport Run(IEnumerable<INpcGetter> winners,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache, ISkyrimMod patch,
        Settings settings, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(winners);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        // Validate all settings before the first possible write.
        var configured = CreatureGroupCatalog.Groups.ToDictionary(g => g.Id, g =>
        {
            GroupSettings value = g.SelectSettings(settings)
                ?? throw new ArgumentException($"Missing settings for {g.Name}.");
            if (value.SpeedMultiplier is < 1 or > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(settings),
                    $"{g.Name}: Speed must be an integer from 1 to {short.MaxValue}.");
            return (value.Enabled, Speed: (short)value.SpeedMultiplier);
        });

        var report = new PatchReport();
        var plan = new List<PlannedChange>();
        foreach (INpcGetter npc in winners)
        {
            report.Inspected++;
            if (npc.IsDeleted) { report.Deleted++; continue; }
            if (CreatureGroupCatalog.Exclusions.TryGetValue(npc.FormKey, out var excludedGroup))
            {
                report.Groups[excludedGroup.Id].Excluded++;
                LogSkip("explicit exclusion");
                continue;
            }
            RaceResolution resolution = TemplateMatcher.ResolveRace(npc, cache);
            if (!resolution.Success)
            {
                report.TemplateSkips++;
                LogSkip(resolution.SkipReason!);
                continue;
            }
            if (!CreatureGroupCatalog.ByRace.TryGetValue(resolution.Race, out var group))
            { report.Unmatched++; continue; }
            GroupReport counts = report.Groups[group.Id];
            counts.Matched++;
            var desired = configured[group.Id];
            if (!desired.Enabled) { counts.Disabled++; continue; }
            if (!TemplateMatcher.OwnsStats(npc))
            {
                counts.Inherited++;
                LogSkip($"{group.Name}: inherits stats; no child override");
                continue;
            }
            short before = npc.Configuration.SpeedMultiplier;
            if (before == desired.Speed) { counts.AlreadyCorrect++; continue; }
            plan.Add(new PlannedChange(npc, group, before, desired.Speed));
            counts.WouldChange++;

            void LogSkip(string reason)
            {
                if (settings.VerboseLogging)
                    output.WriteLine($"SKIP {npc.FormKey} [{npc.EditorID ?? "<no EditorID>"}]: {reason}");
            }
        }
        foreach (PlannedChange change in plan)
        {
            if (!settings.DryRun)
            {
                var patchedNpc = patch.Npcs.GetOrAddAsOverride(change.Npc);
                patchedNpc.Configuration.SpeedMultiplier = change.Speed;
                report.Groups[change.Group.Id].Written++;
            }
            if (settings.VerboseLogging)
                output.WriteLine(FormattableString.Invariant(
                    $"{(settings.DryRun ? "WOULD CHANGE" : "PATCH")} {change.Npc.FormKey} [{change.Npc.EditorID ?? "<no EditorID>"}] {change.Group.Name}: {change.Before} -> {change.Speed}"));
        }
        output.WriteLine("Creature Speed Patcher");
        output.WriteLine($"mode = {(settings.DryRun ? "DRY RUN" : "NORMAL")}");
        output.WriteLine(FormattableString.Invariant($"Winning NPCs inspected: {report.Inspected}"));
        foreach (CreatureGroup group in CreatureGroupCatalog.Groups)
        {
            GroupReport c = report.Groups[group.Id];
            output.WriteLine(FormattableString.Invariant(
                $"{group.Name}: matched={c.Matched}, would change={c.WouldChange}, written={c.Written}, already correct={c.AlreadyCorrect}, excluded={c.Excluded}, inherits stats={c.Inherited}, disabled={c.Disabled}"));
        }
        output.WriteLine(FormattableString.Invariant(
            $"Deleted={report.Deleted}; unresolved/ambiguous/unsupported templates={report.TemplateSkips}; unmatched={report.Unmatched}; total overrides that would be written={report.TotalWouldChange}; actual overrides written={report.TotalWritten}"));
        return report;
    }
}
