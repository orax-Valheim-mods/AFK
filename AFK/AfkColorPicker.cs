#nullable enable
using System;
using Jotunn.GUI;
using UnityEngine;

namespace AFK;

/// <summary>
/// Interactive color chooser for IndicatorColor, built on Jotunn's ColorPicker (Jotunn is
/// a hard dependency of the mod, see the BepInDependency on AFK). The picker itself is a
/// scene singleton created by Jotunn; this class only calls ColorPicker.Create with the
/// current setting and reacts to its two events:
///
///  - onColorChanged fires on every adjustment AND once more with the restored color when
///    Cancel is pressed - writing the config value live therefore gives an instant preview
///    (AfkHud reads it every frame) and an automatic revert on Cancel;
///  - onColorSelected fires once when Done or Cancel closes the picker: the value is then
///    written to the config file so the choice survives a restart.
///
/// The opening key (IndicatorColorKey) is polled from AfkState.Tick. Create() toggles:
/// pressing the key while the picker is open closes it through Jotunn's Done().
/// </summary>
internal static class AfkColorPicker
{
    internal static void Open()
    {
        try
        {
            ColorPicker.Create(
                ReadCurrentColor(),
                "AFK indicator color",
                OnColorChanged,
                OnColorSelected,
                useAlpha: true
            );
        }
        catch (Exception ex)
        {
            AFK.LogError($"Color picker: {ex}");
        }
    }

    private static Color ReadCurrentColor()
    {
        return ColorUtility.TryParseHtmlString(AfkConfig.IndicatorColor.Value, out Color color)
            ? color
            : Color.white;
    }

    private static void OnColorChanged(Color color)
    {
        AfkConfig.IndicatorColor.Value = ColorUtility.ToHtmlStringRGBA(color);
    }

    private static void OnColorSelected(Color color)
    {
        AfkConfig.IndicatorColor.Value = ColorUtility.ToHtmlStringRGBA(color);
        AfkConfig.Save();
        AFK.LogInfo($"AFK indicator color set to {AfkConfig.IndicatorColor.Value}");
    }
}
