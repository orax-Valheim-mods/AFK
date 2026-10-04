#nullable enable
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AFK;

/// <summary>
/// Every mod setting (BepInEx config file) plus their binding. The other classes read the
/// entries through AfkConfig.ToggleKey.Value and friends.
/// </summary>
internal static class AfkConfig
{
    // Anchor names: one source of truth for the IndicatorAnchor acceptable values and
    // the parser in AfkHud - the nine points of a three by three grid.
    internal const string AnchorTopLeft = "TopLeft";
    internal const string AnchorTopCenter = "TopCenter";
    internal const string AnchorTopRight = "TopRight";
    internal const string AnchorLeft = "Left";
    internal const string AnchorCenter = "Center";
    internal const string AnchorRight = "Right";
    internal const string AnchorBottomLeft = "BottomLeft";
    internal const string AnchorBottomCenter = "BottomCenter";
    internal const string AnchorBottomRight = "BottomRight";

    internal const string UnitPercent = "Percent";
    internal const string UnitPixels = "Pixels";

    // Highest accepted IndicatorPositionX / IndicatorPositionY (pixels of an 8K screen).
    internal const int MaxScreenCoordinate = 10000;

    // Default IndicatorFont: Valheim's own UI font asset, in bold - the message's
    // classic look, as an explicit choice instead of an empty value.
    internal const string DefaultIndicatorFont = "Valheim-AveriaSansLibre Bold";

    // Kept for the late IndicatorFont bind: the game's fonts are only known after
    // BepInEx has already called Bind at Awake.
    private static ConfigFile? s_config;

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

    // Bound late - see BindIndicatorFont: the dropdown can only list the fonts the game
    // has loaded, which is too late for BepInEx's Awake. Null until then.
    internal static ConfigEntry<string>? IndicatorFont;

    internal static ConfigEntry<string> IndicatorColor = null!;

    internal static ConfigEntry<string> IndicatorAnchor = null!;

    internal static ConfigEntry<int> IndicatorPositionX = null!;

    internal static ConfigEntry<int> IndicatorPositionY = null!;

    internal static ConfigEntry<string> IndicatorPositionUnit = null!;

    /// <summary>Binds every setting to the config file. Called once, from AFK.Awake.</summary>
    internal static void Bind(ConfigFile config)
    {
        s_config = config;

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

        IndicatorColor = config.Bind(
            "5 - Display",
            "IndicatorColor",
            "#FFFFFFFF",
            "Color of the persistent message as HTML hex: #RRGGBB or #RRGGBBAA "
                + "(the last two digits set the opacity). Invalid values fall back to white."
        );

        IndicatorAnchor = config.Bind(
            "5 - Display",
            "IndicatorAnchor",
            AnchorTopCenter,
            new ConfigDescription(
                "Point of the message placed at the position: TopLeft puts its top left "
                    + "corner there, Center its centre, and so on. Changing it barely moves "
                    + "the message (by its own size at most): use IndicatorPositionX and "
                    + "IndicatorPositionY to move it on screen.",
                new AcceptableValueList<string>(
                    AnchorTopLeft,
                    AnchorTopCenter,
                    AnchorTopRight,
                    AnchorLeft,
                    AnchorCenter,
                    AnchorRight,
                    AnchorBottomLeft,
                    AnchorBottomCenter,
                    AnchorBottomRight
                )
            )
        );

        IndicatorPositionX = config.Bind(
            "5 - Display",
            "IndicatorPositionX",
            50,
            new ConfigDescription(
                "Horizontal position of the anchor: percent of the screen width "
                    + "(0 = left edge, 100 = right edge), or pixels from the left edge "
                    + "when the unit is Pixels.",
                new AcceptableValueRange<int>(0, MaxScreenCoordinate)
            )
        );

        IndicatorPositionY = config.Bind(
            "5 - Display",
            "IndicatorPositionY",
            15,
            new ConfigDescription(
                "Vertical position of the anchor: percent of the screen height "
                    + "(0 = top edge, 100 = bottom edge), or pixels from the top edge "
                    + "when the unit is Pixels.",
                new AcceptableValueRange<int>(0, MaxScreenCoordinate)
            )
        );

        IndicatorPositionUnit = config.Bind(
            "5 - Display",
            "IndicatorPositionUnit",
            UnitPercent,
            new ConfigDescription(
                "Whether IndicatorPositionX and IndicatorPositionY are percents of the "
                    + "screen or pixels.",
                new AcceptableValueList<string>(UnitPercent, UnitPixels)
            )
        );
    }

    /// <summary>
    /// Binds IndicatorFont once the game's own fonts are known (AfkHud calls this on
    /// the first frame where the HUD exists): the acceptable values become the config
    /// editor's dropdown - every loaded font, each as regular, Bold, Italic and Bold
    /// Italic - and BepInEx writes them into the config file with the entry, refreshing
    /// them each session. The default is Valheim's own UI font in bold
    /// (DefaultIndicatorFont); the empty first value keeps the game's own font as a
    /// choice and is the value an unknown choice clamps back to.
    /// </summary>
    internal static void BindIndicatorFont(string[] fontNames)
    {
        if (IndicatorFont != null || s_config == null || fontNames.Length == 0)
        {
            return;
        }

        List<string> choices = new();
        HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
        void Add(string value)
        {
            if (seen.Add(value))
            {
                choices.Add(value);
            }
        }

        // Empty = the game's own font; also where an unloaded name clamps back to.
        Add("");
        foreach (string name in fontNames)
        {
            // An asset whose name already says bold or italic needs no such variant.
            bool bold = name.EndsWith("Bold", System.StringComparison.OrdinalIgnoreCase);
            bool italic = name.EndsWith("Italic", System.StringComparison.OrdinalIgnoreCase);
            Add(name);
            if (!bold)
            {
                Add(name + " Bold");
            }
            if (!italic)
            {
                Add(name + " Italic");
            }
            if (!bold && !italic)
            {
                Add(name + " Bold Italic");
            }
        }

        // The default must stay a valid choice even if the game ever renames its font.
        Add(DefaultIndicatorFont);

        IndicatorFont = s_config.Bind(
            "5 - Display",
            "IndicatorFont",
            DefaultIndicatorFont,
            new ConfigDescription(
                "Font of the persistent message. Leave empty to use the game's own UI "
                    + "font, in bold. Otherwise pick one of the fonts the game has loaded "
                    + "- each font is offered as regular, Bold, Italic and Bold Italic. "
                    + "The choices are refreshed in this file each time the game enters "
                    + "a world.",
                new AcceptableValueList<string>(choices.ToArray())
            )
        );
        AFK.LogInfo($"IndicatorFont choices: {string.Join(", ", choices)}");
    }
}
