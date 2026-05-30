# Ryuka SULFUR Mods

This repository contains my BepInEx mods for **SULFUR**.

The code is shared mainly for learning, reference, and transparency.  
Each folder contains the source code and notes for one mod.

## About

These mods are built with BepInEx and Harmony.

Most of them were made by reverse engineering specific gameplay systems, then patching or extending the original game logic as lightly as possible.

The repository is not meant to redistribute the game or any third-party binaries.  
It only contains my own mod source code, documentation, and packaging notes.

## Repository Structure

Each mod has its own folder.

A typical folder may contain:

```text
Plugin.cs
README.md
````

The folder README usually explains:

* What the mod does
* Which game systems or methods it interacts with
* Why the implementation was done that way
* Known pitfalls or reverse engineering notes
* Compatibility notes for other modders

Some folders may include packaging text for Thunderstore or Nexus Mods.

## For Modders

This repository is meant to be useful for other modders who want to understand how these mods work.

The README files are written not only as user documentation, but also as implementation notes.
They may include failed approaches, debugging lessons, and reasons why certain patch points were chosen.

Common topics include:

* Harmony patching
* Reflection against private game members
* Unity scene object scanning
* InputAction-based input handling
* Runtime HUD drawing
* Damage / status / durability hooks
* Avoiding overly broad patches
* Compatibility with other mods

## Building

These folders are not guaranteed to be complete standalone Visual Studio projects.

To compile the mods yourself, you need your own local setup, including:

* A legal copy of SULFUR
* BepInEx
* Harmony
* Required Unity assemblies from your own game installation
* Required game assemblies from your own game installation

This repository does **not** include those dependencies.

Depending on the mod, you may need to create your own `.csproj` file or adapt the source file into your existing BepInEx mod project.

## What Is Not Included

This repository does not include:

* SULFUR game files
* Unity engine assemblies
* BepInEx binaries
* Harmony binaries
* Decompiled game source code
* Paid assets
* Copyrighted game assets
* Assets from other games

Only my original mod source code and documentation are included.

## Notes on Reverse Engineering

Some mods rely on game method names, private fields, scene object names, or enum values discovered during development.

Because of this, game updates may break some mods if the internal implementation changes.

If a mod stops working after a game update, the likely causes are:

* A method was renamed
* A private field was renamed
* A method signature changed
* A scene object hierarchy changed
* A relevant enum value changed
* The original game logic moved to a different system

The folder README files usually explain the important assumptions for each mod.

## License

Unless otherwise stated, the original source code in this repository is released under the MIT License.

This license only applies to my own code and documentation in this repository.

SULFUR, Unity, BepInEx, Harmony, and any referenced third-party assets belong to their respective owners.