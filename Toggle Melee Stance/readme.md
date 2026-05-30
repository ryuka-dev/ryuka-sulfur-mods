# Toggle Melee Stance

## What it does

Toggle Melee Stance changes SULFUR's melee behavior from "hold melee key" to "toggle melee stance".

In vanilla behavior, the player needs to hold the melee key to keep a melee weapon drawn.

With this mod:

- Press melee once to draw and keep the melee weapon out.
- Press melee again to sheathe it.
- While the melee weapon is drawn, press normal Fire to perform one melee attack.
- Aim / alternative melee behavior is preserved.
- If you sheathe during an attack, the weapon returns after the current attack finishes instead of starting another attack.

The mod is designed to use the game's own input actions and melee systems instead of checking raw mouse or keyboard buttons.

## Implementation overview

This mod uses BepInEx and Harmony.

It patches several methods from:

```text
PerfectRandom.Sulfur.Core.Items.EquipmentManager
PerfectRandom.Sulfur.Core.Weapons.Weapon
````

Main patched methods:

```text
EquipmentManager.HandleMeleeInput(bool)
EquipmentManager.HandleAimInput(bool)
EquipmentManager.PullTrigger()
EquipmentManager.ReleaseTrigger()
Weapon.ReportMeleeDone()
Weapon.ChargeMelee(bool)
```

The mod keeps a per-`EquipmentManager` state object with:

```text
IsToggled
AttackInProgress
SheatheAfterAttack
SuppressMeleeUntilReleased
NextChargeAttemptTime
```

It also keeps a per-weapon cached safe animator state to prevent animation flicker after repeated draw / sheathe cycles.

## Key game methods / fields used

Important methods:

```text
EquipmentManager.HandleMeleeInput(bool)
EquipmentManager.HandleAimInput(bool)
EquipmentManager.ChargeBasicMelee()
EquipmentManager.UseBasicMelee()
EquipmentManager.OnMeleeDone()
Weapon.ReportMeleeDone()
Weapon.IsMeleeCharging()
Weapon.ChargeMelee(bool)
Weapon.SetAlternativeState(int)
```

Important fields / properties:

```text
EquipmentManager.currentHoldable
EquipmentManager.isInMeleeCharge
EquipmentManager.meleePressed
EquipmentManager.alternativeMeleePressed
EquipmentManager.AimingInputHeld
EquipmentManager.meleeInputCooldown
EquipmentManager.altFireAction
EquipmentManager.meleeFireAction
EquipmentManager.meleeFireActionAlternative
Weapon.IsMelee
Holdable.Animator
Weapon.equipmentManager
Weapon.currentParries
```

Most of these are accessed through reflection because they are private or internal game members.

## Why this approach

The important design rule is:

```text
Do not read raw mouse buttons or keyboard keys.
```

The mod reads the game's own `InputAction` fields:

```text
meleeFireAction
meleeFireActionAlternative
altFireAction
```

This keeps the mod compatible with:

* Custom key bindings
* Controller input
* Non-default mouse bindings
* Future input rebinding by the player

The mod also calls the game's own melee methods:

```text
ChargeBasicMelee()
UseBasicMelee()
OnMeleeDone()
```

instead of trying to create a separate weapon system.

This keeps the original animation events, hit detection, damage logic, and blocking behavior mostly intact.

## Core behavior

### Drawing melee weapon

When the melee input is pressed and the stance is not toggled:

```text
ToggleOn()
→ IsToggled = true
→ ChargeBasicMelee()
→ meleePressed = true
```

The game enters its normal melee charge state.

### Keeping melee weapon drawn

While toggled, the mod repeatedly maintains the melee state.

If the game exits melee charge after an attack, the mod retries `ChargeBasicMelee()` after a short delay:

```ini
ReChargeRetryInterval = 0.08
```

This delay is important because immediately forcing charge again can conflict with the weapon's own attack completion flow.

### Attacking with Fire

When the stance is toggled and the player presses Fire:

```text
PullTrigger()
→ UseBasicMelee()
```

The normal Fire input is converted into one melee attack.

The mod blocks repeated attack starts while:

```text
AttackInProgress = true
```

### Sheathing melee weapon

When the player presses the melee key again while toggled:

```text
ToggleOff()
```

If no attack is happening, the mod calls:

```text
OnMeleeDone()
```

to return to the previous weapon.

If an attack is currently happening, the mod does not interrupt immediately.

Instead:

```text
SheatheAfterAttack = true
```

Then when `Weapon.ReportMeleeDone()` runs, the mod finishes the sheathe.

This avoids adding a second attack or breaking the current attack animation.

## Important behavior fixes

### Pressing melee to sheathe must not attack

A major bug during development was:

```text
Press melee to draw
Press melee again to sheathe
→ the game performs another melee attack before switching back
```

The fix was to separate "melee key pressed for toggle off" from "melee key pressed for vanilla attack".

When toggled, `HandleMeleeInput()` is intercepted and the mod manually decides whether to toggle off or maintain stance.

The mod also uses:

```text
SuppressMeleeUntilReleased
```

After manual sheathe, the melee input must be released before it can toggle again.

This prevents the same held input from being interpreted twice.

### Fire attack then immediate sheathe

Another issue was:

```text
Draw melee
Press Fire to attack
Immediately press melee to sheathe
→ two attacks could play before switching back
```

The fix was:

```text
If AttackInProgress:
    set SheatheAfterAttack = true
    do not call OnMeleeDone immediately
    wait for Weapon.ReportMeleeDone()
