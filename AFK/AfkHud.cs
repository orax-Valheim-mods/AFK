#nullable enable
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AFK;

/// <summary>
/// Persistent on-screen message, drawn as a TextMeshProUGUI label on its own
/// Screen Space Overlay canvas with a sortingOrder far below Valheim's own UI. The
/// vanilla HUD, inventory and menus are uGUI/TextMeshPro canvases rendered after it:
/// the message therefore sits BEHIND them (visible through translucent menu
/// backdrops, hidden behind opaque ones) instead of covering the menus the way the
/// old IMGUI label did. One line at a configurable spot: the nine-point anchor of
/// IndicatorAnchor placed at IndicatorPositionX / IndicatorPositionY (top center by
/// default). IndicatorText shows while the status is Active, and - when enabled - the
/// "AFK: waiting - ..." condition message with its live countdown while the request
/// still waits for the quiet timers.
///
/// The label follows IndicatorFont: empty keeps the game's own UI font in bold (the
/// message's classic look), otherwise a TMP font the game has loaded with the Bold /
/// Italic variant spelled out in the choice - the dropdown of choices is fed to the
/// config once the HUD exists. The canvas is created lazily on the first shown frame,
/// survives scene loads (DontDestroyOnLoad) and is destroyed from AFK.OnDestroy so a hot
/// reload (F6) cannot leave an orphan label behind. The whole object is deactivated
/// whenever neither state applies (AfkState.Active / Requested false).
///
/// The label only writes to TMP when the text, size or color actually changed:
/// assigning TMP text every frame would rebuild the mesh for nothing.
/// </summary>
internal static class AfkHud
{
    // Valheim's own canvases sort at 0 or above: staying well below puts the message
    // behind every menu, while a Screen Space Overlay canvas still draws over the world.
    private const int SortingOrder = -1000;

    private static GameObject? s_root;
    private static TextMeshProUGUI? s_label;
    private static string? s_shownText;
    private static int s_shownFontSize = -1;
    private static float s_boxWidth;
    private static Color s_shownColor = new(-1f, -1f, -1f, -1f);
    private static bool s_fontWarningLogged;
    private static bool s_fontChoicesDone;
    private static string? s_fontChoice;
    private static TMP_FontAsset? s_font;
    private static FontStyles s_fontStyle;

    /// <summary>Called once per frame from AFK.Update: show, refresh or hide the message.</summary>
    internal static void Sync()
    {
        OfferFontChoicesOnce();

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
        s_boxWidth = 0f;
        s_shownColor = new Color(-1f, -1f, -1f, -1f);
        s_fontWarningLogged = false;
        s_fontChoicesDone = false;
        s_fontChoice = null;
        s_font = null;
        s_fontStyle = FontStyles.Normal;
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

        ApplyFontChoice();
        if (s_label == null && !Create())
        {
            return;
        }

        int fontSize = Mathf.Clamp(AfkConfig.IndicatorFontSize.Value, 8, 200);
        Color color = ParseColor(AfkConfig.IndicatorColor.Value);

        bool textChanged = s_shownText != text;
        if (textChanged)
        {
            s_label!.text = text;
            s_shownText = text;
        }
        bool fontSizeChanged = s_shownFontSize != fontSize;
        if (fontSizeChanged)
        {
            s_label!.fontSize = fontSize;
            s_shownFontSize = fontSize;
        }
        if (s_shownColor != color)
        {
            s_label!.color = color;
            s_shownColor = color;
        }

        // One line wide (measured only when the text or the size changes), band height.
        if (textChanged || fontSizeChanged)
        {
            s_boxWidth = s_label!.GetPreferredValues(text).x;
        }

        // The chosen anchor point of that box sits exactly at (PositionX, PositionY) -
        // percents of the screen or pixels, y counted from the top like in CreatureStarAlert.
        RectTransform rect = s_label!.rectTransform;
        rect.sizeDelta = new Vector2(s_boxWidth, fontSize * 1.5f);
        AnchorFactor(AfkConfig.IndicatorAnchor.Value, out float anchorX, out float anchorY);
        float screenW = Screen.width;
        float screenH = Screen.height;
        float positionX;
        float positionY;
        if (
            string.Equals(
                AfkConfig.IndicatorPositionUnit.Value,
                AfkConfig.UnitPixels,
                System.StringComparison.OrdinalIgnoreCase
            )
        )
        {
            positionX = AfkConfig.IndicatorPositionX.Value;
            positionY = AfkConfig.IndicatorPositionY.Value;
        }
        else
        {
            positionX = screenW * AfkConfig.IndicatorPositionX.Value / 100f;
            positionY = screenH * AfkConfig.IndicatorPositionY.Value / 100f;
        }

        // uGUI anchors grow y upwards, the screen position counts y downwards.
        rect.anchorMin = rect.anchorMax = new Vector2(
            screenW > 0f ? positionX / screenW : 0.5f,
            screenH > 0f ? 1f - positionY / screenH : 0.85f
        );
        rect.pivot = new Vector2(anchorX, 1f - anchorY);
        rect.anchoredPosition = Vector2.zero;

        if (!s_root!.activeSelf)
        {
            s_root.SetActive(true);
        }
    }

    /// <summary>Builds the canvas and the label once. False = no font, message stays off.</summary>
    private static bool Create()
    {
        if (s_font == null)
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
        s_label.font = s_font;
        s_label.alignment = TextAlignmentOptions.Center;
        s_label.fontStyle = s_fontStyle;
        s_label.textWrappingMode = TextWrappingModes.NoWrap;
        s_label.overflowMode = TextOverflowModes.Overflow;
        s_label.raycastTarget = false;
        return true;
    }

