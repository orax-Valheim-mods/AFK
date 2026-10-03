# Description

AFK adds an **AFK status** you can toggle with a keybind (**F8** by default) or by
**crouching**. The status is only granted while both quiet conditions hold:

- you have not damaged an enemy for `SecondsSinceDamageDealt` (default 60 s; hits on
  corpses, PVP-off players, friendly tames or neutral creatures do not count), and
- no enemy has had aggro on you for `SecondsSinceAggro` (default 30 s). Aggro uses the
  game's own combat signal - an *alerted* hostile holding you as its target, the same one
  that starts combat music; being hit by a character restarts the timer too. Creatures that
  merely notice or flee from you (deer, boar...) never block it.

While the status is active its effects apply (each one is a config toggle):

- **InvisibleToEnemies** (default on): creatures cannot see, hear or target you - the same
  checks the vanilla `ghost` command passes, but synchronized through your player ZDO, so
  it also works against the creature AI owned by a dedicated server or another client.
- **IgnoreEnemyDamage** (default off): hits from creatures deal no damage at all, so
  nothing can kill you while AFK. Damage from other players and from the environment still
  goes through.

On top of that, a **persistent message** (default "AFK", configurable text / size / color)
is displayed in the top center of the screen **for as long as the status lasts** - it
disappears the moment the status is released. Press **F9** (`IndicatorColorKey`) to choose
its color with Jotunn's interactive color picker: *Done* keeps the new color, *Cancel*
restores the previous one (the setting is saved to the config file). While the request is
still waiting for the quiet timers, the **same spot** shows the pending message with a
**live countdown** (`IndicatorShowWaiting`, default on): *"AFK: waiting - no damage dealt,
no enemy aggro (12s)"* - the seconds tick down and the line is replaced by the indicator
as soon as the status activates. The short top left messages announcing any change are
controlled separately by `Notify`.

Request the status whenever you want: if the quiet conditions don't hold yet (you just
finished a fight), a "waiting" message shows what is missing and for how long, and the
status **activates by itself** once the timers pass. Once active it is **never taken away
by combat events** - it lasts until you release it (keybind, crouch, movement / combat
input - always on - or death), or until the master switch is turned off. Every change is
announced by a short message in the top left corner (`Notify`).

# Configuration

`BepInEx\config\orax.AFK.cfg`

| Section | Setting | Default | Meaning |
|---|---|---|---|
| 1 - General | Enabled | true | Master switch. |
| 1 - General | Notify | true | Status messages in the top left corner. |
| 2 - Trigger | ToggleKey | F8 | Key (with optional Ctrl/Shift/Alt modifiers) that toggles the AFK request. |
| 2 - Trigger | CrouchTriggers | true | Crouch key also toggles the AFK request. |
| 3 - Conditions | SecondsSinceDamageDealt | 60 | Quiet time since your last hit on an enemy. |
| 3 - Conditions | SecondsSinceAggro | 30 | Quiet time since an alerted enemy last targeted you, or since a character hit you. |
| 4 - Effects | InvisibleToEnemies | true | Creatures cannot see, hear or target you (synced). |
| 4 - Effects | IgnoreEnemyDamage | false | Creature attacks deal no damage while AFK. |
| 5 - Display | IndicatorEnabled | true | Persistent message in the top center while AFK; gone when the status is released. |
| 5 - Display | IndicatorShowWaiting | true | Also show "AFK: waiting - … (12s)" with a live countdown in that same spot while the request waits. |
| 5 - Display | IndicatorText | AFK | Text of that message (empty = off). |
| 5 - Display | IndicatorFontSize | 40 | Font size of the message (clamped 8-200). |
| 5 - Display | IndicatorColor | #FFFFFFFF | Color of the message, HTML hex `#RRGGBB` / `#RRGGBBAA`. |
| 5 - Display | IndicatorColorKey | F9 | Opens the Jotunn color picker to choose `IndicatorColor` (Done saves, Cancel reverts). |

# Installation

Requires BepInExPack for Valheim and [Jotunn](https://valheim-modding.github.io/Jotunn/)
(hard dependency, provided automatically by mod managers through `manifest.json`).

**Install the mod on your client AND on the dedicated server** (and on other clients that
may own creature AI near you): creature AI runs on the machine owning the creature - the
server for anything around you - and only machines with this DLL read the synced AFK flag.
Without the server copy, invisibility still holds against AI owned by modded clients and
by you, but not against the server's own creatures. The two copies do not need to be
configured identically: `InvisibleToEnemies` acts as a per-machine switch on top of the
synced flag (keep it on everywhere for full invisibility).

## Using a mod manager (recommended)
You can install the mod using Vortex, R2ModMan or Gale.

## Manual
Install BepInExPack for Valheim.
Copy the DLL file to `Steam\steamapps\common\Valheim\BepInEx\plugins\`
(or to the server's `BepInEx\plugins\`).

# Misc.

Source code: [GitHub repository](https://github.com/orax-Valheim-mods/)
