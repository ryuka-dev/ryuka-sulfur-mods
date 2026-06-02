# Toggle Melee Stance

A shared melee stance module and quality-of-life mod for **SULFUR**.

It changes melee from hold-to-use into a toggle stance, while keeping the original hold melee behavior available through tap / hold input.

Starting from `1.2.0`, this mod is intended to be the shared prerequisite melee stance module for **The Dragonblade** and future melee-focused mods.

## What it does

Toggle Melee Stance provides the base stance system:

- Toggleable melee stance
- Tap / hold melee key behavior
- Fire input as melee attack while melee stance is active
- Preserved Aim / Block behavior
- Safe sheathe behavior
- Animator safe-state cleanup
- External weapon-switch compatibility
- Public API for other mods to read melee stance state

## What it does not do

This mod does not:

- Change melee damage
- Change weapon durability
- Change enemy AI
- Edit save data
- Replace or rebalance melee weapons
- Add dash attacks, thrust attacks, or new melee moves

Dragonblade-specific abilities such as Dash Strike, kill refresh, healing, and HUD are handled by **The Dragonblade**, not this mod.

## Public name and internal ID

The public mod name is:

```text
Toggle Melee Stance
```

The internal BepInEx GUID is:

```text
kumo.sulfur.toggle_melee_stance
```

So the naming is:

```text
Public mod name: Toggle Melee Stance
Internal GUID:   kumo.sulfur.toggle_melee_stance
DLL name:        ToggleMeleeStance.dll
```

## Basic controls

```text
Tap Melee key
→ Toggle melee stance on / off

Hold Melee key
→ Use the original melee behavior
→ Release to perform the original melee attack
→ Return to the previous weapon

Fire
→ Perform one melee attack while melee stance is toggled on

Aim / Alt Fire
→ Keep vanilla alternative melee stance / block behavior
```

The mod does not hardcode `F`, mouse buttons, or controller buttons.

It uses the game's own input actions, so it should respect key rebinding and controller input better than raw key checks.

## Why this mod exists

SULFUR's original melee input is hold-based.

That works for vanilla gameplay, but it becomes uncomfortable for builds or mods that want melee to behave more like a drawn weapon stance.

A simple toggle implementation can easily break because the vanilla melee key does several things:

```text
draw melee
hold melee
attack
block
sheathe
```

This mod separates those states and keeps them stable.

## Tap / hold melee key behavior

The melee key has two behaviors:

```text
Tap melee key
→ Toggle melee stance

Hold melee key
→ Pass through to the original melee behavior
→ Release to perform the original melee attack
→ Return to the previous weapon
```

This keeps the original game melee behavior available while still allowing toggle stance.

The tap / hold threshold is configurable:

```ini
[Melee]
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.13
```

Lower values make hold behavior activate faster.

Higher values make tap behavior easier.

## Toggle melee stance system

The state is stored per `EquipmentManager`.

Core state:

```text
IsToggled
AttackInProgress
SheatheAfterAttack
SuppressMeleeUntilReleased
NextChargeAttemptTime
```

Tap / hold state:

```text
PendingTapHold
LongPressPassThrough
WasMeleeHeldLastFrame
MeleePressStartTime
```

This allows the mod to distinguish between:

- Drawing melee
- Keeping it drawn
- Performing an attack
- Waiting for an attack to finish
- Sheathing after the attack
- Suppressing the same melee input until it is released
- Waiting to determine whether melee input is a tap or a hold
- Passing long melee input through to vanilla behavior

## Fire input as melee attack

When melee stance is toggled on, the normal Fire input is converted into one melee attack.

The mod calls the game's own method:

```text
EquipmentManager.UseBasicMelee()
```

This is important because the original game already handles melee animation events, hit timing, damage, and weapon state.

The mod does not create a fake attack system.

## Aim / block behavior

The original game has special melee behavior when melee and Aim / Alt Fire are held together.

The mod preserves this by maintaining the game's internal alternative melee state:

```text
alternativeMeleePressed
AlternativePressed animator bool
```

It reads the game's own `altFireAction` instead of checking raw mouse input.

## Attack then sheathe behavior

If the player presses melee while an attack is already in progress, the mod does not instantly sheathe.

Instead:

```text
AttackInProgress = true
Player presses melee to sheathe
→ SheatheAfterAttack = true
→ wait for Weapon.ReportMeleeDone()
→ then sheathe
```

This avoids:

- Double attacks
- Interrupted attack animations
- Stale melee state
- Incorrect weapon switching

## External weapon switch compatibility

This mod includes compatibility handling for mods that can change the player's current weapon externally.

One known case is a weapon-randomizing mod that switches the player's weapon after a kill.

Without compatibility handling, this can create a broken state:

```text
melee stance is active
→ another mod switches the player to a gun
→ the player visually holds the gun
→ melee stance still intercepts Fire input
→ the gun cannot shoot
```

