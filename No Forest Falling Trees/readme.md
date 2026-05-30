# No Forest Falling Trees

## What it does

No Forest Falling Trees removes the falling-tree presentation from Forest Ambush events in SULFUR.

It is the lighter version of my forest scare removal mods.

The goal is:

- Hide the falling tree visuals
- Mute the falling-tree audio
- Stop related particles
- Remove tree collision
- Keep the Forest Ambush event structure as intact as possible

## Difference from No Forest Ambush

This mod does **not** remove the whole Forest Ambush event.

It only neutralizes the falling-tree presentation inside Forest Ambush objects.

If you want to remove the entire ambush event, use **No Forest Ambush** instead.

## Implementation overview

The mod scans loaded scenes for Forest Ambush root objects.

It looks for object names such as:

- `Event_Forest_Ambush`
- `Forest_Ambush`

When a Forest Ambush object is found, the mod looks for its `Contents` child.

Inside `Contents`, it neutralizes presentation objects such as:

- `Tree*`
- `Particles`
- Audio sources on the ambush contents

The implementation keeps the root event object active and avoids disabling important event components.

## Key behavior

The mod can:

- Hide Forest Ambush tree renderers
- Disable Forest Ambush tree colliders
- Disable `TreeShakeable` components on the ambush tree objects
- Stop and clear Forest Ambush particle systems
- Mute Ambush content audio sources without disabling the audio component

The code intentionally does not disable the whole `Contents` object or the whole Ambush root.

## Why this approach

Forest Ambush objects contain more than just the falling tree.

They can also contain trigger objects, spawner objects, navigation anchors, and event timeline logic.

The mod avoids disabling these objects directly because they may be needed for the original event flow.

In the current implementation, the mod specifically avoids disabling:

- `Contents` Animator
- `Trigger`
- `TriggerSpawner`
- `NavMeshAnchor`

This helps remove the scare presentation while reducing the chance of breaking the event logic. The uploaded source also comments that the `Contents` Animator may drive animation events required for spawning, so it is intentionally preserved. :contentReference[oaicite:2]{index=2}

## Runtime scanning

The mod does not rely on a single startup scan.

It runs cleanup bursts:

- On startup
- After a scene is loaded
- When the loaded scene hierarchy appears to change

This is because Forest Ambush objects may appear after the initial scene load.

The source uses scene-loaded callbacks, a scene signature watch loop, and repeated cleanup passes to catch newly added ambush objects. :contentReference[oaicite:3]{index=3}

## Configuration

Main options include:

- `LogActions`
  - Logs neutralized Forest Ambush visual objects.

- `MuteAmbushContentAudio`
  - Mutes AudioSource components on Forest Ambush contents.

- `HideTreeRenderers`
  - Hides Forest Ambush tree renderers.

- `DisableTreeColliders`
  - Disables colliders on Forest Ambush tree objects.

- `DisableTreeShakeable`
  - Disables `TreeShakeable` components on Forest Ambush tree objects.

- `StopParticles`
  - Stops and hides Forest Ambush particle effects.

- `SceneSignatureCheckInterval`
  - Controls how often the mod checks whether the loaded scene hierarchy changed.

- `CleanupBurstIterations`
  - Controls how many cleanup passes are run after a scene hierarchy change.

- `CleanupBurstInterval`
  - Controls the delay between cleanup passes.

## What it does not do

This mod does not globally remove enemies.

It does not remove:

- Normal Abominations
- Normal Ghost enemies
- Non-ambush enemies
- The entire Forest Ambush event object

It only targets the falling-tree presentation inside Forest Ambush objects.

## Balance impact

This mod is intended to be low-impact.

It removes the falling-tree scare presentation, but it tries to keep the ambush event structure and possible spawn logic intact.

For a stronger version that removes the whole Forest Ambush event, use **No Forest Ambush**.

## Compatibility

This mod does not edit original game files.

It should be compatible with most mods.  
If another mod changes the same Forest Ambush scene objects, behavior may depend on runtime order.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need:

- A legal copy of SULFUR
- BepInEx
- The required Unity and game assemblies from your own local installation

Game files and third-party binaries are not included in this repository.