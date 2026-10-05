using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace CreatureSpeedPatcher.Tests;

[TestClass]
public sealed class PatcherTests
{
    private static FormKey Key(string value) => FormKey.Factory(value);
    private static SkyrimMod Mod(string name = "Input.esp") =>
        new(ModKey.FromFileName(name), SkyrimRelease.SkyrimSE);

    private static Npc AddNpc(SkyrimMod mod, string race = "0131F8:Skyrim.esm",
        short speed = 100, string? identity = null)
    {
        var npc = identity is null
            ? new Npc(mod)
            : new Npc(Key(identity), SkyrimRelease.SkyrimSE);
        npc.Race.SetTo(Key(race));
        npc.Configuration.SpeedMultiplier = speed;
        mod.Npcs.Add(npc);
        return npc;
    }

    private static (SkyrimMod Patch, PatchReport Report) Run(SkyrimMod mod, Settings? settings = null)
    {
        using var cache = mod.ToImmutableLinkCache();
        var patch = Mod("Output.esp");
        var report = Patcher.Run(mod.Npcs, cache, patch, settings ?? new(), new StringWriter());
        return (patch, report);
    }

    private static Settings Faster() => new()
    {
        FrostbiteSpiders = new(250), DragonbornImbuedSpiders = new(375),
        Chaurus = new(180), ChaurusReapers = new(180),
        ChaurusHunters = new(180), FrozenChaurus = new(216),
    };

    [TestMethod]
    public void EveryAuditedRaceMatchesOneGroupAndDefaultsProduceNoOverrides()
    {
        var mod = Mod();
        var defaults = new Settings();
        var races = new HashSet<FormKey>();
        foreach (var group in CreatureGroupCatalog.Groups)
        {
            foreach (var race in group.Races)
            {
                Assert.IsTrue(races.Add(race), "A race must belong to one group.");
                AddNpc(mod, race.ToString(), (short)group.SelectSettings(defaults).SpeedMultiplier);
            }
        }
        var (patch, report) = Run(mod);
        Assert.AreEqual(9, report.Inspected);
        Assert.AreEqual(0, patch.Npcs.Count);
        Assert.AreEqual(9, report.Groups.Values.Sum(g => g.AlreadyCorrect));
        Assert.AreEqual(9, report.Groups.Values.Sum(g => g.Matched));
        Assert.AreEqual(typeof(short), typeof(NpcConfiguration).GetProperty("SpeedMultiplier")!.PropertyType);
    }

    [TestMethod]
    public void CustomSpeedsPreserveNonSpeedDataAndInputIsNotMutated()
    {
        var mod = Mod();
        var npc = AddNpc(mod);
        npc.EditorID = "ArbitraryName";
        npc.Name = "Friendly spider";
        npc.Height = 1.25f;
        npc.Configuration.HealthOffset = 75;
        npc.Configuration.Flags = NpcConfiguration.Flag.Unique;
        npc.Template.SetTo(Key("000888:Input.esp")); // No inherited data flags.
        npc.Factions.Add(new RankPlacement
        {
            Faction = new FormLink<IFactionGetter>(Key("02997F:Skyrim.esm")), Rank = 2
        });
        npc.ActorEffect = new();
        npc.ActorEffect.Add(Key("012345:Input.esp"));
        var (patch, report) = Run(mod, Faster());
        var result = patch.Npcs[npc.FormKey];
        Assert.AreEqual((short)250, result.Configuration.SpeedMultiplier);
        Assert.AreEqual((short)100, npc.Configuration.SpeedMultiplier);
        Assert.AreEqual(npc.EditorID, result.EditorID);
        Assert.AreEqual(npc.Name, result.Name);
        Assert.AreEqual(npc.Height, result.Height);
        Assert.AreEqual(npc.Configuration.HealthOffset, result.Configuration.HealthOffset);
        Assert.AreEqual(npc.Configuration.Flags, result.Configuration.Flags);
        Assert.AreEqual(npc.Template.FormKey, result.Template.FormKey);
        Assert.AreEqual(npc.Configuration.TemplateFlags, result.Configuration.TemplateFlags);
        Assert.AreEqual(npc.Factions[0].Faction.FormKey, result.Factions[0].Faction.FormKey);
        Assert.AreEqual(npc.Factions[0].Rank, result.Factions[0].Rank);
        Assert.AreEqual(npc.ActorEffect[0].FormKey, result.ActorEffect![0].FormKey);
        Assert.AreEqual(1, report.TotalWritten);
    }

    [TestMethod]
    public void EveryExclusionWinsEvenWhenAssignedAnotherSupportedRace()
    {
        var mod = Mod();
        foreach (var key in CreatureGroupCatalog.Exclusions.Keys)
            AddNpc(mod, "015136:Dawnguard.esm", 100, key.ToString());
        var (patch, report) = Run(mod, Faster());
        Assert.AreEqual(8, report.Groups.Values.Sum(g => g.Excluded));
        Assert.AreEqual(0, report.Groups.Values.Sum(g => g.Matched));
        Assert.AreEqual(0, patch.Npcs.Count);
    }

