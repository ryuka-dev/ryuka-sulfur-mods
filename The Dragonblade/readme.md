# The Dragonblade

You found a katana with a single line of dragon-script carved into its blade.

You are not the legend it belonged to.  
But for a moment, a fragment of that power answers your hand.

## What it does

The Dragonblade is a katana-focused melee expansion for SULFUR.

It adds:

- Toggleable katana stance
- Tap / hold melee key behavior
- Tap melee key to toggle katana stance
- Hold melee key to use the original melee behavior
- Fire input as melee attack while the katana is drawn
- Preserved Aim / Block behavior
- Sprint key as Dash Strike while holding a katana
- Dash Strike movement in the direction the player is looking
- Dash Strike damage using the current katana's own damage and damage type
- Bottom-right ability HUD with cooldown display
- Kill refresh for Dash Strike
- Enemy-kill healing
- Short post-dash hang time to avoid harsh immediate falling
- Compatibility handling for external weapon-switching mods
- Configurable behavior when another mod switches your weapon after a kill

## Public name and internal ID

The public mod name is:

```text
The Dragonblade
```

The internal BepInEx GUID is intentionally kept as:

```text
kumo.sulfur.melee_expansion
```

This is because earlier standalone melee mods may detect this GUID and disable themselves to avoid double-patching the same melee input logic.

So the naming is:

```text
Public mod name: The Dragonblade
Internal GUID:   kumo.sulfur.melee_expansion
DLL name:        TheDragonblade.dll
```

Depending on the build setup, the generated config file may still use the internal GUID.

## Basic controls

While holding a katana-like melee weapon:

```text
Tap Melee key
→ Toggle katana stance on / off

Hold Melee key
→ Use the original melee behavior
→ Release to perform the original melee attack
→ Return to the previous weapon

Fire
→ Perform one melee attack while katana stance is active

Aim
→ Keep original aim / block behavior

Sprint
→ Dash Strike
```

The mod does not hardcode `F`, `LeftShift`, mouse buttons, or controller buttons.

It uses the game's own input actions, so it should respect key rebinding and controller input better than raw key checks.

## Implementation overview

This mod uses BepInEx and Harmony.

It extends the standalone Toggle Melee Stance concept and adds katana-specific movement, damage, HUD, kill reward logic, tap / hold melee input, and compatibility handling for external weapon-switching mods.

Main patched systems:

```text
PerfectRandom.Sulfur.Core.Items.EquipmentManager
PerfectRandom.Sulfur.Core.Weapons.Weapon
PerfectRandom.Sulfur.Core.Movement.ExtendedAdvancedWalkerController
CMF.Mover
PerfectRandom.Sulfur.Core.Units.Unit
```

Important patched methods include:

```text
EquipmentManager.HandleMeleeInput(bool)
EquipmentManager.HandleAimInput(bool)
EquipmentManager.PullTrigger()
EquipmentManager.ReleaseTrigger()
Weapon.ReportMeleeDone()
Weapon.ChargeMelee(bool)

ExtendedAdvancedWalkerController.UpdateSprinting()
CMF.Mover.SetVelocity(Vector3)

Unit.ReceiveDamage(float, DamageTypes, DamageSourceData, Hitmesh.Data, Vector3?)
```

## Toggle melee stance system

The Dragonblade includes the full toggle melee stance behavior.

The state is stored per `EquipmentManager`:

```text
IsToggled
AttackInProgress
SheatheAfterAttack
SuppressMeleeUntilReleased
NextChargeAttemptTime

PendingTapHold
LongPressPassThrough
WasMeleeHeldLastFrame
MeleePressStartTime
```

This allows the mod to distinguish between:

* Drawing the katana
* Keeping it drawn
* Performing a melee attack
* Waiting for an attack to finish
* Sheathing after the attack
* Suppressing the same melee key press until it is released
* Waiting to determine whether the melee input is a tap or a hold
* Passing long melee input through to the original game behavior

## Tap / hold melee key behavior

The melee key has two behaviors:

```text
Tap melee key
→ Toggle Dragonblade katana stance

Hold melee key
→ Pass through to the original melee behavior
→ Release to perform the original melee attack
→ Return to the previous weapon
```

This keeps the original game melee behavior available while still allowing Dragonblade to use a toggle stance.

The tap / hold threshold is configurable:

```ini
[ToggleMelee]
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.22
```

Lower values make hold behavior activate faster.

Higher values make tap behavior easier.

## Why the melee input is handled this way

A simple toggle implementation can easily break because the vanilla melee key does multiple things.

The same input can mean:

```text
draw melee
hold melee
attack
block
sheathe
```

The mod must prevent one physical input from being interpreted twice.

For example, without special handling:

```text
Press melee to draw
Press melee again to sheathe
→ vanilla logic may start another melee attack before switching back
```

The fix is:

```text
When toggled:
    intercept HandleMeleeInput()
    decide toggle on/off manually
    block vanilla handling when needed
    suppress melee input until released after manual sheathe

When not toggled:
    wait briefly to determine tap vs hold
    tap toggles Dragonblade stance
    hold passes through to vanilla melee behavior
```

## Fire input as melee attack

When katana stance is toggled on, the normal Fire input is converted into one melee attack.

The mod calls the game's own method:

```text
EquipmentManager.UseBasicMelee()
```

This is important because the original game already handles melee animation events, hit timing, damage, and weapon state.

The mod does not create a separate fake attack system for normal melee attacks.

## External weapon switch compatibility

The Dragonblade includes compatibility handling for mods that can change the player's current weapon externally.

One known case is a weapon-randomizing mod that switches the player's weapon after a kill.

Without compatibility handling, this can create a broken state:

```text
Dragonblade stance is active
→ another mod switches the player to a gun
→ the player visually holds the gun
→ Dragonblade still thinks katana stance is active
→ Fire input is still intercepted as melee input
→ the gun cannot shoot
```

The default fix is:

```text
Dragonblade stance is active
→ another mod switches the player to a gun
→ the player tries to fire
→ Dragonblade detects that the current holdable is no longer melee
→ Dragonblade exits katana stance
→ the fire input is passed through
→ the gun fires normally
```

This behavior is configurable:

```ini
[Compatibility]
KeepKatanaStanceAfterExternalWeaponSwitch = false
```

Recommended default:

```text
false
```

This means Dragonblade exits stance when the player tries to fire after an external weapon switch.

Setting it to `true` makes Dragonblade try to keep katana stance even after another mod switches the current weapon, but this may conflict with weapon-randomizing mods.

Important implementation rule:

```text
Do not clear Dragonblade stance immediately when the current holdable appears to be non-melee.
```

The check is delayed until the player actually tries to fire.

This avoids false state loss where the dash HUD disappears or Dragonblade stance is cleared too early during normal melee transitions.

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

* Double attacks
* Interrupted attack animations
* Stale melee state
* Incorrect weapon switching

## Aim / block behavior

The original game has special melee behavior when the melee key and aim input are held together.

The mod preserves this by maintaining the game's internal alternative melee state:

```text
alternativeMeleePressed
AlternativePressed animator bool
```

It reads the game's own `altFireAction` instead of checking raw mouse input.

## Animator flicker fix

One of the hardest problems was the katana draw animation flicker.

Observed issue:

```text
Normal weapon
→ press melee
→ first frame shows stale katana pose
→ draw animation starts after that
```

The issue was caused by the melee weapon animator keeping a stale state after repeated draw / attack / sheathe cycles.

## Failed approaches that were rejected

### 1. Force ChargeMelee immediately

Forcing `ChargeMelee(true)` too early can fail or fight the game's own weapon selection timing.

In early debugging, immediate forced charge could throw a null reference during the weapon transition timing window.

### 2. Hide the weapon model temporarily

Hiding the melee model during draw looked worse.

It could cause:

* The previous gun to remain visible briefly
* The katana to appear late
* More obvious visual popping

This was rejected.

### 3. Cache any current animation state

Caching the wrong state is dangerous.

States like these are not safe to use as a future draw start:

```text
Slash
Attack
Fire
ADS
ToADS
Charge
Charged
```

Restoring one of these can make the next draw look worse.

## Final animator solution

The final solution is to cache only a safe Equip state.

When `Weapon.ChargeMelee(true)` runs, the mod checks the current animation clip name.

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

## Katana detection

The mod detects katana-like weapons by keyword.

Default keywords:

```ini
KatanaNameKeywords = Katana,Wakizashi,BiggerKatana
```

The search text is built from several sources:

* Weapon object string
* Component name
* GameObject name
* SourceName
* weaponDefinition

This makes the detection more robust than checking only one object name.

## Dash Strike input

While the katana is drawn, the game is kept in sprint state and the Sprint input becomes Dash Strike.

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

The dash direction is based on where the player is looking.

The mod first tries:

```text
Player.DirectionLooking(false)
```

and falls back to camera forward direction if needed.

## Why SetVelocity is used

Direct transform movement is risky.

It can cause:

* Collision bypass
* Wall clipping
* Rigidbody / controller disagreement
* Bad interaction with the game's mover system

By injecting final velocity through the mover, the dash behaves more like a normal movement action.

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

Vanilla jumping has its own short "jumping" state and does not immediately fall at full speed.

Instead of forcing the player into the real jump state, the mod adds a short post-dash hang:

```ini
PostHangDuration = 0.12
PostHangMaxDownwardSpeed = 0.5
```

During this short window, the mod only limits downward velocity.

It does not add upward force.

This avoids turning dash into a fake jump while still making aerial dash feel smoother.

## Falling animation during post-dash hang

The mod can temporarily suppress the falling animation during post-dash hang:

```ini
SuppressFallingAnimationDuringPostHang = true
```

It does this by saving the old `fallingEnabled` value, setting it false during hang, then restoring it afterward.

Important lesson:

```text
Always restore the original value.
```

Do not assume the default should be true or false.

## Ability HUD

The mod draws a bottom-right ability HUD using IMGUI.

It shows:

* Dash icon
* Actual sprint binding when available
* Cooldown number
* Unavailable state
* Kill refresh feedback
* Heal feedback

The HUD appears while the player is in katana stance.

Attack state and HUD state are intentionally separated:

```text
Dash can be temporarily unavailable during attack
but the HUD should not disappear
```

This prevents the icon from vanishing every time the player swings the katana.

It also makes state-loss bugs easier to detect during testing:

```text
If the katana stance is active, the HUD should stay visible.
If the HUD disappears unexpectedly, stance state was probably cleared.
```

## HUD icon loading

The ability icon is loaded from an external PNG file:

```text
dash_icon.png
```

Recommended install structure:

```text
BepInEx/
└─plugins/
  └─TheDragonblade/
    ├─TheDragonblade.dll
    └─dash_icon.png
```

The icon is not embedded into the DLL.

This makes packaging and replacing the icon easier.

## Image loading pitfall

Do not directly reference `UnityEngine.ImageConversionModule.dll` if it causes a `netstandard2.1` vs `netstandard2.0` build conflict.

The mod uses reflection to call:

```text
UnityEngine.ImageConversion.LoadImage(Texture2D, byte[], bool)
```

This avoids a hard compile-time dependency on `UnityEngine.ImageConversionModule`.

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

This avoids refreshing on hit.

It only refreshes when the target actually dies.

## Why kill refresh is based on ReceiveDamage

Checking only dash hits would miss kills from:

* Normal katana swings
* Alternative melee behavior
* Delayed player damage
* Other player-caused damage while in katana stance

Using the damage/death transition is more general.

## Enemy / obstacle filtering

By default:

```ini
RefreshCooldownOnNonNpcUnitKill = false
```

This means breakable objects and non-NPC Units do not refresh cooldown.

If the user enables it, non-NPC units can refresh cooldown.

However, healing is stricter:

```text
Healing always requires an NPC/enemy target.
Breakable objects never heal the player.
```

This prevents players from farming health from props.

## Enemy-kill healing

When the player kills a valid enemy NPC while in katana stance, the player heals:

```ini
HealAmountOnEnemyKill = 5
```

The mod manually clamps healing to missing health:

```text
current health
max health
actual heal = min(configured heal, max - current)
```

This was necessary because simply calling `ModifyStatus(Status_CurrentHealth, amount, false)` could allow health to exceed the player's maximum in some cases.

HUD feedback uses the actual healed amount, not the configured amount.

Examples:

```text
95 / 100 HP + kill
→ +5 HP

98 / 100 HP + kill
→ +2 HP

100 / 100 HP + kill
→ no heal text
```

## Config

Config file may use the internal plugin GUID:

```text
BepInEx/config/kumo.sulfur.melee_expansion.cfg
```

If you updated from an older version and the new options do not appear, delete the old config file once:

```text
BepInEx/config/kumo.sulfur.melee_expansion.cfg
```

The config will be regenerated the next time the game starts.

Main options:

```ini
[General]
EnableMod = true

[ToggleMelee]
FirePerformsMeleeAttack = true
ReChargeRetryInterval = 0.08
ResetMeleeAnimatorBeforeSheathe = true
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.22

[Compatibility]
KeepKatanaStanceAfterExternalWeaponSwitch = false

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

## Config details

### Toggle melee

```ini
[ToggleMelee]
EnableTapHoldMeleeKey = true
MeleeToggleTapThreshold = 0.22
```

```text
EnableTapHoldMeleeKey = true
→ Tap the melee key to toggle Dragonblade stance.
→ Hold the melee key to use the original melee behavior.

