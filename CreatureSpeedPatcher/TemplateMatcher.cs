using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace CreatureSpeedPatcher;

public readonly record struct RaceResolution(FormKey Race, string? SkipReason)
{
    public bool Success => SkipReason is null;
}

public static class TemplateMatcher
{
    public static bool OwnsStats(INpcGetter npc) =>
        !npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Stats);

    public static RaceResolution ResolveRace(
        INpcGetter npc, ILinkCache<ISkyrimMod, ISkyrimModGetter> cache)
    {
        var visited = new HashSet<FormKey>();
        INpcGetter current = npc;
        for (int depth = 0; depth < 64; depth++)
        {
            if (!visited.Add(current.FormKey))
                return new(default, "cyclic trait template");
            if (current.IsDeleted)
                return new(default, "deleted trait template");
            if (!current.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
                return current.Race.FormKey.IsNull
                    ? new(default, "missing effective race")
                    : new(current.Race.FormKey, null);
            FormKey template = current.Template.FormKey;
            if (template.IsNull)
                return new(default, "Use Traits with no template");
            if (cache.TryResolve<INpcGetter>(template, out var provider, ResolveTarget.Winner))
            {
                current = provider;
                continue;
            }
            // Never pick an LVLN branch or trust a placeholder RNAM.
            return new(default, cache.TryResolve<ILeveledNpcGetter>(template, out _, ResolveTarget.Winner)
                ? "leveled-list trait template (unsupported in v1)"
                : "unresolved trait template");
        }
        return new(default, "trait template depth exceeds 64");
    }
}
