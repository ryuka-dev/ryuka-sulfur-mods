# Deadeye Instinct

Your aim does not move alone.  
It follows instinct, pressure, and the weak point hiding under the monster's skin.

Deadeye Instinct is a configurable aim assist overhaul for **SULFUR**.

It expands the game's original controller aim assist into a stronger and more flexible system for both mouse and controller. It includes three assist styles: **Magnet**, **Natural**, and **HardLock**.

## Features

- Mouse aim assist support
- Controller aim assist support
- Three assist styles:
  - **Magnet**
  - **Natural**
  - **HardLock**
- Optional weakspot targeting
- Smooth target tracking
- Low / Medium / High / Custom presets for Magnet and Natural
- 360-degree HardLock target scanning
- HardLock target priority scoring
- Optional AutoFire
- Runtime weakspot debug overlay
- Edge softening to reduce snap-back jitter
- Controller escape handling for Magnet mode
- Configurable hold key to temporarily disable the aimbot

## Assist Modes

### Magnet

Magnet is the stronger normal assist mode.

It behaves like a magnetic pull toward the target area. When weakspot targeting is enabled, it tries to pull toward the best available weakspot instead of the enemy's center.

This mode is recommended if you want a strong but still aim-assist-like effect.

### Natural

Natural is the smoother assist mode.

It is designed to feel more like aim friction or sticky aim rather than direct snapping. It helps stabilize aim near targets, reduces overshooting, and keeps player input feeling more natural.

This mode is recommended if you want aim assist without an obvious lock-on feeling.

### HardLock

HardLock is the experimental strong lock mode.

It scans hostile NPCs around the player, chooses a target using priority scoring, and directly rotates the camera toward the selected target point. It is designed for extreme assist behavior, not subtle assist.

HardLock can optionally use AutoFire.

## Preset System

Magnet and Natural use a simplified preset system.

```ini
[Assist Mode]
Mode = Magnet

[Preset]
MagnetPreset = High
NaturalPreset = Medium
```

Available preset values:

```ini
Low
Medium
High
Custom
```

Rules:

- `Mode = Magnet` uses `MagnetPreset`
- `Mode = Natural` uses `NaturalPreset`
- `Low`, `Medium`, and `High` use built-in runtime values
- `Custom` uses the advanced config values

Default setup:

```ini
MagnetPreset = High
NaturalPreset = Medium
```

## HardLock Recommended Config

```ini
[Assist Mode]
Mode = HardLock

[Master Switch]
EnableAimbot = true
HoldDisableKey = LeftAlt

[HardLock]
HardLockMaxDistance = 90
HardLockMaxDistanceCap = 90
HardLockRequireVisibleTarget = true
HardLockRequireLineOfSight = true
HardLockPreferWeakspot = true
HardLockWeakspotBias = 1

[HardLock Target Priority]
HardLockTargetPriority = ThreatWeighted
DistanceWeight = 0.75
LowHealthWeight = 0.25
WeakspotWeight = 0.15
StickyTargetBonus = 0.20
```

`HardLockMaxDistanceCap` is a safety cap. The actual search distance is:

```text
min(HardLockMaxDistance, HardLockMaxDistanceCap)
```

Set `HardLockMaxDistanceCap = 0` only if you intentionally want to disable the safety cap.

## AutoFire

AutoFire is optional and disabled by default in normal release usage.

```ini
[Auto Fire]
EnableAutoFire = false
AutoFireOnlyInHardLock = true
AutoFireRequireAligned = true
AutoFireMaxAngleDegrees = 1.0
AutoFirePulseSeconds = 0.04
AutoFireReleaseSeconds = 0.04
AutoFireRespectSemiAuto = true
```

AutoFire does **not** directly call:

```text
Weapon.Shoot()
Weapon.AttemptShoot()
Weapon.DispatchProjectile()
```

Instead, it controls the original trigger state through the game's normal weapon pipeline. This allows the original weapon logic to handle ammo, cooldown, reload, semi-auto behavior, full-auto behavior, animations, sound, and durability.

## Weakspot Targeting

Magnet and HardLock can prioritize enemy weakspots.

The weakspot system uses the game's actual hitbox data:

- `Hitmesh`
- `Hitmesh.Data`
- `Hitmesh.Data.GetShapeMultiplier()`
- `HitboxColliders`
- runtime hitmesh / vertex data

Known base hitbox multiplier order found during development:

```text
Eye     = 1.5
Head    = 1.0
Thorax  = 0.75
Body    = 0.5
Groin   = 0.5
Arm     = 0.25
Leg     = 0.25
Block   = 0
```

Important limitation:

This system currently uses the base hitbox multiplier. It does not fully evaluate final damage after weapon-specific states, buffs, elemental effects, or projectile context.

## Weakspot Debug Overlay

