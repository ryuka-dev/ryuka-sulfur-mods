# Deadeye Instinct

Deadeye Instinct is a **SULFUR** aim assist overhaul mod for **mouse and controller**.

It expands the game's original aim assist pipeline into two configurable assist styles:

- **Magnet** — a stronger magnetic pull with optional weakspot targeting.
- **Natural** — smoother input-shaped sticky aim that feels less like direct lock-on.

This repository contains only my original mod source code and packaging text. It does **not** contain SULFUR game files, Unity assemblies, BepInEx binaries, paid assets, or decompiled game source.

---

## Features

- Mouse aim assist support
- Controller aim assist support
- Magnet and Natural assist modes
- Low / Medium / High / Custom presets
- Optional weakspot targeting
- Runtime weakspot debug overlay
- Smooth target tracking
- Edge softening to reduce snap-back jitter
- Look-away escape handling for controller aiming
- Uses the game's original aim assist / input / camera pipeline
- Does not patch weapon firing or projectile spawning

---

## Mod Identity

```text
Mod Name: Deadeye Instinct
GUID: ryuka.sulfur.deadeyeinstinct
Namespace: Ryuka.Sulfur.DeadeyeInstinct
Suggested DLL: DeadeyeInstinct.dll
Config: BepInEx/config/ryuka.sulfur.deadeyeinstinct.cfg
```

---

## Assist Modes

### Magnet

Magnet is the stronger assist mode.

It works like a magnetic pull toward the target area. When weakspot targeting is enabled, it tries to pull toward the best available weakspot instead of the enemy's center.

Magnet is recommended for players who want a strong and obvious aim assist effect.

### Natural

Natural is the smoother assist mode.

It is designed to feel more like sticky aim or aim friction. It helps stabilize aim near targets without directly snapping to the target center.

Natural is recommended for players who want aim assist without an obvious lock-on feeling.

---

## Preset System

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

The default release setup is:

```ini
MagnetPreset = High
NaturalPreset = Medium
```

These match the current tested default balance.

---

## Preset Values

### Magnet Low

```ini
AssistCoefficient = 0.65
RotationDeltaPerSecond = 4.5
MaxRotationDeltaPerFrame = 0.35
OuterBubbleAngleDegrees = 24
InnerBubbleAngleDegrees = 5
BubbleCurvePower = 1.1
DistanceFalloffPower = 0.7

MagnetWeakspotBias = 0.55
MagnetCenterBias = 0.05
MagnetMovementOnlyScale = 0.20
MagnetSmoothing = 16
MagnetReleaseSmoothing = 8
MagnetEdgeSofteningPower = 3.0
MagnetOuterEdgeStrength = 0.03
MagnetSnapBackAnglePadding = 6
MagnetLookAwayThreshold = 0.05
MagnetLookAwayMinScale = 0.02
MagnetLookAwayCurvePower = 1.0
MagnetLookAwayMovementScale = 0.35
```

### Magnet Medium

```ini
AssistCoefficient = 0.95
RotationDeltaPerSecond = 7.0
MaxRotationDeltaPerFrame = 0.55
OuterBubbleAngleDegrees = 28
InnerBubbleAngleDegrees = 6
BubbleCurvePower = 0.9
DistanceFalloffPower = 0.55

MagnetWeakspotBias = 0.70
MagnetCenterBias = 0.10
MagnetMovementOnlyScale = 0.28
MagnetSmoothing = 19
MagnetReleaseSmoothing = 10
MagnetEdgeSofteningPower = 2.6
MagnetOuterEdgeStrength = 0.05
MagnetSnapBackAnglePadding = 5
MagnetLookAwayThreshold = 0.08
MagnetLookAwayMinScale = 0.05
MagnetLookAwayCurvePower = 1.1
MagnetLookAwayMovementScale = 0.40
```

### Magnet High

Current tested default Magnet preset.

