# Description

AFK adds an **AFK status** you can toggle with a keybind (no key assigned **by default**)
or by **sitting down** (the game's `Sit` action, **X** by default). The status is only
granted while both quiet conditions hold:

- you have not damaged an enemy for `SecondsSinceDamageDealt` (default 30 s, allowed
  range **5-60 s**; hits on corpses, PVP-off players, friendly tames or neutral creatures
  do not count), and
- no enemy has had aggro on you for `SecondsSinceAggro` (default 30 s, allowed range
  **5-60 s**). Aggro uses the
  game's own combat signal - an *alerted* hostile holding you as its target, the same one
  that starts combat music; being hit by a character restarts the timer too. Creatures that
  merely notice or flee from you (deer, boar...) never block it.

While the status is active its effects apply (each one is a config toggle):

- **InvisibleToEnemies** (default on): creatures cannot see, hear or target you - the same
  checks the vanilla `ghost` command passes, but synchronized through your player ZDO, so
  it also works against the creature AI owned by a dedicated server or another client.
- **IgnoreEnemyDamage** (default on): hits from creatures deal no damage at all, so
  nothing can kill you while AFK. Damage from other players and from the environment still
  goes through.

On top of that, a **persistent message** (default "AFK", configurable text / size / font /
color) is displayed in the top center of the screen **for as long as the status lasts** - it
disappears the moment the status is released. It is drawn **behind the game's own UI**
(menus, inventory, map): opening one leaves the message in the background instead of on
top of the interface. Its spot is configurable with a nine-point anchor
(`IndicatorAnchor`, default `TopCenter`) placed at `IndicatorPositionX` /
`IndicatorPositionY` (percents of the screen, or pixels with `IndicatorPositionUnit`).
The font comes from `IndicatorFont`: a dropdown of every font the game has loaded, each
offered as regular / Bold / Italic / Bold Italic (empty = the game's own UI font, in
bold; the choices are refreshed in the config file each time the game enters a world).
While the request is
still waiting for the quiet timers, the **same spot** shows the pending message with a
**live countdown** (`IndicatorShowWaiting`, default on): *"AFK: waiting - no damage dealt,
no enemy aggro (12s)"* - the seconds tick down and the line is replaced by the indicator
as soon as the status activates. The short top left messages announcing any change are
controlled separately by `Notify`.

Request the status whenever you want: if the quiet conditions don't hold yet (you just
finished a fight), a "waiting" message shows what is missing and for how long, and the
status **activates by itself** once the timers pass. Once active it is **never taken away
by combat events** - it lasts until you release it (keybind, sit, movement / combat
input - always on - or death), or until the master switch is turned off. Every change is
announced by a short message in the top left corner (`Notify`).

# Configuration

`BepInEx\config\orax.AFK.cfg`

| Section | Setting | Default | Meaning |
|---|---|---|---|
| 1 - General | Enabled | true | Master switch. |
| 1 - General | Notify | true | Status messages in the top left corner. |
| 2 - Trigger | ToggleKey | (not set) | Key (with optional Ctrl/Shift/Alt modifiers) that toggles the AFK request; no key by default, the Sit trigger is then the only one. |
| 2 - Trigger | SitTriggers | true | Sit key (**X** by default) also toggles the AFK request. |
| 3 - Conditions | SecondsSinceDamageDealt | 30 | Quiet time since your last hit on an enemy (5-60 s). |
| 3 - Conditions | SecondsSinceAggro | 30 | Quiet time since an alerted enemy last targeted you, or since a character hit you (5-60 s). |
| 4 - Effects | InvisibleToEnemies | true | Creatures cannot see, hear or target you (synced). |
| 4 - Effects | IgnoreEnemyDamage | true | Creature attacks deal no damage while AFK. |
| 5 - Display | IndicatorEnabled | true | Persistent message in the top center while AFK; gone when the status is released. |
| 5 - Display | IndicatorShowWaiting | true | Also show "AFK: waiting - … (12s)" with a live countdown in that same spot while the request waits. |
| 5 - Display | IndicatorText | AFK | Text of that message (empty = off). |
| 5 - Display | IndicatorFontSize | 40 | Font size of the message (clamped 8-200). |
| 5 - Display | IndicatorFont | (empty) | Font of the message: empty = the game's own UI font (bold); otherwise a dropdown of every font the game has loaded, with a regular / Bold / Italic / Bold Italic choice for each (the list fills the config file after a world load). |
| 5 - Display | IndicatorColor | #FFFFFFFF | Color of the message, HTML hex `#RRGGBB` / `#RRGGBBAA`. |
| 5 - Display | IndicatorAnchor | TopCenter | Which point of the message sits at the position: TopLeft, TopCenter, TopRight, Left, Center, Right, BottomLeft, BottomCenter, BottomRight. |
| 5 - Display | IndicatorPositionX | 50 | Horizontal position of that anchor: percent of the screen width (0 = left, 100 = right), or pixels with the Pixels unit. |
| 5 - Display | IndicatorPositionY | 15 | Vertical position: percent of the screen height (0 = top, 100 = bottom), or pixels. |
| 5 - Display | IndicatorPositionUnit | Percent | Whether the two positions are percents of the screen or pixels. |

# Installation

Requires BepInExPack for Valheim.

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