```ini
[Weakspot Debug Overlay]
EnableWeakspotDebugOverlay = true
WeakspotDebugOnlyTrackedTarget = true
WeakspotDebugShowAllPositiveShapes = false
```

The overlay displays the weakspot polygons used by the game's runtime hitbox data.

Default color meaning:

```text
Red     = very high multiplier
Orange  = high multiplier
Yellow  = medium multiplier
White   = lower positive multiplier
```

This is mainly for testing and tuning.

## Installation

1. Install BepInEx 5 for SULFUR.
2. Put `DeadeyeInstinct.dll` into:

```text
BepInEx/plugins/
```

3. Start the game once.
4. Edit the generated config file:

```text
BepInEx/config/ryuka.sulfur.deadeyeinstinct.cfg
```

If upgrading from prototype builds, remove the old config:

```text
BepInEx/config/ryuka.sulfur.strongaimassist.cfg
```

## Compatibility

Deadeye Instinct is designed around the game's existing aim assist, input, camera, and weapon trigger pipeline.

Normal Magnet / Natural path:

```text
AimAssist
→ rotationPullDelta
→ InputReader
→ CameraController
```

HardLock path:

```text
UnitManager.GetAllNpcs(false)
→ hostile target filtering
→ target priority scoring
→ ExtendedCameraController.RotateTowardPosition()
```

AutoFire path:

```text
Holdable.SetTrigger(bool)
→ Weapon.HandleShootingUpdate()
→ Weapon.AttemptShoot()
→ Weapon.Shoot()
```

Deadeye Instinct does not directly patch projectile spawning or projectile flight behavior.

## Notes

This mod is intended for SULFUR's single-player / PvE gameplay.

The default presets are intentionally strong. Use `Low` or `Medium` if you want a lighter assist feel.

HardLock and AutoFire are experimental strong-assist features and may require tuning depending on weapon, enemy type, and level layout.

## Source Code

The source code for this mod is available in my GitHub repository.

It is shared for learning, reference, and transparency. The repository only contains my original mod source code and packaging text. It does not include SULFUR game files, Unity assemblies, BepInEx binaries, paid assets, or decompiled game source.

If you are another modder, feel free to study how the mod works or use it as a reference for your own implementation.

[ryuka-sulfur-mods](https://github.com/ryuka-dev/ryuka-sulfur-mods)


---

## Development Notes and Pitfalls

### Do not patch projectile firing for aim assist

Aim assist should not patch `Weapon.DispatchProjectile()` or directly modify projectile behavior.

Final rule:

```text
Do not patch Weapon.DispatchProjectile()
Do not directly spawn projectiles
Do not modify projectile flight for aim assist
```

### AutoFire should use the original trigger pipeline

AutoFire should not call `Shoot()`, `AttemptShoot()`, or `DispatchProjectile()` directly.

Final path:

```text
Holdable.SetTrigger(bool)
→ Weapon.HandleShootingUpdate()
→ Weapon.AttemptShoot()
→ Weapon.Shoot()
```

This preserves ammo checks, cooldown, reload, semi-auto behavior, full-auto behavior, animations, sound, durability, and weapon-specific logic.

### Empty-magazine reload behavior

Original `Weapon.AttemptShoot()` handles empty-magazine behavior.

The important flow is:

```text
first empty trigger press:
    click
    hasClicked = true
    SetTrigger(false)

next trigger press with auto-reload enabled:
    Reload()
```

Do not stop AutoFire just because `iAmmoCurrent <= 0`, or the original auto-reload path will never run.

### Mouse input must use the new Unity Input System

SULFUR uses the new Unity Input System. Do not call:

```text
UnityEngine.Input.GetKey()
```

Use:

```text
UnityEngine.InputSystem.Keyboard.current
```

### Normal assist and HardLock use different camera paths

Normal Magnet / Natural assist should keep using the original small input-like `rotationPullDelta` pipeline.

HardLock must not feed yaw / pitch degrees into `rotationPullDelta`, because `InputReader` and `RotateCamera` will multiply that value by camera speed and cause over-rotation.

HardLock should use:

```text
ExtendedCameraController.RotateTowardPosition()
```

### Npc.GetAimPosition is not a player target point

`Npc.GetAimPosition()` is for an NPC aiming at its own `aimTarget`.

Do not use it as the point the player should aim at on that NPC.

Use:

```text
Unit.GetPositionToAimAt()
mainCollider.bounds.center
```

### HardLock needs visible target filtering

HardLock should not lock enemies behind walls or far outside the current combat area by default.

Use:

```text
HardLockRequireVisibleTarget = true
HardLockMaxDistanceCap = 90
```

### Target points must be validated

Weakspot or fallback target points must be finite and near the target collider bounds.

Invalid target points can cause sky-locking, camera twitching, or false AutoFire alignment.

### Presets should not overwrite advanced config

Low / Medium / High should be runtime preset values. `Custom` should read advanced config values.

Preset selection must not overwrite advanced tuning values in the config file.