```ini
AssistCoefficient = 1.4
RotationDeltaPerSecond = 10.0
MaxRotationDeltaPerFrame = 0.9
OuterBubbleAngleDegrees = 34
InnerBubbleAngleDegrees = 8
BubbleCurvePower = 0.65
DistanceFalloffPower = 0.35

MagnetWeakspotBias = 0.85
MagnetCenterBias = 0.15
MagnetMovementOnlyScale = 0.35
MagnetSmoothing = 22
MagnetReleaseSmoothing = 12
MagnetEdgeSofteningPower = 2.2
MagnetOuterEdgeStrength = 0.08
MagnetSnapBackAnglePadding = 4
MagnetLookAwayThreshold = 0.10
MagnetLookAwayMinScale = 0.08
MagnetLookAwayCurvePower = 1.2
MagnetLookAwayMovementScale = 0.45
```

### Natural Low

```ini
AssistCoefficient = 0.8
RotationDeltaPerSecond = 6.0
MaxRotationDeltaPerFrame = 0.45
OuterBubbleAngleDegrees = 26
InnerBubbleAngleDegrees = 6
BubbleCurvePower = 0.9
DistanceFalloffPower = 0.55

NaturalTowardAssist = 0.05
NaturalSideAssist = 0.28
NaturalAwayDamping = 0.75
NaturalMovementOnlyScale = 0
NaturalMaxInputFraction = 0.55
NaturalMinDeltaPerFrame = 0.003
NaturalSmoothing = 24
NaturalReleaseSmoothing = 8
NaturalDirectionSmoothing = 14
NaturalReleaseCurvePower = 2.8
NaturalOuterEdgeStrength = 0.01
```

### Natural Medium

Current tested default Natural preset.

```ini
AssistCoefficient = 1.4
RotationDeltaPerSecond = 10.0
MaxRotationDeltaPerFrame = 0.9
OuterBubbleAngleDegrees = 34
InnerBubbleAngleDegrees = 8
BubbleCurvePower = 0.65
DistanceFalloffPower = 0.35

NaturalTowardAssist = 0.12
NaturalSideAssist = 0.55
NaturalAwayDamping = 1.45
NaturalMovementOnlyScale = 0
NaturalMaxInputFraction = 0.85
NaturalMinDeltaPerFrame = 0.003
NaturalSmoothing = 30
NaturalReleaseSmoothing = 10
NaturalDirectionSmoothing = 18
NaturalReleaseCurvePower = 2.4
NaturalOuterEdgeStrength = 0.02
```

### Natural High

```ini
AssistCoefficient = 2.0
RotationDeltaPerSecond = 14.0
MaxRotationDeltaPerFrame = 1.4
OuterBubbleAngleDegrees = 42
InnerBubbleAngleDegrees = 10
BubbleCurvePower = 0.5
DistanceFalloffPower = 0.2

NaturalTowardAssist = 0.20
NaturalSideAssist = 0.85
NaturalAwayDamping = 2.2
NaturalMovementOnlyScale = 0
NaturalMaxInputFraction = 1.2
NaturalMinDeltaPerFrame = 0.003
NaturalSmoothing = 38
NaturalReleaseSmoothing = 14
NaturalDirectionSmoothing = 24
NaturalReleaseCurvePower = 2.0
NaturalOuterEdgeStrength = 0.04
```

---

## Weakspot Targeting

Magnet mode can prioritize weakspots.

The weakspot system uses the game's actual hitbox data:

- `Hitmesh`
- `Hitmesh.Data`
- `Hitmesh.Data.GetShapeMultiplier()`
- `HitboxColliders`
- `runtimeHitmeshData`
- `runtimeVertexData`

Base hitbox multiplier order found during development:

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

This system currently uses the base hitbox multiplier. It does not fully evaluate final damage after weapon-specific states, elemental mitigation, buffs, or projectile context, because the aim assist hook does not have full current projectile damage context.

---

## Weakspot Debug Overlay

Deadeye Instinct includes a runtime weakspot overlay.

```ini
[Weakspot Debug Overlay]
EnableWeakspotDebugOverlay = true
WeakspotDebugOnlyTrackedTarget = true
WeakspotDebugShowAllPositiveShapes = false
```

The overlay draws actual hitbox polygons from the game's runtime hitbox data.

