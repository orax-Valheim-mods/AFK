#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
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
/// The font follows IndicatorFont: empty keeps Valheim's own UI font, a name picks a
/// TMP font the game already has loaded or, failing that, builds a runtime font from a
/// system-installed font of that name. Every usable name - the game's fonts and the
/// fonts installed on this computer - is listed in orax.AFK.Fonts.txt next to the
/// config file, written once per launch as soon as the HUD exists. The canvas is
/// created lazily on the first shown frame, survives
/// scene loads (DontDestroyOnLoad) and is destroyed from AFK.OnDestroy so a hot
/// reload (F6) cannot leave an orphan label behind. The whole object is deactivated
/// whenever neither state applies (AfkState.Active / Requested false).
///
/// The label only writes to TMP when the text, font, size or color actually changed:
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
    private static bool s_fontListWritten;

    // Font resolution cache: the trimmed IndicatorFont value last applied (null before
    // the first resolution), the font it resolved to, and - if a system font had to be
    // built at runtime - the asset to destroy when the setting changes again.
    private static string? s_fontQuery;
    private static TMP_FontAsset? s_font;
    private static TMP_FontAsset? s_createdFont;

    /// <summary>Called once per frame from AFK.Update: show, refresh or hide the message.</summary>
    internal static void Sync()
    {
        WriteFontListOnce();

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
        s_fontListWritten = false;
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

    /// <summary>
    /// Writes every font usable with IndicatorFont to a text file next to the config
    /// file - the game's loaded fonts first, then the fonts installed on this computer -
    /// once per launch, as soon as the HUD exists so the game's own fonts are in. The
    /// loaded names also go to the BepInEx log, for discovery.
    /// </summary>
    private static void WriteFontListOnce()
    {
        if (s_fontListWritten || Hud.instance == null)
        {
            return;
        }
        s_fontListWritten = true;

        try
        {
            List<string> gameFonts = new();
            foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (asset != null && asset.name.Length > 0)
                {
                    gameFonts.Add(asset.name);
                }
            }
            gameFonts.Sort(System.StringComparer.OrdinalIgnoreCase);
            AFK.LogInfo($"Loaded TMP fonts: {string.Join(", ", gameFonts)}");

            string[] installed = Font.GetOSInstalledFontNames() ?? new string[0];
            List<string> systemFonts = new(installed);
            systemFonts.Sort(System.StringComparer.OrdinalIgnoreCase);

            string path = Path.Combine(Paths.ConfigPath, AFK.PluginGUID + ".Fonts.txt");
            List<string> lines =
                new()
                {
                    "# Fonts usable with IndicatorFont - written by the AFK mod each time it loads.",
                    "# Set IndicatorFont to one of these names, or leave it empty for the game's own font.",
                    "# The game's fonts are tried first, then the fonts installed on this computer:",
                    "# a name present in both lists uses the game's font.",
                    "",
                    "## Fonts loaded by the game",
                };
            lines.AddRange(gameFonts.Distinct(System.StringComparer.OrdinalIgnoreCase));
            lines.Add("");
            lines.Add("## Fonts installed on this computer");
            lines.AddRange(systemFonts.Distinct(System.StringComparer.OrdinalIgnoreCase));
            File.WriteAllLines(path, lines, Encoding.UTF8);
            AFK.LogInfo($"IndicatorFont choices written to {path}");
        }
        catch (System.Exception ex)
        {
            AFK.LogWarning($"Could not write the font list: {ex.Message}");
        }
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
