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
/// old IMGUI label did. One line, one spot (top center): IndicatorText while the
/// status is Active, and - when enabled - the "AFK: waiting - ..." condition message
/// with its live countdown while the request still waits for the quiet timers.
///
/// The font follows IndicatorFont: empty keeps Valheim's own UI font, a name picks a
/// TMP font the game already has loaded (every loaded name is written once to the
/// BepInEx log) or, failing that, builds a runtime font from a system-installed font
/// of that name. The canvas is created lazily on the first shown frame, survives
/// scene loads (DontDestroyOnLoad) and is destroyed from AFK.OnDestroy so a hot
/// reload (F6) cannot leave an orphan label behind. The whole object is deactivated
/// whenever neither state applies (AfkState.Active / Requested false).
///
/// The label only writes to TMP when the text, font, size or color actually changed:
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
    private static bool s_fontsListed;

    // Font resolution cache: the trimmed IndicatorFont value last applied (null before
    // the first resolution), the font it resolved to, and - if a system font had to be
    // built at runtime - the asset to destroy when the setting changes again.
    private static string? s_fontQuery;
    private static TMP_FontAsset? s_font;
    private static TMP_FontAsset? s_createdFont;

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
        s_fontWarningLogged = false;
        s_fontsListed = false;
        s_fontQuery = null;
        s_font = null;
        s_createdFont = null;
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

        // Re-resolve when IndicatorFont is edited in the configuration UI.
        ApplyFont();

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
        ApplyFont();
        if (s_font == null)
        {
            if (!s_fontWarningLogged)
            {
                s_fontWarningLogged = true;
                AFK.LogWarning("No TMP font available - the indicator message stays hidden.");
            }
            return false;
        }

        ListFontsOnce();

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

    /// <summary>
    /// Resolves IndicatorFont when its value changed: empty = the game's own UI font,
    /// a name = a TMP font the game already loaded, else a system font built at
    /// runtime, else the game's font with a warning.
    /// </summary>
    private static void ApplyFont()
    {
        string? raw = AfkConfig.IndicatorFont.Value;
        string wanted = raw == null ? "" : raw.Trim();
        if (wanted == s_fontQuery)
        {
            return;
        }
        s_fontQuery = wanted;

        // A previously built system font must go once something else is used.
        TMP_FontAsset? oldCreated = s_createdFont;
        s_createdFont = null;

        TMP_FontAsset? resolved;
        if (wanted.Length == 0)
        {
            resolved = DefaultFont();
        }
        else
        {
            resolved = FindLoadedFont(wanted) ?? CreateSystemFont(wanted);
            if (resolved == null)
            {
                AFK.LogWarning(
                    $"Font \"{wanted}\" not found - the indicator keeps the game's own font."
                );
                resolved = DefaultFont();
            }
        }

        if (oldCreated != null && !ReferenceEquals(oldCreated, resolved))
        {
            Object.Destroy(oldCreated);
        }
        if (resolved == null)
        {
            return;
        }

        s_font = resolved;
        if (s_label != null)
        {
            s_label.font = resolved;
        }
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

    /// <summary>A TMP font asset the game already has loaded, by asset name.</summary>
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

    /// <summary>Builds a runtime TMP font from a system-installed font of that name.</summary>
    private static TMP_FontAsset? CreateSystemFont(string name)
    {
        Font osFont = Font.CreateDynamicFontFromOSFont(name, 90);
        if (osFont == null)
        {
            return null;
        }
        TMP_FontAsset? created = TMP_FontAsset.CreateFontAsset(osFont);
        if (created != null)
        {
            s_createdFont = created;
        }
        return created;
    }

    /// <summary>Writes the loaded font names to the BepInEx log, once, for discovery.</summary>
    private static void ListFontsOnce()
    {
        if (s_fontsListed)
        {
            return;
        }
        s_fontsListed = true;
        List<string> names = new();
        foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (asset != null)
            {
                names.Add(asset.name);
            }
        }
        AFK.LogInfo($"Loaded TMP fonts: {string.Join(", ", names)}");
    }

    /// <summary>Accepts "#RRGGBB" / "#RRGGBBAA", falls back to plain white.</summary>
    private static Color ParseColor(string value) =>
        ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
}
