# No Forest Ambush

## What it does

No Forest Ambush removes Forest Ambush events from SULFUR.

This is the stronger version of my forest scare removal mods.  
It is intended for players who want to remove the entire forest ambush event instead of only hiding the falling-tree presentation.

## Important difference from No Forest Falling Trees

This mod removes the whole Forest Ambush event.

That means it may remove:

- The falling tree scare
- Ambush visual effects
- Ambush audio
- Ambush trigger behavior
- Enemy / scare elements tied to that specific Forest Ambush event

If you only want to remove the falling-tree visual scare while keeping the ambush event logic mostly intact, use **No Forest Falling Trees** instead.

## Implementation overview

The mod looks for Forest Ambush event objects in loaded scenes.

The important target object names are:

- `Event_Forest_Ambush`
- `Forest_Ambush`

When a matching event object is found, the mod disables and removes that object.

The goal is to remove the whole event object, not just hide one renderer or mute one sound.

## Why this approach

Forest Ambush is represented by scene objects rather than a simple global enemy type.

The falling-tree scare is not caused by every Abomination or every ghost-like object in the game.  
It is tied to specific Forest Ambush event objects.

Removing the event object is the most direct way to make the Forest Ambush stop happening.

## What it does not do

This mod does not globally remove all enemies.

It does not remove:

- Normal Abominations
- Normal Ghost enemies
- Non-ambush enemy spawns
- Unrelated scare objects outside the Forest Ambush event

It only targets Forest Ambush event objects.

## Balance impact

This mod can reduce the difficulty or intensity of forest ambush encounters.

If you want a less invasive version that keeps the ambush event mostly intact, use **No Forest Falling Trees**.

## Configuration

Depending on the version, configuration may include:

- Enable / disable blocking
- Logging
- Scan interval

Debug logging should normally stay disabled unless you are testing object detection.

## Compatibility

This mod does not edit original game files.

It should be compatible with most mods.  
If another mod changes the same Forest Ambush event objects, behavior may depend on load order or runtime timing.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need:

- A legal copy of SULFUR
- BepInEx
- Harmony if the implementation uses Harmony patches
- The required Unity and game assemblies from your own local installation

Game files and third-party binaries are not included in this repository.