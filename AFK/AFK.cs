#nullable enable
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace AFK;

/// <summary>
/// AFK - grants the local player an "AFK status" when the configured keybind is pressed or
/// the player crouches, but only while both quiet conditions hold: no damage dealt to a
/// character and no enemy aggro on the player, each for a configurable time.
///
/// Multiplayer: the vanilla "ghost" console command only writes the LOCAL field
/// Player.m_ghostMode, which never reaches the machine that runs the creature AI. Creature
/// AI runs on the OWNER of the creature ZDO (a dedicated server or another client), so the
/// status is published as a bool on the player's own ZDO instead (AfkState.GhostKey) and
/// read back inside the BaseAI sense checks on every machine running this DLL.
///
/// Player.m_localPlayer is null on a dedicated server: AfkState.Tick then only resets its
/// state, the effect patches simply never see the flag there for a local player.
/// </summary>
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
internal class AFK : BaseUnityPlugin
{
    public const string PluginGUID = "orax.AFK";
    public const string PluginName = "AFK";
    public const string PluginVersion = "0.1.0";

    internal static Harmony? PatchHarmony;

    private static AFK? s_instance;

    // -- Logging: the plugin instance stays private, every other class logs through these -
    internal static void LogDebug(string message) => s_instance?.Logger.LogDebug(message);

    internal static void LogInfo(string message) => s_instance?.Logger.LogInfo(message);

    internal static void LogWarning(string message) => s_instance?.Logger.LogWarning(message);

    internal static void LogError(string message) => s_instance?.Logger.LogError(message);

    // ------------------------------------------------------------------ lifecycle

    private void Awake()
    {
        s_instance = this;

        AfkConfig.Bind(Config);

        PatchHarmony = new Harmony(PluginGUID);
        PatchHarmony.PatchAll(Assembly.GetExecutingAssembly());

        Logger.LogInfo(
            $"{PluginName} {PluginVersion} loaded - toggle: {AfkConfig.ToggleKey.Value}, "
                + $"crouch trigger: {AfkConfig.CrouchTriggers.Value}, "
                + $"invisible: {AfkConfig.InvisibleToEnemies.Value}, "
                + $"no damage: {AfkConfig.IgnoreEnemyDamage.Value}"
        );
    }

    private void OnDestroy()
    {
        AfkState.Shutdown();
        PatchHarmony?.UnpatchSelf();
        PatchHarmony = null;
        s_instance = null;
    }

    private void Update()
    {
        AfkState.Tick();
    }

    private void OnGUI()
    {
        AfkHud.Draw();
    }
}
