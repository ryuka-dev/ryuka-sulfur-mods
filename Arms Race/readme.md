# Arms Race

You accidentally brought habits from another world into SULFUR.  
Now you have to Arms Race your way out.

## Overview

**Arms Race** is a BepInEx mod for **SULFUR**.

It gives the player a generated random weapon during a run and replaces that weapon as the run progresses. The mod is inspired by the classic "Arms Race" style of gameplay: kill enemies, get a new weapon, and keep adapting.

The generated weapons are built using SULFUR's existing systems:

- Random vanilla weapon selection
- Random oils
- Random scrolls
- Random compatible attachments
- Rank adjustment
- Durability normalization
- Weapon-slot placement
- Runtime cleanup

This mod does **not** directly modify base `WeaponSO` assets.

## Main Behavior

By default:

- Entering a non-safe-zone level grants one generated random weapon.
- Killing eligible enemies can grant another generated random weapon.
- Kill reward has a cooldown.
- The debug key exists but is disabled by default.
- Old generated weapons are removed before a new generated weapon is created.
- Generated weapons are removed before level transitions.
- Generated weapons are removed before Amulet teleport.
- Dropping a generated weapon destroys it instead of creating a pickup.

Generated weapons are intended to be temporary run weapons, not permanent save-file items.

## Save Safety

The mod avoids adding custom persistent data to the save file.

Generated weapons are marked at runtime with a `RandomWeaponMarker` component. This marker is not intended to persist through save/load or scene reconstruction.

To avoid leaving mod-specific state in saves, the mod removes generated weapons during important transition points:

- Before level transitions
- Before `NextLevelTrigger.MakeTransition`
- Before Amulet teleport
- When another generated weapon is created
- When the player tries to drop a generated weapon

If the mod is removed, the game should continue to load normally. The mod does not require custom save data.

## Important Implementation Notes

### Generated weapons are runtime-only

The mod does not try to persist a custom tag inside `InventoryData` or `ItemAttributeCollectionData`.

That is intentional.

The goal is:

```text
Generated weapon exists during the run.
Generated weapon is cleaned before transition/save-sensitive moments.
Removing the mod should not break the original game.
````

### Weapon stats are not directly randomized

The mod does not directly randomize internal base weapon fields such as:

* Raw damage
* Fire rate
* Projectile speed
* Magazine size
* Reload time
* Base caliber

Instead, it randomizes through existing vanilla systems:

```text
Random vanilla weapon
+ random oils
+ random scrolls
+ random compatible attachments
+ rank adjustment
+ durability safety
```

This avoids mutating shared `WeaponSO` assets.

### Why old generated weapons are removed

Only one generated Arms Race weapon should exist at a time.

This prevents players from keeping many generated weapons by moving them into the backpack, dropping them, or carrying them through transitions.

## Configuration

The config file is generated at runtime:

```text
BepInEx/config/kumo.sulfur.arms_race.cfg
```

Main options:

```ini
[General]
EnableMod = true
RandomizeOnLevelStart = true
PreferredWeaponSlot = FirstAvailable
ForceSelectGeneratedWeapon = true
CleanupOldGeneratedWeapons = true

[Kill Reward]
EnableRandomWeaponOnKill = true
RandomWeaponOnKillCooldown = 1
KillRewardRequiresPlayableLevel = true
KillRewardRequireExperienceOnKill = true

[Random Upgrades]
EnableRandomOils = true
MinOilCount = 1
MaxOilCount = 5
EnableRandomScrolls = true
ScrollChance = 0.5
EnableRandomAttachments = true
AttachmentChance = 0.6
GrantRankForAppliedOils = true
RespectEnchantmentSlots = false

[Safety]
CleanupGeneratedWeaponsBeforeLevelTransition = true
DestroyGeneratedWeaponsOnDrop = true
FixLowDurability = true
MinimumDurabilityNormalized = 0.5

[Debug]
DebugKeyEnabled = false
DebugKey = K
```

## Build Notes

This mod is a BepInEx plugin.

Required references generally include:

```text
BepInEx/core/BepInEx.dll
BepInEx/core/0Harmony.dll
SULFUR_Data/Managed/Assembly-CSharp.dll
SULFUR_Data/Managed/UnityEngine.dll
SULFUR_Data/Managed/UnityEngine.CoreModule.dll
SULFUR_Data/Managed/Unity.InputSystem.dll
SULFUR_Data/Managed/UnityEngine.UI.dll
```

Depending on the local project setup, Visual Studio may also require additional Unity module references if new Unity types are used.

## Development Notes

Important patched or referenced game systems include:

* `InventoryItem.DropFromPlayer`
* `GameManager` level transition methods
* `NextLevelTrigger.MakeTransition`
* `AmuletHelper.DoneChanneling`
* `Unit.Die`
* `InventoryUI.SpawnItemInSlot`
* `ItemGrid`
* `PaperdollSlot`
* `EquipmentManager`
* `LootSettings`
* `LootTable`
* `InventoryItem.AddEnchantment`
* `InventoryItem.AddAttachment`

The mod uses Harmony patches carefully and falls back to warnings if optional transition patches fail.

## Source Code Policy

This folder contains only original mod source code and packaging text.

It does not include:

* SULFUR game files
* Unity assemblies
* BepInEx binaries
* Decompiled game source
* Paid assets

The code is shared for learning, reference, and transparency.

## License / Usage

Use this source code as a reference for SULFUR modding.

If you reuse large parts of the implementation, please credit the original repository.

## Changelog

### 1.0.0

* Initial release.
* Added random generated weapon on level start.
* Added random generated weapon reward on enemy kill.
* Added configurable kill reward cooldown.
* Added random oils with lightly weighted oil-count distribution.
* Added random scrolls.
* Added random compatible attachments.
* Added rank adjustment based on applied enchantments.
* Added durability normalization and minimum durability protection.
* Added runtime marker for generated weapons.
* Added cleanup before level transitions.
* Added cleanup for `NextLevelTrigger`.
* Added cleanup for Amulet teleport.
* Added drop prevention for generated weapons.
* Debug key spawning is available but disabled by default.