The default fix is:

```text
melee stance is active
→ another mod switches the player to a gun
→ the player tries to fire
→ this mod detects that the current holdable is no longer melee
→ this mod exits melee stance
→ Fire input is passed through
→ the gun fires normally
```

This behavior is configurable:

```ini
[Compatibility]
KeepMeleeStanceAfterExternalWeaponSwitch = false
```

Recommended default:

```text
false
```

Important implementation rule:

```text
Do not clear melee stance immediately when the current holdable appears to be non-melee.
```

The check is delayed until the player actually tries to fire.

This avoids false state loss during normal weapon transitions.

## Animator flicker fix

Some melee weapons can briefly flash an attack animation on repeated draw.

The issue is caused by the melee weapon animator keeping a stale state after repeated draw / attack / sheathe cycles.

The solution is to cache only a safe Equip state.

A state is only considered safe if it contains:

```text
Equip
```

and does not contain:

```text
Slash
Attack
Fire
ADS
ToADS
Charge
Charged
```

Before sheathing, the mod resets volatile animator state:

```text
SetAlternativeState(0)
currentParries = 0
Charge = false
Sprinting = false
AlternativePressed = false
ResetTrigger("Parry")
```

Then it restores the cached safe Equip state:

```text
Animator.Play(cachedSafeEquipState, 0, 0f)
Animator.Update(0f)
```

This prevents the next draw from starting from a stale attack / idle / ADS state.

## Public API for other mods

The public API is exposed through:

```text
ToggleMeleeStance.Plugin
```

Available API:

```text
IsRuntimeReady
IsMeleeStanceActive(object equipmentManager)
IsAttackInProgress(object equipmentManager)
IsSheatheAfterAttack(object equipmentManager)
IsMeleeChargeActive(object equipmentManager)
GetCurrentHoldableForExternal(object equipmentManager)
IsCurrentHoldableMeleeForExternal(object equipmentManager)
```

These methods are intended for mods such as The Dragonblade to check melee stance state without patching the same input methods again.

## Dragonblade integration

Starting from The Dragonblade `0.3.0`, Dragonblade should depend on Toggle Melee Stance instead of duplicating the base stance system.

Layering:

```text
Toggle Melee Stance
→ owns base melee stance patches
→ owns tap / hold melee input
→ owns Fire-to-melee conversion
→ owns external weapon-switch compatibility
→ exposes stance state through public API

The Dragonblade
→ depends on Toggle Melee Stance
→ reads stance state through public API
→ adds katana dash
→ adds dash damage
→ adds kill refresh
→ adds enemy-kill healing
→ adds dash HUD
```

This reduces the risk of:

- double-patching the same input methods
- fixing one mod but forgetting the other
- two mods fighting over the same internal melee state
- bugs where the player visually holds a gun but melee stance still blocks Fire input

## Main patched systems

This mod uses BepInEx and Harmony.

Patched systems:

```text
PerfectRandom.Sulfur.Core.Items.EquipmentManager
PerfectRandom.Sulfur.Core.Weapons.Weapon
```

Important patched methods:

```text
EquipmentManager.HandleMeleeInput(bool)
EquipmentManager.HandleAimInput(bool)
EquipmentManager.PullTrigger()
EquipmentManager.ReleaseTrigger()
Weapon.ReportMeleeDone()
Weapon.ChargeMelee(bool)
```

## Config

Config file:

```text
BepInEx/config/kumo.sulfur.toggle_melee_stance.cfg
```

Main options:

```ini
[General]
EnableMod = true

[Melee]
FirePerformsMeleeAttack = true
ReChargeRetryInterval = 0.08
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.13

[Visual]
ResetMeleeAnimatorBeforeSheathe = true

[Compatibility]
DisableWhenMeleeExpansionDetected = false
KeepMeleeStanceAfterExternalWeaponSwitch = false

[Debug]
LogStateChanges = false
```

## Config details

### General

```ini
[General]
EnableMod = true
```

Enables or disables the mod.

### Melee

```ini
[Melee]
FirePerformsMeleeAttack = true
ReChargeRetryInterval = 0.08
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.13
```

`FirePerformsMeleeAttack`

When enabled, pressing Fire while melee stance is toggled on performs one melee attack.

`ReChargeRetryInterval`

Controls how often the mod retries re-entering melee stance after the game clears the melee charge state.

`EnableTapHoldMeleeKey`

When enabled, tapping the melee key toggles melee stance, while holding the melee key passes through to the original melee behavior.

`MeleeToggleTapThreshold`

Maximum press duration treated as a tap.

Default:

```ini
MeleeToggleTapThreshold = 0.13
```

Lower values make hold behavior activate faster.

Higher values make tap behavior easier.

### Visual

```ini
[Visual]
ResetMeleeAnimatorBeforeSheathe = true
```

