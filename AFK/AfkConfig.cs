#nullable enable
using BepInEx.Configuration;
using UnityEngine;

namespace AFK;

/// <summary>
/// Every mod setting (BepInEx config file) plus their binding. The other classes read the
/// entries through AfkConfig.ToggleKey.Value and friends.
/// </summary>
internal static class AfkConfig
{
    // -- 1 - General -----------------------------------------------------------------
    internal static ConfigEntry<bool> Enabled = null!;

    internal static ConfigEntry<bool> Notify = null!;

    // -- 2 - Trigger -----------------------------------------------------------------
    internal static ConfigEntry<KeyboardShortcut> ToggleKey = null!;

    internal static ConfigEntry<bool> SitTriggers = null!;

    // -- 3 - Conditions ---------------------------------------------------------------
    internal static ConfigEntry<float> SecondsSinceDamageDealt = null!;

    internal static ConfigEntry<float> SecondsSinceAggro = null!;

    // -- 4 - Effects ------------------------------------------------------------------
    internal static ConfigEntry<bool> InvisibleToEnemies = null!;

    internal static ConfigEntry<bool> IgnoreEnemyDamage = null!;

    // -- 5 - Display ------------------------------------------------------------------
    internal static ConfigEntry<bool> IndicatorEnabled = null!;

    internal static ConfigEntry<bool> IndicatorShowWaiting = null!;

    internal static ConfigEntry<string> IndicatorText = null!;

    internal static ConfigEntry<int> IndicatorFontSize = null!;

    internal static ConfigEntry<string> IndicatorFont = null!;

    internal static ConfigEntry<string> IndicatorColor = null!;

    /// <summary>Binds every setting to the config file. Called once, from AFK.Awake.</summary>
    internal static void Bind(ConfigFile config)
    {
        Enabled = config.Bind(
            "1 - General",
            "Enabled",
            true,
            "Master switch. While false the AFK status can never be requested or granted."
        );

        Notify = config.Bind(
            "1 - General",
            "Notify",
            true,
            "Show a status message (top left) whenever the AFK status changes: "
                + "\"AFK: active\", \"AFK: waiting - ...\", \"AFK: paused (mod disabled)\", "
                + "\"AFK: off\"."
        );

        ToggleKey = config.Bind(
            "2 - Trigger",
            "ToggleKey",
            new KeyboardShortcut(KeyCode.None),
            "Key that toggles the AFK request (modifiers such as Ctrl/Shift are supported). "
                + "Polled through ZInput like the game polls its own bindings, and ignored "
                + "while typing in chat, the console, a menu or the inventory. Defaults to "
                + "Not set: sitting down (the Sit trigger) is then the only way to request "
                + "the status."
        );

        SitTriggers = config.Bind(
            "2 - Trigger",
            "SitTriggers",
            true,
            "Pressing the Sit key (X by default, gamepad supported) also toggles the AFK "
                + "request. The first press sits down and requests AFK, the second one "
                + "stands up and releases it. Movement or action input still releases the "
                + "status as usual, and so does the keybind."
        );

        SecondsSinceDamageDealt = config.Bind(
            "3 - Conditions",
            "SecondsSinceDamageDealt",
            30f,
            new ConfigDescription(
                "You only obtain the AFK status once you have NOT damaged any character for "
                    + "this many seconds (melee, arrows, bombs... - every hit you land "
                    + "counts). The timers start when you join a world. Allowed range: "
                    + "5 to 60 seconds.",
                new AcceptableValueRange<float>(5f, 60f)
            )
        );

        SecondsSinceAggro = config.Bind(
            "3 - Conditions",
            "SecondsSinceAggro",
            30f,
            new ConfigDescription(
                "You only obtain the AFK status once no enemy has had aggro on you for this "
                    + "many seconds. Aggro uses the game's own combat signal (the same one "
                    + "that starts combat music): an ALERTED hostile holding you as its "
                    + "target, re-sent about every 0.5 s while it lasts, and being hit by a "
                    + "character. Creatures that merely notice or flee from you (deer, "
                    + "boar...) never count. Allowed range: 5 to 60 seconds.",
                new AcceptableValueRange<float>(5f, 60f)
            )
        );

        InvisibleToEnemies = config.Bind(
            "4 - Effects",
            "InvisibleToEnemies",
            true,
            "While the AFK status is active, creatures cannot see, hear or target you: the "
                + "same checks the vanilla ghost command passes, but synchronized through the "
                + "afk_ghost flag on your player ZDO so they also apply to AI owned by the "
                + "dedicated server or another client. This machine's setting is a local "
                + "switch on top of that flag: install the DLL on the server and on every "
                + "client that can own creature AI near you, and keep this value identical "
                + "there (the server effectively has the final say for its own creatures)."
        );

        IgnoreEnemyDamage = config.Bind(
            "4 - Effects",
            "IgnoreEnemyDamage",
            true,
            "While the AFK status is active, hits whose attacker is a creature (greydwarf, "
                + "troll, boss...) deal no damage at all, so you cannot "
                + "be killed while AFK. Damage from other players and from the environment "
                + "(falling, fire) is NOT affected."
        );

        IndicatorEnabled = config.Bind(
            "5 - Display",
            "IndicatorEnabled",
            true,
            "Show a persistent message in the top center of the screen for as long as the "
                + "AFK status is active. It disappears the moment the status is released "
                + "(this is independent from the one-shot Notify messages)."
        );

        IndicatorShowWaiting = config.Bind(
            "5 - Display",
            "IndicatorShowWaiting",
            true,
            "While the request is waiting for the quiet timers, also show the pending "
                + "condition message with a live countdown, in the same spot as "
                + "IndicatorText and with its font size and color: "
                + "\"AFK: waiting - no damage dealt, no enemy aggro (12s)\" - the seconds "
                + "tick down and the line is replaced by IndicatorText as soon as the "
                + "status activates."
        );

        IndicatorText = config.Bind(
            "5 - Display",
            "IndicatorText",
            "AFK",
            "The text of that persistent message. Leave empty to disable it."
        );

        IndicatorFontSize = config.Bind(
            "5 - Display",
            "IndicatorFontSize",
            40,
            "Font size of the persistent message (clamped to 8-200)."
        );

        IndicatorFont = config.Bind(
            "5 - Display",
            "IndicatorFont",
            "",
            "Font of the persistent message. Leave empty to use the game's own UI font. "
                + "Otherwise write a font name: fonts already loaded by the game are tried "
                + "first (their names are listed in the BepInEx log), then fonts installed "
                + "on this computer (for example \"Consolas\" or \"Impact\"). Unknown "
                + "names keep the game's font."
        );

        IndicatorColor = config.Bind(
            "5 - Display",
            "IndicatorColor",
            "#FFFFFFFF",
            "Color of the persistent message as HTML hex: #RRGGBB or #RRGGBBAA "
                + "(the last two digits set the opacity). Invalid values fall back to white."
        );
    }
}
