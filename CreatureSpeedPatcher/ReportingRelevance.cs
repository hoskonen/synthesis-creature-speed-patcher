using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace CreatureSpeedPatcher;

// Diagnostic evidence only. This never supplies an effective race or authorizes a write.
internal static class ReportingRelevance
{
    public static bool HasSupportedRaceEvidence(INpcGetter npc,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache)
    {
        var visited = new HashSet<FormKey>();
        var pending = new Stack<FormKey>();
        if (InspectNpc(npc)) return true;
        visited.Add(npc.FormKey);
        while (pending.TryPop(out var key))
        {
            if (key.IsNull || !visited.Add(key)) continue;
            if (cache.TryResolve<INpcGetter>(key, out var provider, ResolveTarget.Winner))
            {
                if (InspectNpc(provider)) return true;
            }
            else if (cache.TryResolve<ILeveledNpcGetter>(key, out var list, ResolveTarget.Winner)
                && list.Entries is not null)
            {
                foreach (var entry in list.Entries)
                    if (entry.Data is not null) pending.Push(entry.Data.Reference.FormKey);
            }
        }
        return false;

        bool InspectNpc(INpcGetter record)
        {
            // Even an inherited placeholder race is useful evidence for diagnostics only.
            if (CreatureGroupCatalog.ByRace.ContainsKey(record.Race.FormKey)) return true;
            if (record.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
                pending.Push(record.Template.FormKey);
            return false;
        }
    }
}
