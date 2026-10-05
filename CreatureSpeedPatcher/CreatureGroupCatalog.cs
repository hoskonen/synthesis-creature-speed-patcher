using System.Collections.Frozen;
using Mutagen.Bethesda.Plugins;

namespace CreatureSpeedPatcher;

public sealed record CreatureGroup(string Id, string Name,
    Func<Settings, GroupSettings> SelectSettings,
    IReadOnlySet<FormKey> Races, IReadOnlySet<FormKey> ExcludedNpcs);

public static class CreatureGroupCatalog
{
    public static IReadOnlyList<CreatureGroup> Groups { get; } = Array.AsReadOnly<CreatureGroup>(
    [
        new("frostbite-spiders", "Frostbite Spiders", s => s.FrostbiteSpiders,
            Keys("0131F8:Skyrim.esm", "053477:Skyrim.esm", "04E507:Skyrim.esm"),
            Keys("038A33:Skyrim.esm", "0BF55C:Skyrim.esm", "0A19FE:Skyrim.esm",
                "0A19FF:Skyrim.esm", "03A1E0:Skyrim.esm", "01A5C6:Dawnguard.esm")),
        new("imbued-spiders", "Dragonborn Imbued Spiders", s => s.DragonbornImbuedSpiders,
            Keys("014449:Dragonborn.esm", "027483:Dragonborn.esm"), Keys()),
        new("chaurus", "Chaurus", s => s.Chaurus,
            Keys("0131EB:Skyrim.esm"), Keys("05C305:Skyrim.esm")),
        new("chaurus-reapers", "Chaurus Reapers", s => s.ChaurusReapers,
            Keys("0A5601:Skyrim.esm"), Keys()),
        new("chaurus-hunters", "Chaurus Hunters / Fledglings", s => s.ChaurusHunters,
            Keys("0051FB:Dawnguard.esm"), Keys("008E39:Dawnguard.esm")),
        new("frozen-chaurus", "Frozen Chaurus", s => s.FrozenChaurus,
            Keys("015136:Dawnguard.esm"), Keys()),
    ]);

    // Duplicate race keys fail catalog initialization rather than patching twice.
    public static IReadOnlyDictionary<FormKey, CreatureGroup> ByRace { get; } =
        Groups.SelectMany(g => g.Races.Select(r => (Race: r, Group: g)))
            .ToFrozenDictionary(x => x.Race, x => x.Group);
    // Exclusions take precedence even when a mod changes an NPC's race.
    public static IReadOnlyDictionary<FormKey, CreatureGroup> Exclusions { get; } =
        Groups.SelectMany(g => g.ExcludedNpcs.Select(n => (Npc: n, Group: g)))
            .ToFrozenDictionary(x => x.Npc, x => x.Group);
    private static FrozenSet<FormKey> Keys(params string[] keys) =>
        keys.Select(key => FormKey.Factory(key)).ToFrozenSet();
}
