# Weapon Double XP

## What it does

Weapon Double XP doubles weapon XP gain.

It is the simple fixed-multiplier version of Weapon XP Multiplier.

There is no config file for the multiplier:

```text
XP gain is always multiplied by 2
````

## Implementation overview

This mod uses BepInEx and Harmony.

It patches:

```text
PerfectRandom.Sulfur.Core.Weapons.Weapon.AddExperience(float experienceIncrease)
```

with a Harmony Prefix.

The core logic is intentionally minimal:

```csharp
private static void DoubleWeaponXpPrefix(ref float __0)
{
    if (__0 <= 0f)
        return;

    __0 *= 2f;
}
```

The mod only changes positive XP gain.

## Why this patch point

The mod patches `Weapon.AddExperience(float)` instead of `InventoryItem.AddExperience(...)`.

This was an important implementation decision.

The game’s `Weapon.AddExperience(float)` method already filters the weapon XP flow before passing XP to the item system.

The original weapon-side logic returns early for:

* Throwable weapons
* Melee weapons

Then it forwards valid XP to the inventory item.

So patching `Weapon.AddExperience(float)` keeps this mod focused on normal weapon XP gain.

## Difference from Weapon XP Multiplier

Weapon Double XP:

* Fixed 2x multiplier
* No multiplier config
* Very small implementation
* Good for users who only want simple double XP

Weapon XP Multiplier:

* Configurable multiplier
* Supports 0x to 100x
* Supports decimal values
* Includes optional logging
* Better for users who want control

Recommended public guidance:

```text
Use Weapon Double XP if you only want simple 2x XP.
Use Weapon XP Multiplier if you want configurable XP scaling.
```

## Pitfalls / lessons learned

### Do not install this together with Weapon XP Multiplier unless stacking is intended

Both mods patch the same method:

```text
Weapon.AddExperience(float)
```

If both are installed, the XP value may be multiplied twice.

Example:

```text
Weapon Double XP = 2x
Weapon XP Multiplier set to 2x
Total result may become 4x
```

### Do not patch lower-level item XP for this feature

Patching `InventoryItem.AddExperience(...)` could affect systems beyond normal weapon XP.

The safer patch point is `Weapon.AddExperience(float)`.

### Keep this version simple

This mod exists as the lightweight version.

The configurable version should stay in `Weapon XP Multiplier`, not here.

## Configuration

This mod does not provide multiplier configuration.

It only has the plugin load behavior from BepInEx.

To change the multiplier, use **Weapon XP Multiplier** instead.

## What it does not do

This mod does not:

* Change weapon damage
* Change player XP
* Change item drops
* Change weapon level requirements
* Edit save files
* Edit original game files

It only doubles the XP value passed into the original weapon XP method.

## Compatibility

This mod should be compatible with most mods.

Potential conflict:

* Any other mod that patches `Weapon.AddExperience(float)`

Do not use it together with another weapon XP multiplier mod unless multiplier stacking is desired.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.