Default color meaning:

```text
Red     = very high multiplier
Orange  = high multiplier
Yellow  = medium multiplier
White   = lower positive multiplier
```

This is mainly for testing weakspot targeting and verifying target points.

---

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

If upgrading from the prototype build, remove the old config:

```text
BepInEx/config/ryuka.sulfur.strongaimassist.cfg
```

---

## Build

Create a .NET Framework class library and reference:

```text
BepInEx/core/BepInEx.dll
BepInEx/core/0Harmony.dll
SULFUR_Data/Managed/Assembly-CSharp.dll
SULFUR_Data/Managed/UnityEngine.dll
SULFUR_Data/Managed/UnityEngine.CoreModule.dll
Unity.Mathematics.dll if required by your project setup
```

Set all references to:

```text
Copy Local = False
```

Suggested output:

```text
DeadeyeInstinct.dll
```

---

## Architecture

Deadeye Instinct intentionally stays inside the game's existing input and camera pipeline.

The final path is:

```text
AimAssist
→ rotationPullDelta
→ InputReader
→ CameraController
```

The mod does not:

- patch `Weapon.DispatchProjectile()`
- patch projectile spawning
- modify projectile trajectory
- directly rotate `camera.transform`
- replace the camera controller

This is important for compatibility and for preserving normal camera behavior such as sensitivity, controller input, ADS behavior, and camera constraints.

---

## Development Notes and Pitfalls

This section records the major problems found during development. It is intended to help future maintenance and prevent repeating the same mistakes.

### 1. Do not patch projectile firing for aim assist

An early prototype patched projectile dispatch / bullet magnetism. This caused projectile / AutoPool-related errors.

Final decision:

```text
Do not patch Weapon.DispatchProjectile()
Do not patch projectile spawning
Do not modify projectile behavior
```

Aim assist should be implemented through input / camera delta, not projectile logic.

### 2. Do not directly rotate the camera transform

An early approach tried to rotate `camera.transform.rotation` directly.

This was rejected because it bypasses the game's real camera pipeline and may conflict with:

- sensitivity
- ADS sensitivity
- controller look curves
- camera constraints
- recoil
- FOV behavior
- input locks

Final decision:

```text
Never directly write camera.transform.rotation for this mod.
```

Use the official pipeline instead.

### 3. Mouse did not consume official rotationPullDelta

The game's original `InputReader.Update()` only consumed `AimAssist.rotationPullDelta` when `GamepadUsed == true`.

That means mouse look input could disable the official pull even if aim assist calculated one.

Final decision:

```text
Patch InputReader.Update so mouse can also consume rotationPullDelta.
```

This is one of the core patches.

### 4. Official ADS pull is not continuous tracking

The game's original behavior is mostly an ADS snap/pull window.

It does not behave like continuous tracking. The mod therefore adds its own continuous delta after the official `AimAssist.LateUpdate()` target scan.

Final decision:

```text
Use official AimAssist for target scanning.
Add custom continuous rotationPullDelta afterward.
```

### 5. Natural mode must not include mouse official ADS snap

Natural mode is supposed to feel smooth and input-shaped. Keeping the official mouse ADS snap made it feel inconsistent.

Final decision:

```text
Natural mode clears mouse official ADS pull.
Natural mode keeps controller official ADS pull by default.
```

### 6. Natural mode cannot be only "pull when moving away"

A pure "only resist when the player moves away" design was too weak.

Final Natural mode design:

```text
Toward target: small configurable boost
Side sweep: side catch
Away from target: sticky damping
Movement only: disabled by default
```

This feels stronger while still avoiding hard lock-on.

### 7. Natural mode needs soft release

A previous Natural version had a constant sticky force inside the bubble and then suddenly released outside it. This caused a noticeable "breakaway" speed change.

Final decision:

```text
Natural mode uses release curve falloff near the outer bubble edge.
```

Relevant concepts:

```text
NaturalReleaseCurvePower
NaturalOuterEdgeStrength
NaturalReleaseSmoothing
```

### 8. Target point and target switching can cause jitter

