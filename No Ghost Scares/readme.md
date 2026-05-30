# No Ghost Scares

## What it does

No Ghost Scares disables strong ghost scare trigger objects in SULFUR.

It targets objects whose names match:

```text
Ability_Ghost_*_Scare
````

When such an object is detected, the mod disables and destroys it.

The goal is to remove sudden ghost scare events without removing normal ghost enemies or unrelated ghost-themed objects.

## Implementation overview

This mod uses BepInEx.

It does not patch a specific gameplay method with Harmony.
Instead, it periodically scans all loaded Unity scenes and checks object names.

The scan flow is:

```text
Loaded scenes
→ root GameObjects
→ recursive Transform scan
→ object name check
→ disable / destroy strong ghost scare object
```

The main target check is strict:

```text
object name contains "Ability_Ghost_"
and
object name contains "_Scare"
```

This keeps the blocking focused on strong scare objects only.

## Key target

The important target pattern is:

```text
Ability_Ghost_*_Scare
```

The mod treats this as a strong ghost scare object.

When blocking is enabled, the object is:

```text
SetActive(false)
Destroy(object)
```

## Debug detection

The mod also has an optional broad ghost-name detector.

When enabled, it can report objects containing names like:

```text
Ghost
Wraith
Poltergeist
Apparition
Scare
Haunt
Specter
Spectre
```

However, these broad matches are **debug only**.

They are never blocked by default.

This was an important safety decision because many normal enemies, particles, effects, or unrelated objects may contain ghost-like words in their names.

## Why this approach

The ghost scare objects are scene objects, not a simple player stat or enemy balance value.

The most reliable first implementation was to detect the instantiated scare object by name and remove it after it appears.

This avoids broad changes such as:

* Removing all ghosts
* Disabling all objects with "Ghost" in the name
* Patching enemy spawning globally
* Editing game files

The mod only blocks the specific strong scare pattern.

## Runtime scanning

The mod scans repeatedly instead of only once at startup.

Default scan interval:

```ini
ScanInterval = 1.0
```

This is necessary because scare objects may be created after the scene has already loaded.

A single startup scan could miss them.

## Duplicate prevention

The mod keeps a `HashSet` of processed strong ghost scare object instance IDs.

This prevents the same scare object from being blocked and logged repeatedly.

There is also a separate `HashSet` for broad debug-only ghost records, so the debug overlay does not spam the same object again and again.

## Debug overlay

The mod includes an optional on-screen debug overlay.

When enabled, it shows recent detected or blocked ghost scare objects, including:

* Time
* Action
* Category
* Object name
* Full hierarchy path
* Active state

The overlay is intended for testing object detection and should normally stay disabled.

## Configuration

Config file:

```text
BepInEx/config/kumo.sulfur.no_ghost_scares.cfg
```

Main options:

```ini
[General]
EnableBlocking = true

[Debug]
ShowDebugOverlay = false
LogActions = false
DetectBroadGhostNames = false

[Performance]
ScanInterval = 1.0

[Overlay]
OverlayKeepSeconds = 12.0
MaxOverlayItems = 8
```

### EnableBlocking

If enabled, matching `Ability_Ghost_*_Scare` objects are disabled and destroyed.

```ini
EnableBlocking = true
```

If disabled, the mod only detects matching objects and reports them through log / overlay when debug options are enabled.

### ShowDebugOverlay

Shows recent detections on screen.

```ini
ShowDebugOverlay = false
```

Recommended default is `false`.

### LogActions

Writes detected or blocked objects to the BepInEx log.

```ini
LogActions = false
```

Recommended default is `false`.

### DetectBroadGhostNames

Debug-only option.

If enabled, the mod also reports objects with broad ghost-related names, such as `GhostParticles` or `Unit_Enemy_Ghost`.

These objects are not blocked.

```ini
DetectBroadGhostNames = false
```

Recommended default is `false`.

### ScanInterval

Controls how often loaded scenes are scanned.

```ini
ScanInterval = 1.0
```

The code clamps this to at least `0.2` seconds to avoid overly aggressive scanning.

## Pitfalls / lessons learned

### Do not block every object with "Ghost" in the name

This was the main safety lesson.

Many harmless or normal gameplay objects may contain words like:

```text
Ghost
Scare
Particles
Wraith
Specter
```

Blocking all of them would be too broad and could break enemies, effects, or unrelated systems.

The actual blocking rule should stay strict:

```text
Ability_Ghost_*_Scare
```

Broad ghost names should only be used for debugging.

### Do not rely on a single scan

Ghost scare objects may be created after scene load.

The mod uses a coroutine scan loop so it can catch objects that appear later.

### Keep debug tools separate from blocking logic

The mod has two separate detection modes:

```text
StrongGhostScare
→ can be blocked

BroadGhostRelated
→ debug only, never blocked
```

This separation makes the mod safer while still allowing object discovery during reverse engineering.

### Avoid log spam

The mod stores processed instance IDs so each object is recorded once.

Without this, repeated scene scanning could flood the log or overlay.

### Destroy after SetActive(false)

The mod first disables the scare object, then destroys it.

This reduces the chance that it remains active for a frame after detection.

## Difference from No Forest Ambush

No Ghost Scares targets:

```text
Ability_Ghost_*_Scare
```

No Forest Ambush targets scene objects named:

```text
Event_Forest_Ambush
Forest_Ambush
```

These are different scare systems.

No Ghost Scares should not be described as a Forest Ambush remover.

## Difference from No Forest Falling Trees

No Forest Falling Trees only neutralizes the falling-tree presentation inside Forest Ambush objects.

No Ghost Scares is unrelated to falling trees.

It specifically removes strong ghost scare trigger objects.

## What it does not do

This mod does not:

* Remove all ghosts
* Remove ghost enemies globally
* Remove Forest Ambush events
* Remove falling trees
* Change enemy stats
* Change player stats
* Edit save data
* Edit original game files

It only blocks strong ghost scare objects matching `Ability_Ghost_*_Scare`.

## Compatibility

This mod should be compatible with most mods.

Potential conflicts:

* Mods that intentionally use or replace `Ability_Ghost_*_Scare` objects
* Mods that rename ghost scare objects
* Mods that change scene hierarchy timing

Because this mod scans by object name, future game updates that rename scare objects may require an update.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, and the required Unity / game assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.