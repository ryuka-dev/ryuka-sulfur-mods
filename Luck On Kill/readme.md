# Luck On Kill

## What it does

Luck On Kill gives the player Luck when an enemy dies.

The reward is based on the player's current:

```text
Stat_LuckGain
````

The implementation treats this as approximately one minute of vanilla Luck recovery, then applies the configured multiplier / bonus / override.

## Implementation overview

This mod uses BepInEx and Harmony.

It patches death logic for all concrete subclasses of:

```text
PerfectRandom.Sulfur.Core.Units.Unit
```

The main target is:

```text
Unit.Die()
```

It also patches:

```text
Unit.Spawn()
```

to reset duplicate reward tracking for object-pool reuse.

## Key discovered attributes

The implementation uses the discovered attribute IDs:

```text
Stat_LuckGain = 58
Status_Luck = 94
```

The reward is calculated from the player's current `Stat_LuckGain`.

Then the reward is applied to `Status_Luck`.

## Core flow

The reward flow is:

```text
Unit.Die() Prefix
→ check whether this unit should give a reward before death

Unit.Die() Postfix
→ if it was eligible before death, grant Luck reward
```

This prefix/postfix split matters because after death, the unit state may already be changed.

The mod checks eligibility before the game fully processes death, then grants the reward after the original death method runs.

## Reward calculation

Default behavior:

```ini
RewardMultiplier = 1.0
RewardFlatBonus = 0.0
RewardOverride = -1.0
```

Reward formula:

```text
reward = current Stat_LuckGain * RewardMultiplier + RewardFlatBonus
```

If `RewardOverride >= 0`, the override value is used instead.

Examples:

```ini
RewardMultiplier = 1.0
RewardFlatBonus = 0
RewardOverride = -1
```

Reward equals current `Stat_LuckGain`.

```ini
RewardMultiplier = 2.0
RewardFlatBonus = 0
RewardOverride = -1
```

Reward equals double current `Stat_LuckGain`.

```ini
RewardOverride = 5
```

Every valid kill gives exactly 5 Luck.

## Configuration

Config file:

```text
BepInEx/config/kumo.sulfur.luck_on_kill.cfg
```

Main options:

```ini
[General]
EnableMod = true

[Reward]
RewardMultiplier = 1.0
RewardFlatBonus = 0.0
RewardOverride = -1.0

[Compatibility]
ApplyThroughModifyStatus = true

[Filter]
RequireHostileToPlayer = true
RewardOnlyExperienceUnits = false
RewardCivilians = false
RewardBreakables = false

[Safety]
PreventDuplicateRewards = true

[Debug]
LogRewards = true
```

### ApplyThroughModifyStatus

Recommended:

```ini
ApplyThroughModifyStatus = true
```

When enabled, Luck On Kill applies rewards through:

```text
EntityStats.ModifyStatus(...)
```

This allows **Better Luck Control** to affect kill rewards too.

If disabled, the mod directly sets the Luck status value instead.

### RequireHostileToPlayer

Recommended:

```ini
RequireHostileToPlayer = true
```

Only hostile units grant Luck.

If hostility cannot be determined, the mod falls back to checking `ExperienceOnKill`.

### RewardOnlyExperienceUnits

Default:

```ini
RewardOnlyExperienceUnits = false
```

This is intentionally false.

Some hostile or special enemies may give 0 XP but still be real enemies.
Keeping this false allows those enemies to grant Luck if they pass the hostile filter.

### RewardCivilians

Default:

```ini
RewardCivilians = false
```

Civilian units do not grant Luck by default.

### RewardBreakables

Default:

```ini
RewardBreakables = false
```

Breakable objects do not grant Luck by default.

This prevents barrels, props, and destructible objects from becoming Luck sources.

### PreventDuplicateRewards

Recommended:

```ini
PreventDuplicateRewards = true
```

Prevents the same unit death from giving Luck more than once.

The mod also resets this state when a unit spawns, which helps with object-pool reuse.

## How player stats are found

The mod tries several ways to find the player stats:

```text
1. From the context unit's PlayerUnit reference
2. From GameManager.Instance.PlayerUnit
3. By scanning active Unit objects and finding the player unit
```

This fallback chain was added because death events may not always have a simple direct path to the player.

## Enemy filtering

Before giving Luck, the mod filters out:

* Already dead units
* The player
* Breakables, unless enabled
* Civilians, unless enabled
* Non-hostile units, if hostility filtering is enabled
* Units without XP, if `RewardOnlyExperienceUnits` is enabled

This avoids giving Luck from unintended sources.

## Pitfalls / lessons learned

### Do not reward after death without a prefix state

If eligibility is checked only after `Die()`, the unit may already be in a dead state.

The mod stores whether the unit was eligible in the Prefix, then uses that state in the Postfix.

### Avoid duplicate rewards

Deaths can sometimes be processed more than once, or units may be reused by object pooling.

The mod tracks rewarded unit instance IDs and clears that state on `Spawn()`.

### Do not rely only on ExperienceOnKill

Some valid hostile enemies may have `ExperienceOnKill = 0`.

If rewards only depended on XP, those enemies would not grant Luck.

That is why the default is:

```ini
RewardOnlyExperienceUnits = false
```

### Do not reward breakables by default

Breakable objects can also be Unit-like objects.

If not filtered, players could farm Luck from props.

That is why:

```ini
RewardBreakables = false
```

is the recommended default.

### Make Better Luck Control compatibility explicit

When `ApplyThroughModifyStatus = true`, the reward goes through the same status-change path that Better Luck Control patches.

This makes the two mods stack naturally:

```text
Luck On Kill creates a Luck reward
Better Luck Control can modify how that Luck status change is applied
```

## Difference from Better Luck Control

Luck On Kill adds a new source of Luck:

```text
enemy death → Luck reward
```

Better Luck Control changes how Luck recovery and consumption are applied:

```text
Status_Luck delta → modified delta
```

They are designed to work together, but they solve different problems.

## What it does not do

This mod does not:

* Change normal Luck recovery by itself
* Change Luck consumption by itself
* Change enemy stats
* Change loot tables
* Change player damage
* Edit save data
* Edit original game files

It only adds Luck reward on valid unit death.

## Compatibility

Potential conflicts:

* Other mods that patch `Unit.Die()`
* Other mods that patch `Unit.Spawn()`
* Other mods that directly replace the Luck system
* Mods that change unit hostility logic

The mod uses reflection and broad subclass patching so it can catch deaths from different Unit subclasses.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.