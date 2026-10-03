#nullable enable
using System;
using HarmonyLib;

namespace AFK;

/// <summary>
/// Character.RPC_Damage is routed to the machine OWNING the damaged character - for the
/// local player that is this machine (players own their own ZDO), which makes it the one
/// place that sees incoming damage from the victim's point of view in every topology:
///
///  - Any hit from a character other than ourselves restarts the SecondsSinceAggro
///    condition: whatever happened, the player was not safely AFK. Deliberately broader
///    than BaseAI.IsEnemy (as used by NoAggroNoStaminaCost for stamina): for us a false
///    positive only delays AFK, while a false negative could leave an invisible player in
///    a real fight. Damage without a character attacker (falling, fire) does not count -
///    it is not aggro.
///  - With IgnoreEnemyDamage active, a hit whose attacker is a creature is skipped
///    entirely (the whole damage pipeline), so nothing can kill an AFK player. Hits from
///    other players and environmental hits still go through. The aggro reset above happens
///    BEFORE any skipping.
/// </summary>
[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class Patch_RPCDamage
{
    private static bool Prefix(Character __instance, HitData hit)
    {
        try
        {
            Player? local = Player.m_localPlayer;
            if (local == null || __instance != local || hit == null)
            {
                return true;
            }

            Character? attacker = hit.m_attacker.IsNone() ? null : hit.GetAttacker();
            if (attacker != null && attacker != local)
            {
                AfkState.OnLocalPlayerTookDamage();
            }

            if (!AfkState.Active || !AfkConfig.IgnoreEnemyDamage.Value)
            {
                return true;
            }
            return attacker == null || attacker.IsPlayer();
        }
        catch (Exception ex)
        {
            AFK.LogError($"Patch Character.RPC_Damage: {ex}");
            return true;
        }
    }
}
