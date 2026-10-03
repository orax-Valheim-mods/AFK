#nullable enable
using System;
using HarmonyLib;
using UnityEngine;

namespace AFK;

/// <summary>
/// Creatures must not see an AFK player. Same reasoning as Patch_CanHearTarget: vanilla
/// bails out on `player.InGhostMode()`, a local-only field that never reaches the machine
/// running the AI, so the check is answered from the afk_ghost flag on the player's ZDO.
/// This static method is the one every line-of-sight check goes through (including
/// CanSenseTarget and FindClosestCreature).
/// </summary>
[HarmonyPatch(
    typeof(BaseAI),
    nameof(BaseAI.CanSeeTarget),
    new[]
    {
        typeof(Transform),
        typeof(Vector3),
        typeof(float),
        typeof(float),
        typeof(bool),
        typeof(bool),
        typeof(Character),
    }
)]
internal static class Patch_CanSeeTarget
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
            AFK.LogError($"Patch BaseAI.CanSeeTarget: {ex}");
            return true;
        }
    }
}
