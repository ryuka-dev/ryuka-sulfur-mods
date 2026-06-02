# The Dragonblade

You found a katana with a single line of dragon-script carved into its blade.

You are not the legend it belonged to.  
But for a moment, a fragment of that power answers your hand.

## What it does

The Dragonblade is a katana-focused melee expansion for SULFUR.

Starting from `0.3.0`, The Dragonblade no longer owns the base toggle melee stance implementation.

The base stance system has been split into:

```text
Toggle Melee Stance
```

The Dragonblade now depends on Toggle Melee Stance and only adds Dragonblade-specific features on top of it.

It adds:

- Katana-only Dash Strike
- Dash Strike movement in the direction the player is looking
- Dash Strike damage using the current katana's own damage and damage type
- Bottom-right ability HUD with cooldown display
- Kill refresh for Dash Strike
- Enemy-kill healing
- Short post-dash hang time to avoid harsh immediate falling

Toggle Melee Stance provides:

- Toggleable melee stance
- Tap / hold melee key behavior
- Fire input as melee attack while melee stance is active
- Preserved Aim / Block behavior
- Compatibility handling for external weapon-switching mods

## Required dependency

The Dragonblade requires:

```text
Toggle Melee Stance 1.2.0 or newer
```

Thunderstore dependency:

```text
ryuka_labs-Toggle_Melee_Stance-1.2.0
```

## Public name and internal ID

The public mod name is:

```text
The Dragonblade
```

The internal BepInEx GUID is intentionally kept as:

```text
kumo.sulfur.melee_expansion
```

So the naming is:

```text
Public mod name: The Dragonblade
Internal GUID:   kumo.sulfur.melee_expansion
DLL name:        TheDragonblade.dll
```

The stance foundation is provided by:

```text
Public mod name: Toggle Melee Stance
Internal GUID:   kumo.sulfur.toggle_melee_stance
```

## Basic controls

While holding a katana-like melee weapon:

```text
Tap Melee key
→ Toggle melee stance on / off

Hold Melee key
→ Use the original melee behavior
→ Release to perform the original melee attack
→ Return to the previous weapon

Fire
→ Perform one melee attack while melee stance is active

Aim
→ Keep original aim / block behavior

Sprint
→ Dash Strike
```

The Dragonblade does not hardcode `F`, `LeftShift`, mouse buttons, or controller buttons.

It relies on Toggle Melee Stance and the game's own input actions, so it should respect key rebinding and controller input better than raw key checks.

## Implementation overview

This mod uses BepInEx and Harmony.

Starting from `0.3.0`, The Dragonblade is layered like this:

```text
Toggle Melee Stance
→ owns base melee stance patches
→ owns tap / hold melee input
→ owns Fire-to-melee conversion
→ owns external weapon-switch compatibility

The Dragonblade
→ reads stance state from Toggle Melee Stance public API
→ adds katana dash
→ adds dash damage
→ adds kill refresh
→ adds enemy-kill healing
→ adds dash HUD
```

## Main patched systems in The Dragonblade

The Dragonblade still patches:

```text
PerfectRandom.Sulfur.Core.Movement.ExtendedAdvancedWalkerController
CMF.Mover
PerfectRandom.Sulfur.Core.Units.Unit
```

Important patched methods include:

```text
ExtendedAdvancedWalkerController.UpdateSprinting()
CMF.Mover.SetVelocity(Vector3)
Unit.ReceiveDamage(float, DamageTypes, DamageSourceData, Hitmesh.Data, Vector3?)
```

The Dragonblade no longer patches these toggle melee methods directly:

```text
EquipmentManager.HandleMeleeInput(bool)
EquipmentManager.HandleAimInput(bool)
EquipmentManager.PullTrigger()
EquipmentManager.ReleaseTrigger()
Weapon.ReportMeleeDone()
Weapon.ChargeMelee(bool)
```

Those are owned by Toggle Melee Stance.

## Why the stance system was split out

The original Dragonblade included its own full toggle melee stance implementation.

That worked when Dragonblade was installed alone, but it made future maintenance worse because standalone Toggle Melee Stance and Dragonblade both needed the same fixes:

```text
tap / hold melee behavior
external weapon-switch compatibility
Fire-to-melee input handling
safe sheathe behavior
animator state cleanup
```

Keeping those systems duplicated increases the chance of:

- double-patching the same input methods
- fixing one mod but forgetting the other
- two mods fighting over the same internal melee state
- bugs where the player visually holds a gun but melee stance still blocks Fire input

The new architecture makes Toggle Melee Stance the shared base module.

## Toggle Melee Stance public API used by The Dragonblade

The Dragonblade reads stance state through:

```text
ToggleMeleeStance.Plugin
```

Used API:

```text
IsMeleeStanceActive(object equipmentManager)
IsAttackInProgress(object equipmentManager)
IsSheatheAfterAttack(object equipmentManager)
IsMeleeChargeActive(object equipmentManager)
```

The Dragonblade uses these checks to decide whether Dash Strike and the dash HUD should be active.

## Dash Strike input

While katana stance is active, the game is kept in sprint state and the Sprint input becomes Dash Strike.

The mod patches:

```text
ExtendedAdvancedWalkerController.UpdateSprinting()
```

When katana stance is active, it reads the game's own sprint action:

```text
sprintAction
```

If that action is performed, Dash Strike starts.

Important rule:

```text
Do not hardcode KeyCode.LeftShift.
```

Using the game's sprint action keeps the feature compatible with rebinding and controller input.

## Sprint state handling

While katana stance is active, the mod calls:

```text
ToggleSprint(true)
```

