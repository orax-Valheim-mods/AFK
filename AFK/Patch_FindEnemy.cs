#nullable enable
using System;
using HarmonyLib;

namespace AFK;

/// <summary>
/// BaseAI.FindEnemy() has a "hunt players" fallback that picks the closest player WITHOUT
/// any see/hear check - vanilla only drops that player when InGhostMode()/debug fly is on.
/// The mod's flag is on the ZDO, so the returned player is filtered here. The other branch
/// of FindEnemy (CanSenseTarget) is already covered by Patch_CanHearTarget /
/// Patch_CanSeeTarget.
/// </summary>
[HarmonyPatch(typeof(BaseAI), "FindEnemy")]
internal static class Patch_FindEnemy
{
    private static void Postfix(ref Character? __result)
    {
        try
        {
            if (__result is Player player && AfkState.IsGhost(player))
            {
                __result = null;
            }
        }
        catch (Exception ex)
        {
            AFK.LogError($"Patch BaseAI.FindEnemy: {ex}");
        }
    }
}
