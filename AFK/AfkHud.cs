#nullable enable
using TMPro;
using UnityEngine;

namespace AFK;

/// <summary>
/// Persistent on-screen message, drawn as a TextMeshProUGUI label on its own
/// Screen Space Overlay canvas with a sortingOrder far below Valheim's own UI. The
/// vanilla HUD, inventory and menus are uGUI/TextMeshPro canvases rendered after it:
/// the message therefore sits BEHIND them (visible through translucent menu
/// backdrops, hidden behind opaque ones) instead of covering the menus the way the
/// old IMGUI label did. One line, one spot (top center): IndicatorText while the
/// status is Active, and - when enabled - the "AFK: waiting - ..." condition message
/// with its live countdown while the request still waits for the quiet timers.
///
/// The canvas is created lazily on the first shown frame, survives scene loads
/// (DontDestroyOnLoad) and is destroyed from AFK.OnDestroy so a hot reload (F6)
/// cannot leave an orphan label behind. The whole object is deactivated whenever
/// neither state applies (AfkState.Active / Requested false).
///
/// The label only writes to TMP when the text, font size or color actually changed:
/// assigning TMP text every frame would rebuild the mesh for nothing.
/// </summary>
internal static class AfkHud
{
    // Top center: below the top-left zone/status messages, well above the hotbar, and clear
    // of the vanilla TopLeft transition notifications (they fade, this one persists).
    private const float VerticalFraction = 0.15f;

    // Valheim's own canvases sort at 0 or above: staying well below puts the message
    // behind every menu, while a Screen Space Overlay canvas still draws over the world.
    private const int SortingOrder = -1000;

    private static GameObject? s_root;
    private static TextMeshProUGUI? s_label;
    private static string? s_shownText;
    private static int s_shownFontSize = -1;
    private static Color s_shownColor = new(-1f, -1f, -1f, -1f);
    private static bool s_fontWarningLogged;

    /// <summary>Called once per frame from AFK.Update: show, refresh or hide the message.</summary>
    internal static void Sync()
    {
        string? text = null;
        if (AfkConfig.IndicatorEnabled.Value)
        {
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
        }

        Show(string.IsNullOrEmpty(text) ? null : text);
    }

    /// <summary>Destroys the canvas and its label - called from AFK.OnDestroy.</summary>
    internal static void Reset()
    {
        if (s_root != null)
        {
            Object.Destroy(s_root);
        }
        s_root = null;
        s_label = null;
        s_shownText = null;
        s_shownFontSize = -1;
        s_shownColor = new Color(-1f, -1f, -1f, -1f);
    }

    private static void Show(string? text)
    {
        if (text == null)
        {
            if (s_root != null && s_root.activeSelf)
            {
                s_root.SetActive(false);
            }
            return;
        }

        if (s_label == null && !Create())
        {
            return;
        }

        int fontSize = Mathf.Clamp(AfkConfig.IndicatorFontSize.Value, 8, 200);
        Color color = ParseColor(AfkConfig.IndicatorColor.Value);

        if (s_shownText != text)
        {
            s_label!.text = text;
            s_shownText = text;
        }
        if (s_shownFontSize != fontSize)
        {
            s_label!.fontSize = fontSize;
            s_shownFontSize = fontSize;
        }
        if (s_shownColor != color)
        {
            s_label!.color = color;
            s_shownColor = color;
        }

        // Full-width band starting 15% down from the top - the spot the old IMGUI label
        // used, so the position on screen does not change.
        RectTransform rect = s_label!.rectTransform;
        rect.anchoredPosition = new Vector2(0f, -Screen.height * VerticalFraction);
        rect.sizeDelta = new Vector2(0f, fontSize * 1.5f);

        if (!s_root!.activeSelf)
        {
            s_root.SetActive(true);
        }
    }

    /// <summary>Builds the canvas and the label once. False = no font, message stays off.</summary>
    private static bool Create()
    {
        // Valheim's HUD font while in game, TMP's configured default font otherwise.
        TMP_FontAsset? font = null;
        if (Hud.instance != null && Hud.instance.m_healthText != null)
        {
            font = Hud.instance.m_healthText.font;
        }
        font ??= TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            if (!s_fontWarningLogged)
            {
                s_fontWarningLogged = true;
                AFK.LogWarning("No TMP font available - the indicator message stays hidden.");
            }
            return false;
        }

        s_root = new GameObject("AFK Indicator");
        Object.DontDestroyOnLoad(s_root);
        Canvas canvas = s_root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(s_root.transform, false);
        s_label = labelObject.AddComponent<TextMeshProUGUI>();
        s_label.font = font;
        s_label.alignment = TextAlignmentOptions.Center;
        s_label.fontStyle = FontStyles.Bold;
        s_label.textWrappingMode = TextWrappingModes.NoWrap;
        s_label.overflowMode = TextOverflowModes.Overflow;
        s_label.raycastTarget = false;

        RectTransform rect = s_label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        return true;
    }

    /// <summary>Accepts "#RRGGBB" / "#RRGGBBAA", falls back to plain white.</summary>
    private static Color ParseColor(string value) =>
        ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
}
