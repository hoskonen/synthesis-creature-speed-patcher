using System.ComponentModel.DataAnnotations;
using Mutagen.Bethesda.Synthesis.Settings;

namespace CreatureSpeedPatcher;

public sealed class Settings
{
    [SynthesisSettingName("Frostbite Spiders")]
    public GroupSettings FrostbiteSpiders { get; set; } = new(100);
    [SynthesisSettingName("Dragonborn Imbued Spiders")]
    public GroupSettings DragonbornImbuedSpiders { get; set; } = new(150);
    public GroupSettings Chaurus { get; set; } = new(100);
    [SynthesisSettingName("Chaurus Reapers")]
    public GroupSettings ChaurusReapers { get; set; } = new(100);
    [SynthesisSettingName("Chaurus Hunters / Fledglings")]
    public GroupSettings ChaurusHunters { get; set; } = new(100);
    [SynthesisSettingName("Frozen Chaurus")]
    public GroupSettings FrozenChaurus { get; set; } = new(120);
    [SynthesisSettingName("Verbose Record Logging")]
    public bool VerboseLogging { get; set; }
}

public sealed class GroupSettings
{
    public GroupSettings() { }
    public GroupSettings(int speedMultiplier) => SpeedMultiplier = speedMultiplier;
    public bool Enabled { get; set; } = true;
    // Int settings allow invalid JSON numbers to be validated before any writes.
    [Range(1, short.MaxValue)]
    [SynthesisSettingName("Speed Multiplier")]
    public int SpeedMultiplier { get; set; } = 100;
}
