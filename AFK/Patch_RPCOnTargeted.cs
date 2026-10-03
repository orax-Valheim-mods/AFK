#nullable enable
using System;
using HarmonyLib;

namespace AFK;

/// <summary>
/// Aggro detection, the multiplayer-safe way (same signal as NoAggroNoStaminaCost and as
/// the game's own combat music): MonsterAI calls OnTargeted(canSee|canHear, IsAlerted()) on
/// its target creature every frame, which routes "OnTargeted" to the machine owning that
/// player - Player.RPC_OnTargeted, registered only for the owner. Only the alerted argument
/// means "a hostile AI is hunting this player", and it is re-sent about every 0.5 s for as
/// long as the aggro lasts.
///
/// Everything else must NOT reset the condition, or passive creatures would block AFK
/// forever (the bug this replaces a blind IsTargeted()/IsSensed() poll for):
///  - AnimalAI (deer, boar, hens...) flees FROM its target and calls
///    OnTargeted(sensed: false, alerted: false) on you in a loop while you are near;
///  - MonsterAI with sensed=true but IsAlerted()==false: a neutral mob merely watching you
///    (it never attacks, e.g. Dvergr or a boss idling).
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.RPC_OnTargeted))]
internal static class Patch_RPCOnTargeted
{
    private static void Postfix(Player __instance, bool alerted)
    {
        try
        {
            if (alerted && __instance == Player.m_localPlayer)
            {
                AfkState.OnLocalPlayerTargetedByHostile();
            }
        }
        catch (Exception ex)
        {
            AFK.LogError($"Patch Player.RPC_OnTargeted: {ex}");
        }
    }
}