Some jitter came from official `trackedTargetPoint` changes and target switches.

Final decision:

```text
Smooth Natural target direction.
Fade assist in after target switch.
Smooth release separately from active pull.
```

Relevant concepts:

```text
NaturalDirectionSmoothing
TargetSwitchFadeSeconds
TargetSwitchMinScale
```

### 9. Magnet center bias can accidentally become hard center lock

A "more center focused" Magnet version overdid center bias and caused the aim to stick too much to the enemy center.

Final decision:

```text
Magnet should pull back into the valid target area, not force the crosshair to one center point.
```

`MagnetCenterBias` should stay low.

### 10. Weakspot targeting should use weakspot area, not weakspot center

A weakspot implementation that aimed at the average point of the weakspot polygon still felt like point lock.

Final decision:

```text
If the current aim point is inside the weakspot polygon:
    keep the current point

If outside:
    pull toward the closest point on the weakspot polygon edge
```

This preserves movement inside the valid target area.

### 11. Weakspot delta must replace official center pull

When weakspot pull was added on top of the official center pull, the final result still favored enemy center.

Final decision:

```text
If Magnet weakspot targeting succeeds:
    clear official center rotationPullDelta
    add weakspot-directed pull
```

### 12. Magnet needs edge softening

Strong Magnet pull near the edge of the assist bubble caused snap-back jitter.

Final decision:

```text
Reduce Magnet strength near the outer edge before release.
```

Relevant concepts:

```text
MagnetEdgeSofteningPower
MagnetOuterEdgeStrength
MagnetSnapBackAnglePadding
```

### 13. Controller needs look-away escape

With controller right stick only, the player sometimes could not move out of lock because stick speed was lower than Magnet pull.

Final decision:

```text
If player look input clearly moves away from target:
    reduce Magnet pull
```

Relevant concepts:

```text
MagnetAllowLookAwayEscape
MagnetLookAwayThreshold
MagnetLookAwayMinScale
MagnetLookAwayCurvePower
MagnetLookAwayMovementScale
```

### 14. Presets should not overwrite advanced config

An early preset implementation wrote preset values into all advanced config entries at startup.

This was not ideal because it destroyed manual tuning.

Final decision:

```text
Low / Medium / High should be runtime preset values.
Custom should use advanced config values.
Preset selection should not overwrite advanced values.
```

### 15. Keep reflection and config binding safe

Several errors came from missing config bindings or unsafe field access.

Final decision:

```text
Every ConfigEntry must be bound.
Use fallback-safe reads where appropriate.
Avoid logging repeated per-frame warnings.
Fallback to official trackedTargetPoint when optional weakspot logic fails.
```

### 16. Do not guess game internals

Several design mistakes came from assuming how the game worked before checking the real code.

Final rule:

```text
If a behavior depends on game internals, verify it through actual game code or logs first.
Do not guess method behavior.
```

This was especially important for:

- `InputReader.Update()`
- `AimAssist.LateUpdate()`
- `ExtendedCameraController.InsertRotationValue()`
- `Hitmesh`
- `HitboxColliders`
- damage / hitbox multipliers

---

## Compatibility Notes

Deadeye Instinct should be more compatible than projectile-based aim assist because it does not touch firing or projectile systems.

Known external log noise that is not caused by Deadeye Instinct:

- Unity fullscreen / swapchain warnings
- negative scale `BoxCollider` warnings from level geometry
- unrelated mod reflection warnings
- unrelated weapon randomizer errors

When debugging, check whether the stack trace includes:

```text
Ryuka.Sulfur.DeadeyeInstinct
```

If it does not, the error is likely from another mod or the game itself.

---

## Source Code Policy

This repository only contains my original mod source code and packaging text.

It does not include:

- SULFUR game files
- Unity assemblies
- BepInEx binaries
- paid assets
- decompiled game source

The source is shared for learning, reference, and transparency.

Other modders may study the implementation or use it as a reference for their own work.

---

## License

Add a license before publishing if needed.

Recommended options:

- MIT License for permissive source sharing
- All Rights Reserved if you only want to share for reference
