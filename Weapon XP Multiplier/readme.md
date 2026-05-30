# Weapon XP Multiplier

## What it does

Weapon XP Multiplier changes weapon XP gain by a configurable multiplier.

Default value:

```ini
XpMultiplier = 2.0
````

Examples:

* `0` = disable weapon XP gain
* `0.5` = half XP
* `1.0` = vanilla XP
* `2.0` = double XP
* `10.2` = 10.2x XP
* `100` = maximum allowed multiplier

## Implementation overview

This mod uses BepInEx and Harmony.

It patches:

```text
PerfectRandom.Sulfur.Core.Weapons.Weapon.AddExperience(float experienceIncrease)
```

with a Harmony Prefix:

```text
ref float __0
__0 *= multiplier
```

The mod changes the XP value before the original game method applies it.

## Why this patch point

The important reverse engineering result was that weapon XP should be changed at the `Weapon.AddExperience(float)` level, not at the lower `InventoryItem.AddExperience(...)` level.

The game-side weapon XP method already filters out some cases before forwarding XP to the inventory item.

The relevant original logic is effectively:

```csharp
public void AddExperience(float experienceIncrease)
{
    if (this.IsThrowable)
    {
        return;
    }

    if (this.weaponDefinition.IsMelee)
    {
        return;
    }

    base.inventoryItem.AddExperience(experienceIncrease);
}
```

Because of this, patching `Weapon.AddExperience(float)` keeps the mod focused on normal weapon XP gain.

## Pitfalls / lessons learned

### Do not patch InventoryItem.AddExperience for this mod

`InventoryItem.AddExperience(...)` is lower level and may be used by more than just normal weapons.

Patching it would risk affecting other upgradeable items or systems.

For this mod, `Weapon.AddExperience(float)` is cleaner because the game has already decided that this XP belongs to a valid weapon XP flow.

### Do not hardcode only 2x when a configurable version exists

The first simple version was a fixed double XP mod.

This configurable version is more flexible and supports:

* XP reduction
* XP disabling
* Decimal multipliers
* Large multipliers up to 100x

### Guard invalid multiplier values

The code protects against invalid config values such as `NaN` or infinity.

If the multiplier is invalid, it falls back to vanilla `1.0`.

The value is also clamped between:

```text
0.0 and 100.0
```

## Configuration

Config file:

```text
BepInEx/config/kumo.sulfur.weapon_xp_multiplier.cfg
```

Main options:

```ini
[General]
EnableMod = true
XpMultiplier = 2.0

[Debug]
LogXpChanges = false
```

### EnableMod

Enables or disables the mod.

```ini
EnableMod = true
```

### XpMultiplier

Controls the XP multiplier.

```ini
XpMultiplier = 2.0
```

Recommended values:

```text
0.5 = slower weapon progression
1.0 = vanilla
2.0 = double XP
5.0 = fast progression
```

### LogXpChanges

Logs XP changes to BepInEx log.

This should stay disabled during normal gameplay.

```ini
LogXpChanges = false
```

## What it does not do

This mod does not:

* Change weapon damage
* Change player XP
* Change item drop rates
* Change weapon level requirements
* Edit save files
* Edit original game files

It only modifies the XP amount passed into the original weapon XP method.

## Compatibility

This mod should be compatible with most mods.

Potential conflict:

* Another mod that also patches `Weapon.AddExperience(float)`

If another XP mod is installed at the same time, multipliers may stack.

For example:

```text
Weapon Double XP + Weapon XP Multiplier 2x = likely 4x total
```

Use only one weapon XP mod at a time unless stacking is intentional.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.