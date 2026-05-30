# No Forest Ambush

## What it does

No Forest Ambush neutralizes the Forest Ambush scare presentation in SULFUR.

It targets Forest Ambush event objects and disables the parts responsible for the falling-tree scare presentation:

- Forest Ambush `Contents` Animator
- Forest Ambush `Contents` AudioSource
- Tree visual objects
- Particle visual objects

The goal is to stop the Forest Ambush scare presentation without globally removing enemies or editing game files.

## Implementation overview

This mod uses BepInEx.

It scans loaded Unity scenes for objects whose names contain:

```text
Event_Forest_Ambush
Forest_Ambush
````

When a matching root object is found, the mod looks for its child:

```text
Contents
```

Then it neutralizes the scare presentation inside that `Contents` object.

The uploaded implementation disables `Animator` and `AudioSource` components directly on `Contents`, then disables child objects whose names match:

```text
Tree*
Particles
```

This matches the observed Forest Ambush hierarchy from debugging.

## Key game objects used

The important scene hierarchy is:

```text
Forest_Ambush / Event_Forest_Ambush
└─ Contents
   ├─ Tree1
   ├─ Tree2
   ├─ Tree4
   ├─ Tree5
   ├─ Tree6
   ├─ Tree8
   ├─ Tree9
   └─ Particles
```

The mod does not rely on a hardcoded full path.
It searches loaded scene roots and recursively scans transforms.

## Why this approach

Forest Ambush is not a normal enemy type and not a simple global setting.

The scare is driven by scene objects, especially the `Contents` object inside Forest Ambush roots.

The important lesson from implementation was:

```text
Do not blindly disable the whole Forest Ambush root.
```

Forest Ambush roots may contain objects required for encounter or spawn logic.

So this version only neutralizes the scare presentation:

* Disable the `Contents` Animator
* Disable the `Contents` AudioSource
* Disable tree visual children
* Disable particle visual children

It intentionally avoids disabling these objects:

* `Trigger`
* `TriggerSpawner`
* `NavMeshAnchor`

Those may be required for encounter or spawn behavior.

## Runtime scanning

Forest Ambush objects may appear after the first scene load.

Because of that, the mod does not only scan once at startup.

It runs cleanup bursts:

* On startup
* When a scene is loaded
* When the loaded scene hierarchy signature changes

The scene signature is based on:

* Loaded scene count
* Scene names
* Scene root counts

When the signature changes, the mod starts another cleanup burst.

This avoids scanning every frame while still catching objects that appear after scene load.

## Cleanup burst design

The mod uses repeated cleanup passes instead of one single scan.

Default behavior:

```ini
CleanupBurstIterations = 24
CleanupBurstInterval = 0.2
```

This means after startup / scene load / hierarchy change, the mod runs multiple passes over a short period.

This was necessary because the Forest Ambush object may be instantiated or completed slightly after the scene becomes loaded.

## Duplicate prevention

The mod stores processed Forest Ambush instance IDs in a `HashSet`.

This prevents the same ambush object from being neutralized and logged repeatedly.

When a new scene is loaded, the processed ID cache is cleared.

## Configuration

Config file:

```text
BepInEx/config/kumo.sulfur.no_forest_ambush.cfg
```

Main options:

```ini
[General]
LogActions = false

[Performance]
SceneSignatureCheckInterval = 1.0
CleanupBurstIterations = 24
CleanupBurstInterval = 0.2
```

### LogActions

Logs neutralized Forest Ambush objects.

Recommended default:

```ini
LogActions = false
```

Enable this only when debugging detection.

### SceneSignatureCheckInterval

Controls how often the mod checks whether the loaded scene hierarchy changed.

Recommended default:

```ini
SceneSignatureCheckInterval = 1.0
```

### CleanupBurstIterations

Controls how many cleanup passes run after startup, scene load, or hierarchy change.

Recommended default:

```ini
CleanupBurstIterations = 24
```

### CleanupBurstInterval

Controls the delay between cleanup passes.

Recommended default:

```ini
CleanupBurstInterval = 0.2
```

## Pitfalls / lessons learned

### Do not only scan once

Forest Ambush objects can appear after scene load.

A single startup scan can miss them.

The current solution uses scene-loaded callbacks plus a lightweight scene signature watch loop.

### Do not scan every frame

Scanning all loaded scene roots every frame would be wasteful.

The mod instead checks a cheap scene signature periodically, then runs a short cleanup burst only when needed.

### Do not globally remove all ghosts or enemies

The Forest Ambush scare is tied to specific scene objects.

Removing all ghost-like enemies or all objects with scary names would be too broad and could break unrelated gameplay.

### Do not disable important encounter infrastructure

The mod intentionally avoids disabling:

```text
Trigger
TriggerSpawner
NavMeshAnchor
```

because those may be part of encounter or spawn logic.

### Do not rely on exact tree numbers only

Debug logs showed objects like `Tree1`, `Tree2`, `Tree4`, `Tree5`, `Tree6`, `Tree8`, and `Tree9`.

Instead of hardcoding every number, the mod disables child objects whose names start with `Tree`.

This is more robust if the game uses slightly different tree numbering.

## Difference from No Forest Falling Trees

No Forest Ambush is more direct.

It disables the `Contents` Animator and AudioSource, then disables tree and particle objects.

No Forest Falling Trees is the lighter / safer version.
It tries harder to keep the Forest Ambush timeline intact by not disabling the `Contents` Animator and by only hiding or neutralizing visual / physical tree elements.

Use this mod if you want stronger Forest Ambush scare suppression.

Use No Forest Falling Trees if you want a lighter approach that preserves more of the original event logic.

## Difference from No Ghost Scares

No Ghost Scares targets objects named like:

```text
Ability_Ghost_*_Scare
```

That is a different scare system.

No Forest Ambush targets Forest Ambush scene objects:

```text
Event_Forest_Ambush
Forest_Ambush
```

These mods solve different problems and should not be described as the same implementation.

## What it does not do

This mod does not:

* Remove all enemies
* Remove all ghost enemies
* Remove all scare objects
* Change player stats
* Change save data
* Edit original game files

It only neutralizes Forest Ambush scare presentation objects found in loaded scenes.

## Compatibility

This mod should be compatible with most mods.

Potential conflicts:

* Mods that modify Forest Ambush scene objects
* Mods that depend on the Forest Ambush `Contents` Animator
* Mods that change scene hierarchy timing

If another mod also edits the same Forest Ambush objects, behavior may depend on runtime order.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, and the required Unity / game assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.