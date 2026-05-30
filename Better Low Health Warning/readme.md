# Better Low Health Warning

## What it does

Better Low Health Warning replaces the default low-health feeling with a clearer red vignette warning.

When the player’s health drops below the configured threshold, the screen edges gradually turn red.  
At critical health, the warning becomes stronger and can pulse.

The goal is to make low health easier to notice without changing gameplay balance.

## Implementation overview

This mod uses BepInEx and Harmony.

It patches:

```text
PerfectRandom.Sulfur.Gameplay.PlayerHUD.Update()
````

with a Harmony Postfix.

The patch reads the player’s normalized health from the HUD’s player reference, then passes that value to a persistent overlay object.

The overlay is a separate `MonoBehaviour` created at startup:

```text
BetterLowHealthWarningOverlay
```

It is marked with `DontDestroyOnLoad`, so the visual warning can continue working across scene changes.

## Key game methods / fields used

The mod reads health through the HUD path:

```text
PlayerHUD
→ player
→ playerUnit
→ GetNormalizedHealth()
```

It also checks HUD visibility through:

```text
PlayerHUD.canvasGroup.alpha
```

If the HUD is hidden and `HideWhenHudHidden` is enabled, the low-health warning is hidden too.

## Why this approach

The mod does not edit player health, damage, healing, or stats.

It only reads the existing normalized health value and draws a visual overlay.

This keeps the mod low-risk:

* No gameplay values are changed.
* No save data is changed.
* No health logic is replaced.
* The original HUD still controls the actual health state.

Patching `PlayerHUD.Update()` is useful because the HUD already has access to the player reference and is updated regularly during gameplay.

## Visual implementation

The overlay is drawn with Unity IMGUI through `OnGUI()`.

It draws:

* A subtle fullscreen red tint
* A soft red border / vignette around the screen
* Optional pulsing based on current health severity
* Smooth fade in / fade out

The warning strength is calculated from two thresholds:

```text
WarningHealthPercent
CriticalHealthPercent
```

Example:

```ini
WarningHealthPercent = 0.35
CriticalHealthPercent = 0.15
```

This means:

* Above 35% HP: no warning
* Below 35% HP: warning starts
* At or below 15% HP: strongest warning

## Configuration

Config file:

```text
BepInEx/config/kumo.sulfur.better_low_health_warning.cfg
```

Main options:

```ini
[General]
EnableMod = true
HideWhenHudHidden = true

[Visual]
WarningHealthPercent = 0.35
CriticalHealthPercent = 0.15
BorderThickness = 160
MinOpacity = 0.08
MaxOpacity = 0.42
EnablePulse = true
PulseSpeed = 2.2
FadeSpeed = 10
FullscreenTintOpacity = 0.035

[Debug]
LogErrors = false
```

### EnableMod

Enables or disables the mod.

```ini
EnableMod = true
```

### HideWhenHudHidden

If enabled, the warning disappears when the game HUD is hidden.

```ini
HideWhenHudHidden = true
```

### WarningHealthPercent

Health percentage where the warning starts.

```ini
WarningHealthPercent = 0.35
```

### CriticalHealthPercent

Health percentage where the warning reaches maximum strength.

```ini
CriticalHealthPercent = 0.15
```

### BorderThickness

Controls how thick the red vignette border is.

```ini
BorderThickness = 160
```

### MinOpacity / MaxOpacity

Controls warning opacity.

```ini
MinOpacity = 0.08
MaxOpacity = 0.42
```

### EnablePulse

Enables pulsing at low health.

```ini
EnablePulse = true
```

### FullscreenTintOpacity

Adds a very subtle fullscreen red tint.

Set to `0` if you only want the screen-edge vignette.

```ini
FullscreenTintOpacity = 0.035
```

## Pitfalls / lessons learned

### Do not modify actual health values

This mod is purely visual.

The safest implementation is to read `GetNormalizedHealth()` and draw an overlay.
Changing health stats directly would turn this into a gameplay mod and increase the chance of conflicts.

### Do not depend on exact health numbers

The mod uses normalized health instead of current / max HP values.

This avoids needing to reverse engineer every health stat field and keeps the mod compatible with max-health changes.

### Do not assume the HUD is always visible

The game may hide or fade the HUD.
The mod checks `canvasGroup.alpha` so the warning can respect HUD visibility when configured.

### Keep error logging optional

The patch uses reflection to read fields such as `player`, `playerUnit`, and `canvasGroup`.

If a future game update changes these names, reflection may fail.
For normal gameplay, repeated errors should not spam the log, so `LogErrors` is disabled by default.

## What it does not do

This mod does not:

* Change player max health
* Change damage taken
* Change healing
* Change enemy behavior
* Edit save data
* Replace the original HUD
* Edit original game files

It only draws an additional low-health warning overlay.

## Compatibility

This mod should be compatible with most gameplay mods.

Potential conflicts:

* Mods that heavily replace `PlayerHUD`
* Mods that also patch `PlayerHUD.Update()`
* Mods that hide or destroy the normal HUD object

Because this mod only reads health and draws an overlay, compatibility risk is relatively low.

## Source notes

This folder contains the main source file only.

To compile it yourself, you need your own local SULFUR installation, BepInEx, Harmony, and the required game / Unity assemblies.

This repository does not include game files, Unity assemblies, BepInEx binaries, or decompiled game source.