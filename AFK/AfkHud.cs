#nullable enable
using UnityEngine;

namespace AFK;

/// <summary>
/// Persistent on-screen message drawn from AFK.OnGUI (IMGUI, the same mechanism
/// Game.OnGUI and FejdStartup.OnGUI use, so no Jotunn/GUIManager dependency). One line,
/// one spot (top center): IndicatorText while the status is Active, and - when enabled -
/// the "AFK: waiting - ..." condition message with its live countdown while the request
/// is still waiting for the quiet timers. Everything disappears the moment neither state
/// applies (AfkState.Active / Requested false).
///
/// The GUIStyle is cached but rebuilt when the configured font size changes, and GUI.color is
/// saved/restored because IMGUI state is shared with every other OnGUI user.
/// </summary>
internal static class AfkHud
{
    // Top center: below the top-left zone/status messages, well above the hotbar, and clear
    // of the vanilla TopLeft transition notifications (they fade, this one persists).
    private const float VerticalFraction = 0.15f;

    private static GUIStyle? s_style;
    private static int s_styleFontSize = -1;

    internal static void Draw()
    {
        if (!AfkConfig.IndicatorEnabled.Value)
        {
            return;
        }

        // Same spot and style for both lines: the granted status (IndicatorText), or -
        // while the request waits for the quiet timers - the pending message with its
        // live countdown. They never overlap: one state, one line.
        string? text = null;
        if (AfkState.Active)
        {
            text = AfkConfig.IndicatorText.Value;
        }
        else if (
            AfkConfig.IndicatorShowWaiting.Value
            && AfkState.Requested
            && AfkConfig.Enabled.Value
        )
        {
            text = AfkState.WaitingMessage();
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int fontSize = Mathf.Clamp(AfkConfig.IndicatorFontSize.Value, 8, 200);
        if (s_style == null || s_styleFontSize != fontSize)
        {
            // GUI.skin is only valid inside OnGUI - Draw is called from AFK.OnGUI.
            s_style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            s_styleFontSize = fontSize;
        }

        Color previousColor = GUI.color;
        GUI.color = ParseColor(AfkConfig.IndicatorColor.Value);
        GUI.Label(
            new Rect(0f, Screen.height * VerticalFraction, Screen.width, fontSize * 1.5f),
            text,
            s_style
        );
        GUI.color = previousColor;
    }

    /// <summary>Accepts "#RRGGBB" / "#RRGGBBAA", falls back to plain white.</summary>
    private static Color ParseColor(string value) =>
        ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
}