MeleeToggleTapThreshold
→ Maximum press duration treated as a tap.
→ Lower values make hold behavior activate faster.
→ Higher values make tap behavior easier.
```

### External weapon switch behavior

```ini
[Compatibility]
KeepKatanaStanceAfterExternalWeaponSwitch = false
```

```text
false
→ Recommended default.
→ If another mod switches your weapon while Dragonblade stance is active, Dragonblade exits stance when you try to fire.
→ This allows guns to shoot normally after an external weapon switch.

true
→ Dragonblade tries to keep katana stance even after another mod switches your weapon.
→ This may conflict with weapon-randomizing mods.
```

## Recommended default behavior

Recommended public defaults:

```text
Tap melee key:
    toggle Dragonblade stance

Hold melee key:
    use original melee behavior

Kill enemy:
    refresh Dash Strike
    heal 5 HP, clamped to max health

Break object:
    no cooldown refresh
    no healing

External weapon switch:
    exit Dragonblade stance when the player tries to fire
    allow the new weapon to fire normally

Enable non-NPC refresh manually:
    break object may refresh cooldown
    still no healing
```

## Pitfalls / lessons learned

### Do not read raw input

Use the game's `InputAction` fields.

Raw keyboard / mouse checks break rebinding and controller support.

### Do not let melee sheathe trigger another attack

The same melee input must not be passed to vanilla attack handling when it is meant to toggle off.

### Do not immediately sheathe during an attack

Wait for `Weapon.ReportMeleeDone()`.

### Do not clear Dragonblade stance too early

Do not automatically exit Dragonblade stance just because the current holdable briefly appears to be non-melee.

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

### Do not force the jump state for dash hang

The game's jump state does more than delay falling.

Dash only needs a short downward-velocity cap, not a fake jump.

### Do not directly move the transform for dash

Use the mover velocity pipeline.

### Do not refresh cooldown on hit

Refresh on confirmed kill, not damage application.

### Do not heal from breakable objects

Even if non-NPC cooldown refresh is allowed, healing should stay enemy-only.

### Do not assume ModifyStatus clamps HP

Read current and max HP, calculate actual heal, then apply only the missing amount.

### Do not let HUD visibility depend on "dash can be used right now"

HUD display and dash availability should be separate.

The icon should stay visible during melee attacks even if dash is temporarily unavailable.

## What it does not do

This mod does not:

* Add new weapon models
* Add new animations
* Replace the full melee combat system
* Change all melee weapons globally
* Change enemy AI
* Change loot tables
* Edit save data
* Edit original game files

It extends existing katana-style melee weapons using the game's own input, movement, weapon, and damage systems.

## Compatibility

Potential conflicts:

* Mods that patch `EquipmentManager.HandleMeleeInput(bool)`
* Mods that patch `EquipmentManager.PullTrigger()`
* Mods that patch `Weapon.ReportMeleeDone()`
* Mods that patch `ExtendedAdvancedWalkerController.UpdateSprinting()`
* Mods that patch `CMF.Mover.SetVelocity(Vector3)`
* Mods that patch `Unit.ReceiveDamage(...)`
* Other mods that implement toggle melee stance or katana dash behavior
* Mods that change the player's current weapon after kills

Do not install the standalone Toggle Melee Stance mod together with The Dragonblade unless that standalone mod is configured to disable itself.

## Compatibility with Random Weapon Per Level

The Dragonblade includes compatibility handling for weapon-randomizing mods such as Random Weapon Per Level.

Default behavior:

```text
Kill enemy
→ Another mod switches your weapon
→ You try to fire
→ Dragonblade exits katana stance
→ The gun fires normally
```

This prevents a broken state where the player appears to be holding a gun, but Dragonblade still intercepts the fire input as melee input.

The behavior can be changed with:

```ini
[Compatibility]
KeepKatanaStanceAfterExternalWeaponSwitch = false
```

Keeping this option set to `false` is recommended when using weapon-randomizing mods.

## Version notes

### v0.2.1

- Added tap / hold melee key behavior.
- Added compatibility handling for external weapon-switching mods.
- Added config option to keep or exit katana stance after another mod switches weapons.
- Fixed an issue where killing an enemy with Random Weapon Per Level installed could leave the player visually holding a gun while Dragonblade still blocked firing input.
- Changed external weapon-switch detection so Dragonblade only exits stance when the player actually tries to fire.
- Fixed unintended Dragonblade state loss caused by overly aggressive non-melee holdable checks.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, Unity Input System, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, copyrighted assets, or decompiled game source.