#nullable enable
using UnityEngine;

namespace AFK;

/// <summary>
/// Persistent on-screen message shown while the AFK status is active - drawn from AFK.OnGUI
/// (IMGUI, the same mechanism Game.OnGUI and FejdStartup.OnGUI use, so no Jotunn/GUIManager
/// dependency) and therefore gone the moment AfkState.Active flips to false.
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
        if (!AfkState.Active || !AfkConfig.IndicatorEnabled.Value)
        {
            return;
        }

        string text = AfkConfig.IndicatorText.Value;
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