    [TestMethod]
    public void InheritedStatsAreSkippedAndTraitChainResolvesForOwnStats()
    {
        var mod = Mod();
        var provider = AddNpc(mod);
        var child = AddNpc(mod, "109C7C:Skyrim.esm");
        child.Template.SetTo(provider.FormKey);
        child.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits | NpcConfiguration.TemplateFlag.Stats;
        var ownStats = AddNpc(mod, "109C7C:Skyrim.esm");
        ownStats.Template.SetTo(child.FormKey);
        ownStats.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var (patch, report) = Run(mod, Faster());
        Assert.AreEqual(2, patch.Npcs.Count);
        Assert.IsFalse(patch.Npcs.ContainsKey(child.FormKey));
        Assert.IsTrue(patch.Npcs.ContainsKey(ownStats.FormKey));
        Assert.AreEqual(1, report.Groups["frostbite-spiders"].Inherited);
        Assert.AreEqual(3, report.Groups["frostbite-spiders"].Matched);
        Assert.AreEqual(NpcConfiguration.TemplateFlag.Traits,
            patch.Npcs[ownStats.FormKey].Configuration.TemplateFlags);
    }

    [TestMethod]
    public void DeletedDisabledAndUnmatchedRecordsAreSkippedWithoutStringMatching()
    {
        var mod = Mod();
        var deleted = AddNpc(mod);
        deleted.IsDeleted = true;
        AddNpc(mod);
        var dwarven = AddNpc(mod, "0131F3:Skyrim.esm");
        dwarven.EditorID = "EncFrostbiteSpider";
        dwarven.Name = "Frostbite Spider";
        var settings = Faster();
        settings.FrostbiteSpiders.Enabled = false;
        var (patch, report) = Run(mod, settings);
        Assert.AreEqual(0, patch.Npcs.Count);
        Assert.AreEqual(1, report.Deleted);
        Assert.AreEqual(1, report.Unmatched);
        Assert.AreEqual(1, report.Groups["frostbite-spiders"].Disabled);
    }

    [TestMethod]
    public void BrokenCyclicDeletedAndLeveledTraitTemplatesAreSkipped()
    {
        var mod = Mod();
        var missing = AddNpc(mod);
        missing.Template.SetTo(Key("099999:Input.esp"));
        missing.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var noLink = AddNpc(mod);
        noLink.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var cyclic = AddNpc(mod);
        cyclic.Template.SetTo(cyclic.FormKey);
        cyclic.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var deleted = AddNpc(mod);
        deleted.IsDeleted = true;
        var child = AddNpc(mod);
        child.Template.SetTo(deleted.FormKey);
        child.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var list = new LeveledNpc(mod);
        mod.LeveledNpcs.Add(list);
        var wrapper = AddNpc(mod);
        wrapper.Template.SetTo(list.FormKey);
        wrapper.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var (patch, report) = Run(mod, Faster());
        Assert.AreEqual(0, patch.Npcs.Count);
        Assert.AreEqual(5, report.TemplateSkips);
        Assert.AreEqual(1, report.Deleted);
    }

    [TestMethod]
    public void InvalidSettingsFailBeforeAnyWrite()
    {
        var mod = Mod();
        AddNpc(mod);
        using var cache = mod.ToImmutableLinkCache();
        foreach (int invalid in new[] { -1, 0, 32768 })
        {
            var patch = Mod("Output.esp");
            var settings = Faster();
            settings.FrozenChaurus.SpeedMultiplier = invalid;
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                Patcher.Run(mod.Npcs, cache, patch, settings, new StringWriter()));
            Assert.AreEqual(0, patch.Npcs.Count);
        }
    }

    [TestMethod]
    public void WinningInputAndWinningTraitProviderAreUsedAndRepeatedRunDoesNotCompound()
    {
        var original = Mod();
        var provider = AddNpc(original);
        var child = AddNpc(original);
        child.Template.SetTo(provider.FormKey);
        child.Configuration.TemplateFlags = NpcConfiguration.TemplateFlag.Traits;
        var overhaul = Mod("Overhaul.esp");
        var changedProvider = overhaul.Npcs.GetOrAddAsOverride(provider);
        changedProvider.Race.SetTo(Key("0A5601:Skyrim.esm"));
        var winner = overhaul.Npcs.GetOrAddAsOverride(child);
        winner.Configuration.SpeedMultiplier = 200;
        winner.Configuration.HealthOffset = 123;
        var loadOrder = new ISkyrimModGetter[] { original, overhaul };
        using var cache = loadOrder.ToImmutableLinkCache();
        var patch = Mod("Output.esp");
        var settings = Faster();
        settings.ChaurusReapers.SpeedMultiplier = 180;
        var report = Patcher.Run(loadOrder.Reverse().Npc().WinningOverrides(), cache,
            patch, settings, new StringWriter());
        Assert.AreEqual(2, report.TotalWritten);
        Assert.AreEqual((short)180, patch.Npcs[child.FormKey].Configuration.SpeedMultiplier);
        Assert.AreEqual((short)123, patch.Npcs[child.FormKey].Configuration.HealthOffset);
        // The child still points to an external provider, so resolve with the full order.
        var all = new ISkyrimModGetter[] { original, overhaul, patch };
        using var fullCache = all.ToImmutableLinkCache();
        var repeatPatch = Mod("Repeat.esp");
        var repeat = Patcher.Run(all.Reverse().Npc().WinningOverrides(), fullCache,
            repeatPatch, settings, new StringWriter());
        Assert.AreEqual(0, repeat.TotalWritten);
        Assert.AreEqual(0, repeatPatch.Npcs.Count);
    }
}
