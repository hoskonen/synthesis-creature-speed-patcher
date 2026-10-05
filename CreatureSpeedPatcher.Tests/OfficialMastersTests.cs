using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace CreatureSpeedPatcher.Tests;

[TestClass]
public sealed class OfficialMastersTests
{
    // Opt-in real-record verification, not a Skyrim runtime test. No files are exported.
    [TestMethod]
    public void OfficialRecordsMatchAuditedStatOwnersAndVanillaDefaults()
    {
        string? data = Environment.GetEnvironmentVariable("CREATURE_SPEED_GAME_DATA");
        if (string.IsNullOrWhiteSpace(data))
        {
            Assert.Inconclusive("Set CREATURE_SPEED_GAME_DATA to an official Skyrim SE Data directory.");
            return;
        }
        using var skyrim = SkyrimMod.CreateFromBinaryOverlay(Path.Combine(data, "Skyrim.esm"), SkyrimRelease.SkyrimSE);
        using var update = SkyrimMod.CreateFromBinaryOverlay(Path.Combine(data, "Update.esm"), SkyrimRelease.SkyrimSE);
        using var dawnguard = SkyrimMod.CreateFromBinaryOverlay(Path.Combine(data, "Dawnguard.esm"), SkyrimRelease.SkyrimSE);
        using var hearthfires = SkyrimMod.CreateFromBinaryOverlay(Path.Combine(data, "HearthFires.esm"), SkyrimRelease.SkyrimSE);
        using var dragonborn = SkyrimMod.CreateFromBinaryOverlay(Path.Combine(data, "Dragonborn.esm"), SkyrimRelease.SkyrimSE);
        var order = new ISkyrimModGetter[] { skyrim, update, dawnguard, hearthfires, dragonborn };
        using var cache = order.ToImmutableLinkCache();
        var vanillaPatch = new SkyrimMod(ModKey.FromFileName("VanillaTest.esp"), SkyrimRelease.SkyrimSE);
        var defaults = Patcher.Run(order.Reverse().Npc().WinningOverrides(), cache,
            vanillaPatch, new Settings(), new StringWriter());
        Assert.AreEqual(0, defaults.TotalWritten);
        Assert.AreEqual(0, vanillaPatch.Npcs.Count);

        var configuredPatch = new SkyrimMod(ModKey.FromFileName("ConfiguredTest.esp"), SkyrimRelease.SkyrimSE);
        var settings = new Settings
        {
            FrostbiteSpiders = new(250), DragonbornImbuedSpiders = new(375),
            Chaurus = new(180), ChaurusReapers = new(180),
            ChaurusHunters = new(180), FrozenChaurus = new(216)
        };
        var custom = Patcher.Run(order.Reverse().Npc().WinningOverrides(), cache,
            configuredPatch, settings, new StringWriter());
        Assert.AreEqual(31, custom.TotalWritten);
        Assert.AreEqual(8, custom.Groups["frostbite-spiders"].Written);
        Assert.AreEqual(13, custom.Groups["imbued-spiders"].Written);
        Assert.AreEqual(1, custom.Groups["chaurus"].Written);
        Assert.AreEqual(1, custom.Groups["chaurus-reapers"].Written);
        Assert.AreEqual(2, custom.Groups["chaurus-hunters"].Written);
        Assert.AreEqual(6, custom.Groups["frozen-chaurus"].Written);
        Assert.AreEqual(8, custom.Groups.Values.Sum(g => g.Excluded));
        foreach (var npc in configuredPatch.Npcs)
        {
            Assert.IsTrue(TemplateMatcher.OwnsStats(npc));
            Assert.IsFalse(CreatureGroupCatalog.Exclusions.ContainsKey(npc.FormKey));
        }
    }
}
