# Better Luck Control

## What it does

Better Luck Control lets you control how Luck changes in SULFUR.

It can modify:

- Luck recovery
- Luck consumption
- Flat Luck recovery bonus
- Fixed Luck recovery override

This mod was originally developed internally as `Luck Tweaks`, but the public mod name is **Better Luck Control**.

## Implementation overview

This mod uses BepInEx and Harmony.

It patches:

```text
PerfectRandom.Sulfur.Core.Stats.EntityStats.ModifyStatus(...)
````

The patch checks whether the changed status is:

```text
Status_Luck
```

Internally, the implementation uses the discovered attribute IDs:

```text
Stat_LuckGain = 58
Status_Luck = 94
```

When `ModifyStatus(...)` receives a Luck status change, the mod modifies the delta before the original game method applies it.

## Core logic

The mod separates Luck changes into two directions:

```text
Positive Status_Luck change
→ Luck recovery

Negative Status_Luck change
→ Luck consumption
```

Positive changes are controlled by:

```ini
RecoveryMultiplier
RecoveryFlatBonus
RecoveryOverridePerMinute
```

Negative changes are controlled by:

```ini
ConsumptionMultiplier
```

## Configuration

Config file:

```text
BepInEx/config/ryukalabs.sulfur.lucktweaks.cfg
```

Depending on the final package name, users may still see the internal config file name from the plugin GUID.

Main options:

```ini
[Luck Recovery]
RecoveryMultiplier = 1
RecoveryFlatBonus = 0
RecoveryOverridePerMinute = -1

[Luck Consumption]
ConsumptionMultiplier = 1

[Debug]
VerboseLogging = true
```

### RecoveryMultiplier

Multiplies positive Luck recovery.

Examples:

```ini
RecoveryMultiplier = 1
```

Vanilla recovery.

```ini
RecoveryMultiplier = 2
```

Double Luck recovery.

### RecoveryFlatBonus

Adds a flat bonus to every positive Luck recovery tick after multiplier.

Example:

```ini
RecoveryFlatBonus = 0.5
```

### RecoveryOverridePerMinute

If this value is `>= 0`, it replaces each positive Luck recovery tick with a fixed value.

Default:

```ini
RecoveryOverridePerMinute = -1
```

`-1` means disabled.

### ConsumptionMultiplier

Multiplies negative Luck changes.

Examples:

```ini
ConsumptionMultiplier = 1
```

Vanilla Luck consumption.

```ini
ConsumptionMultiplier = 0.5
```

Half Luck consumption.

```ini
ConsumptionMultiplier = 0
```

No Luck consumption.

## Why this patch point

Luck changes eventually pass through `EntityStats.ModifyStatus(...)`.

Patching this method gives one central place to control both Luck recovery and Luck consumption.

This is better than trying to patch every individual system that may recover or spend Luck.

## Pitfalls / lessons learned

### Attribute names may not be enough

During development, the implementation needed to identify Luck status changes reliably.

The final code does not only compare strings.
It also supports enum values, numeric IDs, and nested `id` / `value` members.

This matters because the same attribute may appear in different forms depending on where the game calls `ModifyStatus(...)`.

### Avoid recursive modification

The patch uses an internal guard:

```text
IsTweakingLuckStatus
```

This prevents the mod from accidentally re-processing its own Luck change while modifying `Status_Luck`.

Without this guard, a status modification patch can easily create recursive behavior.

### Separate recovery from consumption

Positive and negative Luck deltas should not be treated the same.

A player may want:

```text
Faster recovery
but normal consumption
```

or:

```text
Normal recovery
but reduced consumption
```

So the mod handles positive and negative deltas separately.

### Clamp invalid consumption behavior

Negative consumption multipliers should not become negative.

If `ConsumptionMultiplier` is below zero, the mod treats it as zero.

This prevents negative consumption from turning into unintended Luck gain.

### Keep logging optional

Luck status may change frequently.

Verbose logging is useful for reverse engineering and testing, but it can spam the BepInEx log during normal gameplay.

## Compatibility

Better Luck Control is designed to work well with **Luck On Kill**.

Luck On Kill has an option:

```ini
ApplyThroughModifyStatus = true
```

When enabled, kill rewards go through `ModifyStatus(...)`, which allows Better Luck Control to affect those rewards too.

Potential conflicts:

* Other mods that patch `EntityStats.ModifyStatus(...)`
* Mods that directly set `Status_Luck` without using `ModifyStatus(...)`

## What it does not do

This mod does not:

* Add new Luck sources by itself
* Give Luck on kill by itself
* Change enemy behavior
* Change loot tables
* Edit save data
* Edit original game files

It only changes how `Status_Luck` deltas are applied.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.