```

This ensures:

```text
Only the current attack finishes.
No extra attack is started.
Then the weapon is sheathed.
```

### Aim / block behavior must remain intact

Vanilla melee has special behavior when holding melee and right-click / aim.

The mod preserves this by:

* Keeping `HandleAimInput(bool)` aware that melee is held while toggled.
* Maintaining `alternativeMeleePressed` from the game's own `altFireAction`.
* Updating the current melee weapon animator bool:

```text
AlternativePressed
```

This allows the original aim / block behavior to continue working while the melee stance is toggled.

## Animator flicker problem

This was the hardest part of the mod.

After repeated draw / attack / sheathe cycles, the next draw could briefly show the wrong frame.

Observed problem:

```text
Weapon held normally
Press melee
First frame shows stale melee idle / A pose
Then draw animation plays from bottom to final pose
```

This was visible as a brief flash.

## Failed approaches

### Failed approach 1: Simply call ChargeBasicMelee again

At first, it seemed natural to just call `ChargeBasicMelee()` whenever the toggled stance should be maintained.

This works for basic functionality, but it does not fully solve stale animation state.

The weapon animator can still keep a previous state such as idle, slash, charge, or alternative state.

Result:

```text
The weapon stays functionally correct,
but the first visible frame of the next draw can flicker.
```

### Failed approach 2: Hide the weapon model during draw

Another attempted fix was to hide the weapon renderer or suppress visibility for a short time while forcing the animation state.

This caused worse visual issues.

Observed problem:

```text
The main weapon could remain visible for a moment.
The melee weapon could appear late.
The transition looked less reliable than the original small flicker.
```

This approach was rejected.

### Failed approach 3: Force a random animation state

Forcing a generic animation state without confirming it was safe can also break visuals.

If the cached state is an attack / slash / ADS / charge state, restoring it before sheathing makes the next draw worse.

The mod must only cache a known safe draw / equip state.

## Final animator solution

The final solution is:

```text
Cache a safe melee Equip animation state.
Before sheathing, restore the weapon animator to that safe state.
Then let the game sheathe normally.
```

The mod listens to:

```text
Weapon.ChargeMelee(true)
```

and caches the current animator state only if the current clip name looks safe.

Safe state rules:

```text
Must contain: Equip

Must not contain:
Slash
Attack
Fire
ADS
ToADS
Charge
Charged
```

Before sheathing, the mod:

```text
SetAlternativeState(0)
currentParries = 0
Animator.SetBool("Charge", false)
Animator.SetBool("Sprinting", false)
Animator.SetBool("AlternativePressed", false)
Animator.ResetTrigger("Parry")
Animator.Play(cached safe Equip state, 0, 0f)
Animator.Update(0f)
```

This prevents the next draw from starting from a stale attack or idle state.

## Why the safe Equip cache matters

The game may not expose a clean public method like:

```text
ResetMeleeAnimatorToDrawStart()
```

So the mod has to infer a good state from the weapon's own animator while it is behaving correctly.

The cached state approach is less invasive than replacing animations or hiding renderers.

It also stays per weapon through `ConditionalWeakTable`, so different melee weapons can have their own safe state.

## State storage design

The mod uses:

```text
ConditionalWeakTable<object, ToggleState>
ConditionalWeakTable<object, SafeAnimatorState>
```

This avoids global static state tied to one object forever.

It is safer when:

* EquipmentManager instances are recreated
* Weapons are swapped
* Scenes change
* Objects are destroyed

## Compatibility with The Dragonblade / Melee Expansion

This standalone mod has a soft dependency on:

```text
kumo.sulfur.melee_expansion
```

If that plugin is detected and the config option is enabled:

```ini
DisableWhenMeleeExpansionDetected = true
```

Toggle Melee Stance does not patch anything.

This prevents double-patching when another larger melee expansion already includes the same toggle stance functionality.

## Configuration

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

[Visual]
ResetMeleeAnimatorBeforeSheathe = true

[Compatibility]
DisableWhenMeleeExpansionDetected = true

[Debug]
LogStateChanges = false
```

