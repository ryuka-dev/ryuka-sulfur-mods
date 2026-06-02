# Dynamic Pressure

You started an arms race.  
Now the enemies are joining it too.

Dynamic Pressure is a SULFUR mod that adds extra enemy pressure on top of the vanilla game. It does not replace vanilla spawns, reduce vanilla enemy count, or modify level generation directly. Instead, it monitors the current combat pressure and spawns extra enemies only when the pressure is considered too low.

## Status

This mod is currently experimental.

The pressure calculation appears to work, but the balance is not final. The default Nightmare setting may still feel less intense than expected. Use the debug overlay to check real-time pressure values, spawn decisions, and block reasons, then adjust the config to your preference.

## Features

- Real-time pressure monitoring
- Extra enemy spawning when pressure is too low
- Three pressure styles:
  - Light
  - Heavy
  - Nightmare
- Debug overlay
- Anti-loop protection
- Vanilla spawns are not reduced or replaced
- Current level enemy pool is used as the main spawn candidate source
- Spawn point filtering based on vanilla NPC spawn data

## How It Works

The mod reads the current game state and calculates pressure from:

- nearby hostile enemies
- enemies engaged with the player
- enemies targeting the player
- enemies that know the player's position
- current original enemy pressure
- current mod-spawned enemy pressure

If the current pressure is below the configured target, the mod may spawn additional enemies. If pressure is already high, the player is low on health, the player was recently damaged, no valid spawn point exists, or anti-loop protection triggers, the mod will not spawn anything.

## Important Config

```ini
[General]
EnableMod = true
EnableAutoSpawn = true
PressureStyle = 1
````

`EnableAutoSpawn` must be `true` for automatic spawning.

If `EnableAutoSpawn` is `false`, the mod can still show debug information, but it will not actively spawn enemies automatically.

## Debug Overlay

The debug overlay is useful for tuning and testing.

It shows:

* current pressure
* target pressure
* pressure deficit
* original hostile count
* mod-spawned hostile count
* spawn decision
* block reason
* last spawned enemy
* current mod impact

If the mod is not spawning enemies, check the overlay first. The `Reason` field usually explains why.

Common block reasons:

```text
PressureAlreadyHigh
NoEngagedOriginalHostiles
NoValidNpcSpawnPoint
RecentDamage
LowHealth
MaxModSpawnedAlive
NoOriginalKillProgress
SpawnBudgetPerOriginalExceeded
```

## Recommended Settings

### Light

```ini
[Style 1 - Light]
TargetPressure = 6
SpawnCooldown = 16
MaxSpawnPerWave = 1
MaxModSpawnedAlive = 2
MaxModSpawnedPerRoom = 3
MaxModSpawnedPerLevel = 8
```

### Heavy

```ini
[Style 2 - Heavy]
TargetPressure = 9
SpawnCooldown = 10
MaxSpawnPerWave = 1
MaxModSpawnedAlive = 4
MaxModSpawnedPerRoom = 5
MaxModSpawnedPerLevel = 16
```

### Nightmare

```ini
[Style 3 - Nightmare]
TargetPressure = 13
SpawnCooldown = 7
MaxSpawnPerWave = 2
MaxModSpawnedAlive = 6
MaxModSpawnedPerRoom = 8
MaxModSpawnedPerLevel = 28
```

If Nightmare feels too mild:

```ini
[Style 3 - Nightmare]
TargetPressure = 16
SpawnCooldown = 5
MaxSpawnPerWave = 2
MaxModSpawnedAlive = 8
MaxModSpawnedPerRoom = 10
MaxModSpawnedPerLevel = 36
```

For a more extreme test:

```ini
[Style 3 - Nightmare]
TargetPressure = 18
SpawnCooldown = 4
MaxSpawnPerWave = 3
MaxModSpawnedAlive = 10
MaxModSpawnedPerRoom = 14
MaxModSpawnedPerLevel = 45
```

## Anti-Loop Protection

The mod includes safeguards to avoid endless spawning.

```ini
[Anti Loop]
MinOriginalEngagedHostilesForSpawn = 1
CooldownAfterModKillOnly = 8
MaxSecondsWithoutOriginalKill = 30
MaxModSpawnsPerOriginalEngaged = 2
```

These settings prevent the mod from repeatedly spawning enemies when only a stuck or unreachable original enemy remains.

For a more aggressive Nightmare setup:

```ini
[Anti Loop]
MaxModSpawnsPerOriginalEngaged = 3
MaxSecondsWithoutOriginalKill = 40
CooldownAfterModKillOnly = 5
```

Do not raise these too much unless you specifically want a chaotic test setup.

## Spawn Distance

If the overlay often shows `NoValidNpcSpawnPoint`, increase the spawn distance range:

```ini
[Spawn]
SpawnDistanceMin = 8
SpawnDistanceMax = 60
```

For testing:

```ini
[Spawn]
SpawnDistanceMin = 6
SpawnDistanceMax = 120
```

A larger range makes it easier for the mod to find valid spawn points, but enemies may spawn farther away and take longer to reach the player.

## Suggested Tuning Process

1. Enable the debug overlay.
2. Play one or two levels.
3. Watch the pressure value and block reasons.
4. If pressure is often below target, raise `TargetPressure`.
5. If enemies do not appear often enough, lower `SpawnCooldown`.
6. If too few mod enemies remain alive, raise `MaxModSpawnedAlive`.
7. If rooms feel overcrowded, lower `MaxModSpawnedPerRoom`.
8. If Nightmare stops spawning too easily, raise `MaxModSpawnsPerOriginalEngaged` from `2` to `3`.

## Known Issues

* Balance is not final.
* Nightmare may still be too mild under the default values.
* Some rooms may not have valid spawn points close enough to the player.
* The mod intentionally stops spawning when pressure is already high.
* The mod intentionally stops spawning when the player is low on health or recently damaged.
* Spawned enemies may take time to reach the player if the valid spawn point is far away.

## Development Notes

This mod is designed to avoid replacing vanilla spawn systems.

The main runtime logic is:

```text
Vanilla enemies spawn normally
↓
Dynamic Pressure reads current pressure
↓
If pressure is too low and safety checks pass
↓
Spawn an extra enemy from the current level enemy pool
↓
Mark it as mod-spawned
↓
Report the player's position to its AI
```

The mod should not:

* reduce vanilla spawn count
* delete vanilla enemies
* replace level generation
* write mod-spawned enemies to save data
* force endless spawning when original enemies are stuck

## Source Code

This repository contains my original mod source code and packaging text.

It does not include:

* SULFUR game files
* Unity assemblies
* BepInEx binaries
* paid assets
* decompiled game source

The source code is shared for learning, reference, and transparency.

Other modders may study the implementation or use it as a reference for their own mods.