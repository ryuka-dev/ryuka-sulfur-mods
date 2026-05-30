# Weapon Durability Loss Multiplier

## What it does

Weapon Durability Loss Multiplier changes weapon durability loss from shooting.

Default value:

```ini
WeaponDurabilityLossMultiplier = 0.5
````

Examples:

* `0` = no shooting durability loss
* `0.5` = half durability loss
* `1.0` = vanilla durability loss
* `2.0` = double durability loss
* `10.0` = maximum allowed multiplier

## Implementation overview

This mod uses BepInEx and Harmony.

It patches:

```text
PerfectRandom.Sulfur.Core.Items.InventoryItem.TakeDurabilityLossFromShoot()
```

with a Harmony Transpiler.

The transpiler finds the original call to:

```text
InventoryItem.ModifyDurability(float)
```

and injects this method immediately before it:

```text
ApplyWeaponDurabilityLossMultiplier(float durabilityChange)
```

So the final flow becomes:

```text
Vanilla weapon shooting durability loss calculation
→ vanilla DurabilityLossMultiplier is already included
→ mod applies WeaponDurabilityLossMultiplier
→ InventoryItem.ModifyDurability(final value)
```

## Why this patch point

The important design goal was:

```text
Apply this mod after the game has already calculated the final weapon durability loss.
```

The game’s shooting durability loss is not just a fixed number.

The original method includes:

* Base shooting durability loss
* Attachment-related calculation
* Enchantment durability cost
* The weapon/item `DurabilityLossMultiplier`

So the mod should not replace the whole calculation.

Instead, it modifies the final negative durability value right before `ModifyDurability(float)` receives it.

This means other vanilla durability modifiers, such as oil-related durability loss changes, are already included before this mod applies its multiplier.

## Why a Transpiler is used

A simple Prefix would run too early.

A simple Postfix would run too late, after durability has already changed.

The correct timing is:

```text
right before ModifyDurability(float)
```

That is why the mod uses a Transpiler.

The transpiler scans the IL instructions of `TakeDurabilityLossFromShoot()` and inserts the multiplier call before the original `ModifyDurability(float)` call.

## Important implementation detail

The transpiler preserves branch labels.

When inserting a new instruction before `ModifyDurability(float)`, labels from the original instruction are moved onto the inserted multiplier instruction.

This matters because if any branch jumps to the original call location, it should still run the multiplier first.

## Configuration

Config file:

```text
BepInEx/config/kumo.sulfur.weapon_durability_loss_multiplier.cfg
```

Main options:

```ini
[General]
EnableMod = true

[Weapon]
WeaponDurabilityLossMultiplier = 0.5

[Debug]
LogDurabilityChanges = false
```

### EnableMod

Enables or disables the mod.

```ini
EnableMod = true
```

### WeaponDurabilityLossMultiplier

Controls final weapon durability loss from shooting.

```ini
WeaponDurabilityLossMultiplier = 0.5
```

Recommended values:

```text
0.0 = no shooting durability loss
0.5 = half durability loss
1.0 = vanilla
2.0 = double durability loss
```

The value is clamped between:

```text
0.0 and 10.0
```

### LogDurabilityChanges

Logs adjusted durability values to the BepInEx log.

Recommended default:

```ini
LogDurabilityChanges = false
```

Enable this only for debugging.

## Pitfalls / lessons learned

### Do not patch ModifyDurability globally

`InventoryItem.ModifyDurability(float)` is used by more than weapon shooting.

If the mod patched `ModifyDurability(float)` globally, it could accidentally affect:

* Repairs
* Armor durability
* Other item durability changes
* Non-shooting durability loss

This mod only targets `TakeDurabilityLossFromShoot()` so it stays focused on weapon shooting durability loss.

### Do not multiply before vanilla DurabilityLossMultiplier

The game already has:

```text
InventoryItem.DurabilityLossMultiplier
```

That value may include item stats, oil effects, enchantments, or other durability modifiers.

The mod should apply after that vanilla multiplier, not before it.

That is why the injection happens before the final `ModifyDurability(float)` call.

### Only modify durability loss

The multiplier method checks:

```text
durabilityChange < 0
```

Positive durability changes are ignored.

This prevents the mod from changing repairs or durability restoration.

### Guard invalid config values

If the multiplier is `NaN` or infinity, the mod falls back to vanilla `1.0`.

The value is also clamped to avoid extreme values.

## What it affects

This mod affects:

* Weapon durability loss from shooting

## What it does not affect

This mod does not affect:

* Armor durability loss
* Weapon repair
* Item repair
* Max durability
* Weapon XP
* Weapon damage
* Enchantment logic itself
* Save data
* Original game files

## Compatibility

This mod should be compatible with most mods.

Potential conflicts:

* Other mods that transpile `InventoryItem.TakeDurabilityLossFromShoot()`
* Other mods that replace weapon durability loss logic
* Other mods that patch the same `ModifyDurability(float)` call inside `TakeDurabilityLossFromShoot()`

It should not conflict with armor durability mods because armor durability is handled separately.

## Difference from Armor Durability Loss Multiplier

Weapon Durability Loss Multiplier targets weapon shooting durability loss.

Armor Durability Loss Multiplier should target armor damage logic, such as:

```text
EquipmentManager.DamageArmor(float, bool)
```

These are separate systems and should be implemented separately.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.