### EnableMod

Enables or disables the mod.

```ini
EnableMod = true
```

### FirePerformsMeleeAttack

If enabled, normal Fire performs one melee attack while the melee stance is toggled.

```ini
FirePerformsMeleeAttack = true
```

If disabled, Fire input is blocked while toggled.

### ReChargeRetryInterval

Controls how soon the mod retries melee charge after an attack.

```ini
ReChargeRetryInterval = 0.08
```

Recommended range:

```text
0.05 - 0.12
```

Too low may fight the original animation flow.
Too high can make the weapon feel slow to return to held stance.

### ResetMeleeAnimatorBeforeSheathe

Enables the safe animator reset before sheathing.

```ini
ResetMeleeAnimatorBeforeSheathe = true
```

Recommended default is `true`.

Disabling this may bring back the draw-frame flicker.

### DisableWhenMeleeExpansionDetected

If enabled, this standalone mod disables itself when the larger melee expansion plugin is installed.

```ini
DisableWhenMeleeExpansionDetected = true
```

Recommended default is `true`.

### LogStateChanges

Logs toggle state transitions.

```ini
LogStateChanges = false
```

Useful for debugging, but should stay disabled for normal gameplay.

## Pitfalls / lessons learned

### Do not treat the melee key as both toggle and attack

The same input cannot safely mean:

```text
toggle off
and
perform vanilla melee attack
```

at the same time.

If you let vanilla melee handling continue after toggle-off, the game may start another attack before sheathing.

### Do not interrupt an attack directly

If the player sheathes during an attack, wait for `Weapon.ReportMeleeDone()`.

Interrupting immediately can cause:

* Extra attacks
* Broken animation state
* Weapon switching at the wrong time
* Stale melee charge state

### Do not ignore input release

After manual sheathe, suppress melee input until the key is released.

Otherwise, one physical key press can be processed again on the next frame.

### Do not read raw mouse buttons

Raw input checks like `Mouse.current.leftButton` or `Input.GetMouseButton` would ignore game rebinding and controller input.

Use the game's own `InputAction` fields instead.

### Do not globally force melee charging every frame

Calling `ChargeBasicMelee()` too aggressively can fight the original melee state machine.

Use a small retry interval and check whether the game is already in melee charge.

### Do not hide the weapon model to fix animation flicker

Hiding the model can create worse visual artifacts than the original problem.

The safer solution is to restore the animator to a cached safe Equip state before sheathing.

### Do not cache unsafe animation states

Never cache attack, slash, ADS, or charge animation states as the future draw state.

Only cache states that look like safe Equip states.

### Do not patch without compatibility detection

A larger melee expansion may include this same feature.

The standalone version should detect and disable itself when that mod is installed to avoid duplicate input patches.

## What it does not do

This mod does not:

* Add new melee attacks
* Add dash skills
* Add new damage logic
* Change melee weapon damage
* Change enemy behavior
* Replace the melee animation controller
* Edit save data
* Edit original game files

It only changes how the existing melee stance is entered, maintained, attacked from, and exited.

## Compatibility

Potential conflicts:

* Mods that patch `EquipmentManager.HandleMeleeInput(bool)`
* Mods that patch `EquipmentManager.HandleAimInput(bool)`
* Mods that patch `EquipmentManager.PullTrigger()`
* Mods that patch `Weapon.ReportMeleeDone()`
* Mods that replace melee weapon animation behavior
* Mods that also implement toggle melee stance

The mod is designed to disable itself when `kumo.sulfur.melee_expansion` is installed.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, Unity Input System, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.