    /// <summary>
    /// Feeds the config's IndicatorFont dropdown with the game's loaded fonts, once,
    /// on the first frame where the HUD exists (all of the game's fonts are in by then).
    /// </summary>
    private static void OfferFontChoicesOnce()
    {
        if (s_fontChoicesDone || Hud.instance == null)
        {
            return;
        }

        List<string> names = new();
        HashSet<string> seen = new(System.StringComparer.OrdinalIgnoreCase);
        foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (asset != null && asset.name.Length > 0 && seen.Add(asset.name))
            {
                names.Add(asset.name);
            }
        }
        if (names.Count == 0)
        {
            return;
        }

        names.Sort(System.StringComparer.OrdinalIgnoreCase);
        s_fontChoicesDone = true;
        AfkConfig.BindIndicatorFont(names.ToArray());
    }

    /// <summary>
    /// Applies IndicatorFont when its value changed: empty = the game's own UI font in
    /// bold (the message's classic look), otherwise a loaded game font with the Bold /
    /// Italic variant spelled out in the choice. Unknown names fall back with a warning.
    /// </summary>
    private static void ApplyFontChoice()
    {
        string choice = (AfkConfig.IndicatorFont?.Value ?? "").Trim();
        if (choice == s_fontChoice)
        {
            return;
        }
        s_fontChoice = choice;

        TMP_FontAsset? font;
        FontStyles style;
        if (choice.Length == 0)
        {
            font = DefaultFont();
            style = FontStyles.Bold;
        }
        else
        {
            // The name may be a font asset of its own, or a style variant like "Name Bold".
            font = FindLoadedFont(choice);
            style = FontStyles.Normal;
            if (font == null)
            {
                ParseStyle(choice, out string baseName, out FontStyles parsedStyle);
                font = FindLoadedFont(baseName);
                style = parsedStyle;
            }
            if (font == null)
            {
                AFK.LogWarning(
                    $"Font \"{choice}\" is not loaded by the game - using the game's own font."
                );
                font = DefaultFont();
                style = FontStyles.Bold;
            }
        }

        s_font = font;
        s_fontStyle = style;
        if (s_label != null && font != null)
        {
            s_label.font = font;
            s_label.fontStyle = style;
        }
    }

    /// <summary>Splits a variant choice like "Name Bold Italic" into its base font name and style.</summary>
    private static void ParseStyle(string choice, out string fontName, out FontStyles style)
    {
        if (choice.EndsWith(" Bold Italic", System.StringComparison.OrdinalIgnoreCase))
        {
            fontName = choice.Substring(0, choice.Length - " Bold Italic".Length);
            style = FontStyles.Bold | FontStyles.Italic;
        }
        else if (choice.EndsWith(" Bold", System.StringComparison.OrdinalIgnoreCase))
        {
            fontName = choice.Substring(0, choice.Length - " Bold".Length);
            style = FontStyles.Bold;
        }
        else if (choice.EndsWith(" Italic", System.StringComparison.OrdinalIgnoreCase))
        {
            fontName = choice.Substring(0, choice.Length - " Italic".Length);
            style = FontStyles.Italic;
        }
        else
        {
            fontName = choice;
            style = FontStyles.Normal;
        }
    }

    /// <summary>A TMP font asset the game has already loaded, by asset name.</summary>
    private static TMP_FontAsset? FindLoadedFont(string name)
    {
        foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (asset != null && asset.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }
        return null;
    }

    /// <summary>Valheim's HUD font while in game, TMP's configured default font otherwise.</summary>
    private static TMP_FontAsset? DefaultFont()
    {
        if (Hud.instance != null && Hud.instance.m_healthText != null)
        {
            TMP_FontAsset? hudFont = Hud.instance.m_healthText.font;
            if (hudFont != null)
            {
                return hudFont;
            }
        }
        return TMP_Settings.defaultFontAsset;
    }

    /// <summary>
    /// Turns the IndicatorAnchor name into the anchor point of the box: x grows right,
    /// y grows down (0 = top edge), the same convention as the screen position.
    /// </summary>
    private static void AnchorFactor(string? name, out float anchorX, out float anchorY)
    {
        switch (name)
        {
            case AfkConfig.AnchorTopLeft:
                anchorX = 0f;
                anchorY = 0f;
                break;
            case AfkConfig.AnchorTopCenter:
                anchorX = 0.5f;
                anchorY = 0f;
                break;
            case AfkConfig.AnchorTopRight:
                anchorX = 1f;
                anchorY = 0f;
                break;
            case AfkConfig.AnchorLeft:
                anchorX = 0f;
                anchorY = 0.5f;
                break;
            case AfkConfig.AnchorCenter:
                anchorX = 0.5f;
                anchorY = 0.5f;
                break;
            case AfkConfig.AnchorRight:
                anchorX = 1f;
                anchorY = 0.5f;
                break;
            case AfkConfig.AnchorBottomLeft:
                anchorX = 0f;
                anchorY = 1f;
                break;
            case AfkConfig.AnchorBottomCenter:
                anchorX = 0.5f;
                anchorY = 1f;
                break;
            case AfkConfig.AnchorBottomRight:
                anchorX = 1f;
                anchorY = 1f;
                break;
            default:
                // Anything unrecognised falls back to the default anchor (top center).
                anchorX = 0.5f;
                anchorY = 0f;
                break;
        }
    }

    /// <summary>Accepts "#RRGGBB" / "#RRGGBBAA", falls back to plain white.</summary>
    private static Color ParseColor(string value) =>
        ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
}