Keeps repeated melee draws visually stable by resetting the melee Animator to a cached safe equip state before sheathing.

### Compatibility

```ini
[Compatibility]
DisableWhenMeleeExpansionDetected = false
KeepMeleeStanceAfterExternalWeaponSwitch = false
```

`DisableWhenMeleeExpansionDetected`

Deprecated.

Older versions used this to auto-disable Toggle Melee Stance when `kumo.sulfur.melee_expansion` was installed.

Starting from this version, The Dragonblade is expected to use Toggle Melee Stance as a prerequisite, so this option defaults to `false` and is no longer recommended.

`KeepMeleeStanceAfterExternalWeaponSwitch`

Controls behavior when another mod changes the current weapon while melee stance is toggled on.

```text
false
→ Recommended default.
→ If another mod switches your weapon while melee stance is active, this mod exits stance when you try to fire.
→ This allows guns to shoot normally after an external weapon switch.

true
→ Tries to keep melee stance even after another mod switches your weapon.
→ This may conflict with weapon-randomizing mods.
```

## Recommended default behavior

```text
Tap melee key:
    toggle melee stance

Hold melee key:
    use original melee behavior

Fire while toggled:
    perform melee attack

Aim / Alt Fire while toggled:
    keep vanilla alternative stance / block behavior

External weapon switch:
    exit melee stance when the player tries to fire
    allow the new weapon to fire normally
```

## Installation

### With a mod manager

Install through Thunderstore / r2modman if available.

### Manual installation

1. Install BepInEx for SULFUR.
2. Extract this package into your SULFUR game folder.
3. Make sure the DLL ends up here:

```text
SULFUR/BepInEx/plugins/ToggleMeleeStance.dll
```

4. Start the game once to generate the config file.

## Compatibility

This mod does not edit original game files.

Compatibility issues are most likely with mods that also patch:

- `EquipmentManager.HandleMeleeInput(bool)`
- `EquipmentManager.HandleAimInput(bool)`
- `EquipmentManager.PullTrigger()`
- `EquipmentManager.ReleaseTrigger()`
- `Weapon.ReportMeleeDone()`
- `Weapon.ChargeMelee(bool)`
- melee weapon Animator behavior

The Dragonblade `0.3.0+` is expected to use this mod as a prerequisite instead of duplicating the same melee stance patches.

## Pitfalls / lessons learned

### Do not read raw input

Use the game's `InputAction` fields.

Raw keyboard / mouse checks break rebinding and controller support.

### Do not let melee sheathe trigger another attack

The same melee input must not be passed to vanilla attack handling when it is meant to toggle off.

### Do not immediately sheathe during an attack

Wait for `Weapon.ReportMeleeDone()`.

### Do not clear melee stance too early

Do not automatically exit melee stance just because the current holdable briefly appears to be non-melee.

During weapon transitions, the current holdable can temporarily look inconsistent.

External weapon-switch compatibility should only exit stance when the player actually tries to fire.

### Do not make tap / hold depend only on release events

Some input paths can make release-frame detection unreliable.

The safer approach is to track held state across frames and compare:

```text
held this frame
held last frame
press start time
```

### Do not hide the weapon model to fix animation flicker

That causes worse visual artifacts.

Cache and restore a safe Equip animation state instead.

## What it should not own

Toggle Melee Stance should not own Dragonblade-specific systems such as:

- Katana-only dash
- Dash damage
- Kill refresh
- Enemy-kill healing
- Dash HUD
- Dash icon loading
- Post-dash hang

Those belong in The Dragonblade.

## Uninstallation

Remove the DLL from:

```text
BepInEx/plugins/
```

Optional: remove the config file:

```text
BepInEx/config/kumo.sulfur.toggle_melee_stance.cfg
```

## Changelog

### 1.2.0

- Changed Toggle Melee Stance into the shared stance foundation for Dragonblade and future melee mods.
- Added public API for other mods to read melee stance state.
- Changed `DisableWhenMeleeExpansionDetected` default to `false` because Dragonblade now depends on this mod instead of replacing it.
- Kept tap / hold melee behavior.
- Kept external weapon-switch safety handling.
- Changed default `MeleeToggleTapThreshold` to `0.13` for faster hold-melee passthrough.

### 1.1.0

- Added tap / hold melee key behavior.
- Tap melee key to toggle melee stance.
- Hold melee key to use the original melee behavior.
- Added external weapon-switch compatibility.
- Added config option to keep or exit melee stance after another mod switches weapons.

### 1.0.0

- Initial release.
- Added toggle melee stance.
- Added Fire action melee attack while toggled.
- Preserved vanilla Aim / alternative stance / block behavior.
- Added safe sheathe behavior so pressing melee again does not trigger an extra attack.
- Added melee Animator safe-state reset to prevent repeated draw animation flashes.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, Unity Input System, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, copyrighted assets, or decompiled game source.
