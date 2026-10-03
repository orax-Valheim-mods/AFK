#nullable enable
using System;
using HarmonyLib;
using UnityEngine;

namespace AFK;

/// <summary>
/// Creatures must not hear an AFK player. Vanilla: BaseAI.CanHearTarget() bails out for
/// `player.InDebugFlyMode() || player.InGhostMode()`, and InGhostMode is the LOCAL,
/// never-synced field behind the admin-only "ghost" console command. The mod's flag lives
/// on the player's ZDO instead, so the same bail-out is added here - this static method is
/// the one every hearing check goes through (including CanSenseTarget).
/// </summary>
[HarmonyPatch(
    typeof(BaseAI),
    nameof(BaseAI.CanHearTarget),
    new[] { typeof(Transform), typeof(float), typeof(Character) }
)]
internal static class Patch_CanHearTarget
{
    private static bool Prefix(Character target, ref bool __result)
    {
        try
        {
            if (AfkState.IsGhost(target))
            {
                __result = false;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            AFK.LogError($"Patch BaseAI.CanHearTarget: {ex}");
            return true;
        }
    }
}