This keeps the player in sprint state while the katana is held.

Important lesson:

```text
Do not call ToggleSprint(false) when leaving katana stance.
```

When the mod no longer owns sprint behavior, it should return control to vanilla `UpdateSprinting()`.

Calling `ToggleSprint(false)` manually can override the player's own sprint setting or toggle-sprint preference.

## Dash movement

Dash movement is applied by patching:

```text
CMF.Mover.SetVelocity(Vector3)
```

During Dash Strike, the mod replaces the final velocity with:

```text
dashDirection * dashSpeed
```

This is better than directly changing the player transform because it stays inside the game's movement / collision pipeline.

## Dash damage

Dash Strike uses the current katana weapon as the damage source.

The mod reads:

```text
Weapon.GetDamage()
Weapon.GetDamageType()
```

Then applies damage through:

```text
Unit.ReceiveDamage(...)
```

This means Dash Strike uses the equipped katana's own stats instead of a fake fixed damage value.

The damage can still be scaled by config:

```ini
DashDamageMultiplier = 1.0
```

## Dash hit detection

During the dash, the mod checks the path between the previous dash position and the current dash position.

It uses overlap checks such as:

```text
Physics.OverlapCapsule(...)
```

This makes hit detection more reliable than checking only the player's current point each frame.

The mod can also prevent the same unit from being hit more than once per dash:

```ini
DashHitEachUnitOnce = true
```

## Post-dash hang

A raw dash can feel wrong in the air because the player starts falling immediately after the dash ends.

Instead of forcing the player into the real jump state, the mod adds a short post-dash hang:

```ini
PostHangDuration = 0.12
PostHangMaxDownwardSpeed = 0.5
```

During this short window, the mod only limits downward velocity.

It does not add upward force.

## Ability HUD

The mod draws a bottom-right ability HUD using IMGUI.

It shows:

- Dash icon
- Actual sprint binding when available
- Cooldown number
- Unavailable state
- Kill refresh feedback
- Heal feedback

The HUD appears while Toggle Melee Stance reports that melee stance is active and the current weapon is a katana-like melee weapon.

## Kill refresh

The mod refreshes Dash Strike cooldown when the player kills a valid target.

It patches the `DamageSourceData` overload of:

```text
Unit.ReceiveDamage(...)
```

The logic is:

```text
Prefix:
    target was alive
    target is eligible

Postfix:
    target is now dead
    damage source is player
    refresh dash cooldown
```

It only refreshes when the target actually dies.

## Enemy-kill healing

When the player kills a valid enemy NPC while katana stance is active, the player heals:

```ini
HealAmountOnEnemyKill = 5
```

The mod manually clamps healing to missing health:

```text
current health
max health
actual heal = min(configured heal, max - current)
```

HUD feedback uses the actual healed amount, not the configured amount.

## Config

Config file may use the internal plugin GUID:

```text
BepInEx/config/kumo.sulfur.melee_expansion.cfg
```

Main options:

```ini
[General]
EnableMod = true

[KatanaDash]
EnableKatanaDash = true
KatanaNameKeywords = Katana,Wakizashi,BiggerKatana
Cooldown = 5
Distance = 8
Duration = 0.22
HitRadius = 1.0
DamageMultiplier = 1.0
HitEachUnitOnce = true

RefreshCooldownOnPlayerKill = true
RefreshRequiresKatanaStance = true
RefreshCooldownOnNonNpcUnitKill = false

HealOnEnemyKill = true
HealAmountOnEnemyKill = 5

PostHangDuration = 0.12
PostHangMaxDownwardSpeed = 0.5
SuppressFallingAnimationDuringPostHang = true

[UI]
EnableDashHud = true
DashIconFileName = dash_icon.png
DashHudIconSize = 76
DashHudUseActualSprintBinding = true
DashHudFallbackKeyLabel = SHIFT
DashKillRefreshFeedbackDuration = 0.45
DashHealFeedbackDuration = 0.85

[Debug]
LogStateChanges = false
LogDash = false
```

Toggle melee behavior is configured in Toggle Melee Stance:

```text
BepInEx/config/kumo.sulfur.toggle_melee_stance.cfg
```

Recommended Toggle Melee Stance settings:

```ini
[Melee]
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.13

[Compatibility]
KeepMeleeStanceAfterExternalWeaponSwitch = false
```

## What it does not do

This mod does not:

- Implement the base toggle melee stance itself
- Add new weapon models
- Add new animations
- Replace the full melee combat system
- Change all melee weapons globally
- Change enemy AI
- Change loot tables
- Edit save data
- Edit original game files

It extends existing katana-style melee weapons using Toggle Melee Stance and the game's own input, movement, weapon, and damage systems.

## Compatibility

Potential conflicts:

- Mods that patch `ExtendedAdvancedWalkerController.UpdateSprinting()`
- Mods that patch `CMF.Mover.SetVelocity(Vector3)`
- Mods that patch `Unit.ReceiveDamage(...)`
- Other mods that implement katana dash behavior

Base melee stance conflicts should now be handled by Toggle Melee Stance instead of Dragonblade.

## Version notes

### v0.3.0

- Split the base toggle melee stance system out of The Dragonblade.
- Made Toggle Melee Stance a hard dependency.
- Removed duplicate melee input patches from The Dragonblade.
- Dragonblade now reads stance state from Toggle Melee Stance public API.
- Moved tap / hold melee behavior to Toggle Melee Stance.
- Moved external weapon-switch compatibility handling to Toggle Melee Stance.
- Reduced risk of double-patching melee input methods.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, Unity Input System, Toggle Melee Stance, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, copyrighted assets, or decompiled game source.
