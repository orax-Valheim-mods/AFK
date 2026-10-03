#nullable enable
using System;
using HarmonyLib;

namespace AFK;

/// <summary>
/// Records "the local player just landed a hit on an enemy" - resets the
/// SecondsSinceDamageDealt condition. Same filters as NoAggroNoStaminaCost uses for its
/// attack grace period, so that only real fights count:
///  - corpse and PVP-off hits are discarded by the game, they are not combat;
///  - friendly tames / neutral creatures are no enemy (BaseAI.IsEnemy), so feeding the
///    wolves or punching a deer in passing does not restart the timer - hitting another
///    player does.
///
/// Character.Damage(HitData) is called on the machine that SIMULATES the attack (melee in
/// Attack.DoDamage, arrows in Projectile.OnHit, both call SetAttacker first), and the hit
/// carries its attacker as a ZDOID that resolves to our own player here - so this works in
/// single player AND on a client hitting a creature owned by the dedicated server.
/// Vanilla's PlayerStatType.EnemyHits cannot be used for this: it is incremented inside
/// RPC_Damage, which only ever runs on the machine owning the DAMAGED character. Trees,
/// rocks and buildings never reach this patch (they use IDestructible, not Character).
/// </summary>
[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
internal static class Patch_Damage
{
    private static void Prefix(Character __instance, HitData hit)
    {
        try
        {
            Player? local = Player.m_localPlayer;
            if (local == null || hit == null || !hit.HaveAttacker())
            {
                return;
            }
            if (__instance == local || hit.GetAttacker() != local)
            {
                return;
            }
            if (__instance.IsDead())
            {
                return; // Hitting a corpse: the RPC is still sent, but no fight is happening.
            }
            if (__instance is Player victim && !victim.IsPVPEnabled() && !hit.m_ignorePVP)
            {
                return; // Victim has PVP off: RPC_Damage blocks the hit, no damage lands.
            }
            if (__instance is Player || BaseAI.IsEnemy(local, __instance))
            {
                AfkState.OnLocalPlayerDealtDamage();
            }
        }
        catch (Exception ex)
        {
            AFK.LogError($"Patch Character.Damage: {ex}");
        }
    }
}
