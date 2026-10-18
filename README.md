# Description

AFK adds an **AFK status** you can toggle with a keybind (no key assigned **by default**) or by **sitting down** (the game's `Sit` action). The status is only granted while both quiet conditions
hold:

- you have not damaged an enemy for `SecondsSinceDamageDealt` (default 30 s) and
- no enemy has had aggro on you for `SecondsSinceAggro` (default 30 s).

While the status is active its effects apply (each one is a config toggle):

- **InvisibleToEnemies** (default on): creatures cannot see, hear or target you.
- **IgnoreEnemyDamage** (default on): hits from creatures deal no damage at all.

# Installation

Requires BepInExPack for Valheim.

**Install the mod on your client AND on the dedicated server**.

## Using a mod manager (recommended)

You can install the mod using Vortex, R2ModMan or Gale.

## Manual

Install BepInExPack for Valheim. Copy the DLL file to
`Steam\steamapps\common\Valheim\BepInEx\plugins\` (or to the server's `BepInEx\plugins\`).

# Configuration

`BepInEx\config\orax.AFK.cfg`

# Misc.

Source code: [GitHub repository](https://github.com/orax-Valheim-mods/)
