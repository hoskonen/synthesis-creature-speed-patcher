# Creature Speed Patcher

A Skyrim SE Synthesis patcher that sets NPC `ACBS / Speed Multiplier` for six audited creature groups. Settings use **final stored values**, not factors applied to the winning value.

| Group | Enabled by default | Speed Multiplier default |
|---|---|---:|
| Frostbite Spiders (normal, large, giant, snow) | Yes | 100 |
| Dragonborn Imbued Spiders | Yes | 150 |
| Chaurus | Yes | 100 |
| Chaurus Reapers | Yes | 100 |
| Chaurus Hunters / Fledglings | Yes | 100 |
| Frozen Chaurus | Yes | 120 |

These defaults produce no NPC overrides on the inspected official masters. On a modded load order, an enabled group **will reset an earlier mod's different speed to the configured value**. Disable a group to preserve its winning speed. Reference-equivalent targets are 250 / 375 / 180 / 180 / 180 / 216 respectively.

## Matching and writing

The catalog uses exact race FormKeys. Winning NPCs are inspected once; deleted records and explicit exclusions are skipped. If `Use Traits` is set, the matcher follows winning NPC-to-NPC template links to the effective race. Cycles, missing/deleted templates, null races, excessive depth and leveled-list trait templates are skipped. **All LVLN trait templates are unsupported in v1**, even homogeneous lists; the matcher never selects a random branch or falls back to a placeholder race.

Matched NPCs with `Use Stats` set receive no child override. Template links and flags stay intact. For enabled stat owners, an override copies the winning record and changes only `Configuration.SpeedMultiplier`, and only when the value differs. Inputs are not mutated. EditorIDs/names are diagnostic only; factions are not matching gates. Duplicate catalog race keys are rejected at initialization.

Speed settings must be positive integers from 1 through 32767, matching the pinned Mutagen `Int16` API. Every group's settings are validated before any write. Default logs contain per-group counts and overall skip totals. Enable **Verbose Record Logging** to see identities, changed speeds and template-skip reasons; template skips also include unrelated NPCs outside supported families because their effective race could not be established.

## Exclusions and intentional coverage

Explicit exclusions are `038A33:Skyrim.esm`, `0BF55C:Skyrim.esm`, `0A19FE:Skyrim.esm` (Lis), `0A19FF:Skyrim.esm` (Lis corpse), `03A1E0:Skyrim.esm` (wounded quest spider), `01A5C6:Dawnguard.esm` (wounded Dawnguard spider), `05C305:Skyrim.esm`, and `008E39:Dawnguard.esm`. The other four are audio providers.

Dragonborn and Frozen groups are included and enabled independently, as requested after the audit. Dragonborn matching includes friendly, Oil, Pack/cut and other special variants using the two audited races; no hostility classifier was added. Bandit spider-zombie actors and Dwarven spiders do not match those races. Frozen NPC stat owners are handled separately at their vanilla speed of 120.

An exclusion prevents a direct write; it does not stop inherited speed changes when a shared stat provider is patched. Harmugstahl docile cages, Honningbrew and Fellglow encounters can inherit changes. Dragonborn friendly/jumping children can also inherit changes. No template flags are cleared to isolate these descendants.

## Build and verification

Requires .NET 10. Package versions remain Mutagen Skyrim 0.54.4 and Synthesis 0.36.6.

```powershell
dotnet build CreatureSpeedPatcher.sln -m:1
dotnet test CreatureSpeedPatcher.sln --no-restore -m:1
```

Focused tests use in-memory mods to cover catalog matching, exact exclusions, default/custom settings, preserved winner fields, deleted/disabled records, stat inheritance, winning trait providers, broken/cyclic/LVLN templates and repeated runs. An optional test reads actual official master records without exporting anything:

```powershell
$env:CREATURE_SPEED_GAME_DATA = 'your Skyrim Special Edition Data directory'
dotnet test CreatureSpeedPatcher.sln --no-restore -m:1
```

With official masters, custom reference speeds should write 31 NPC overrides: 8 Frostbite Spiders, 13 Imbued Spiders, 1 Chaurus, 1 Reaper, 2 Hunters/Fledglings and 6 Frozen Chaurus. Eight original reference overrides are intentionally excluded (four audio, two Lis, two wounded quest spiders).

Before calling this runtime-verified, compare output against winning inputs in xEdit: only speed should differ, with templates/scripts/AI/inventory intact. Inspect actual modded winning race and template relationships, validate effective inherited speeds on newly spawned creatures, and playtest docile/pet encounters, Dragonborn thrown/friendly/cloaking/jumping spiders and Frozen activation/death sequences. Check NPC patcher ordering; whole-record copying preserves the winner, not changes already lost to an earlier conflict.

Future audited families can be added to `CreatureGroupCatalog` and `Settings`; the matching/write flow has no creature-specific branches.
