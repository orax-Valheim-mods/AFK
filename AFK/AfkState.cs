#nullable enable
using BepInEx.Configuration;
using UnityEngine;

namespace AFK;

/// <summary>
/// State machine of the AFK status, ticked once per frame from AFK.Update on the machine
/// that owns the local player (Player.m_localPlayer is null on a dedicated server: Tick
/// then only resets the state).
///
/// Two layers of state:
///  - Requested: what the triggers (keybind / crouch) asked for. Released by the action
///    cancel, by death, or by the triggers themselves.
///  - Active:    the granted status = Enabled &amp;&amp; Requested &amp;&amp; alive
///    &amp;&amp; the quiet conditions held at some point since the request: re-checked
///    live while waiting (the status activates by itself once the timers pass) but
///    latched once granted (combat events never take it away). Only while Active the
///    effects apply.
///
/// The "invisible to enemies" effect is published as a bool on the player's OWN ZDO
/// (GhostKey): creature AI only runs on the machine owning the creature ZDO, and the
/// vanilla ghost field (Player.m_ghostMode) is a plain local field that never syncs.
/// The owner writes, every modded machine reads (AfkState.IsGhost).
/// </summary>
internal static class AfkState
{
    /// <summary>ZDO key (bool): "creatures must treat this player as invisible".</summary>
    internal const string GhostKey = "afk_ghost";

    /// <summary>
    /// Game buttons whose press releases the AFK request - always on, so nobody can walk
    /// around while invisible (this behaviour is deliberately NOT configurable). These are
    /// the rebindable ZInput buttons, so the check follows the player's own key bindings,
    /// and unknown buttons simply return false.
    /// </summary>
    private static readonly string[] ActionButtons =
    {
        "Forward",
        "Backward",
        "Left",
        "Right",
        "Run",
        "JoyRun",
        "AutoRun",
        "JoyAutoRun",
        "Jump",
        "JoyJump",
        "Attack",
        "JoyAttack",
        "SecondaryAttack",
        "JoySecondaryAttack",
        "Block",
        "JoyBlock",
        "AltDodge",
        "JoyDodge",
        "Use",
        "JoyUse",
    };

    /// <summary>The player asked for the AFK status (triggers).</summary>
    internal static bool Requested { get; private set; }

    /// <summary>The AFK status is granted right now: the effects apply.</summary>
    internal static bool Active { get; private set; }

    private static float s_sinceDamageDealt;

    private static float s_sinceAggro;

    private static bool s_loggedNotOwner;

    // ------------------------------------------------------------------ tick

    /// <summary>Called once per frame from AFK.Update.</summary>
    internal static void Tick()
    {
        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            // Dedicated server or between worlds: nothing to control here.
            ResetLocal();
            return;
        }

        // 1) Quiet timers. The two patches below only zero them, time passes here:
        //    Patch_RPCOnTargeted (an ALERTED hostile holds us as its target) and
        //    Patch_RPCDamage (a character hit us). Polling IsTargeted()/IsSensed() instead
        //    was wrong: RPC_OnTargeted resets m_timeSinceTargeted unconditionally, so a
        //    deer fleeing from you (AnimalAI calls OnTargeted(false, false) in a loop) or a
        //    neutral mob merely watching you kept the aggro timer at zero forever.
        float dt = Time.deltaTime;
        s_sinceDamageDealt += dt;
        s_sinceAggro += dt;

        bool wasRequested = Requested;
        bool wasActive = Active;
        bool notifyRequestChange = false;
        string? message = null;

        // 2) Triggers. Never while a UI takes the input.
        bool inputBlocked = IsInputBlocked();
        if (AfkConfig.Enabled.Value && !player.IsDead() && !inputBlocked)
        {
            if (PressedShortcut(AfkConfig.ToggleKey.Value))
            {
                // The key is an explicit AFK intent: always answer it.
                SetRequested(!Requested);
                notifyRequestChange = true;
            }
            else if (AfkConfig.CrouchTriggers.Value && PressedCrouch())
            {
                // Crouching is also a normal stealth action, so a crouch-triggered request
                // only speaks up when it actually grants (or drops) the status - no
                // "waiting" spam while sneaking around after a fight.
                SetRequested(!Requested);
            }
            else if (Requested && ActionInputPressed())
            {
                SetRequested(false);
                message = "AFK: cancelled (action input)";
            }
        }

        // 3) Death always releases the status.
        if (Requested && player.IsDead())
        {
            SetRequested(false);
        }

        // 4) Grant / drop the status. While the request is still WAITING the quiet
        //    conditions are re-checked live every frame, so a request made too early is
        //    not lost: the status activates by itself once the timers pass. Once ACTIVE
        //    it is latched through wasActive: combat events never take it away - only the
        //    triggers, action input, death or the master switch do.
        Active =
            AfkConfig.Enabled.Value
            && Requested
            && !player.IsDead()
            && (wasActive || AreConditionsMet());

        // 5) One status message per change.
        message ??= StatusMessage(wasRequested, wasActive, notifyRequestChange);
        if (message != null)
        {
            Notify(player, message);
        }

        // 6) Publish the effect flag. Compared (and rewritten if needed) every frame, so a
        //    flag left over in the save is cleared within one frame of spawning.
        PublishGhostFlag(player);
    }

    /// <summary>Character.Damage prefix: the local player just landed a hit on a character.</summary>
    internal static void OnLocalPlayerDealtDamage()
    {
        s_sinceDamageDealt = 0f;
    }

    /// <summary>
    /// Player.RPC_OnTargeted prefix: an ALERTED hostile holds the local player as its
    /// target - the game's own combat signal (it also starts combat music), re-sent every
    /// 0.5 s while the aggro lasts. Passive creatures never produce it.
    /// </summary>
    internal static void OnLocalPlayerTargetedByHostile()
    {
        s_sinceAggro = 0f;
    }

    /// <summary>Character.RPC_Damage prefix: a character damaged the local player.</summary>
    internal static void OnLocalPlayerTookDamage()
    {
        s_sinceAggro = 0f;
    }

    /// <summary>
    /// True when creatures must treat this character as invisible: its ZDO carries the
    /// GhostKey flag (written by its owner) AND this machine has the effect enabled.
    /// Called from the BaseAI sense patches on every machine running the mod, whichever
    /// peer owns the creature.
    /// </summary>
    internal static bool IsGhost(Character? character)
    {
        if (character is not Player player)
        {
            return false;
        }
        if (!AfkConfig.InvisibleToEnemies.Value)
        {
            return false;
        }
        ZDO? zdo = GetPlayerZDO(player);
        return zdo != null && zdo.IsValid() && zdo.GetBool(GhostKey, false);
    }

    /// <summary>Releases the status and clears the ZDO flag. Called from AFK.OnDestroy.</summary>
    internal static void Shutdown()
    {
        // Never leave the flag behind: it would be saved with the character and keep the
        // player invisible on the next join until the self-heal in Tick rewrites it.
        Player? player = Player.m_localPlayer;
        if (player != null)
        {
            ZDO? zdo = GetPlayerZDO(player);
            if (zdo != null && zdo.IsValid() && zdo.IsOwner() && zdo.GetBool(GhostKey, false))
            {
                zdo.Set(GhostKey, false);
            }
        }
        ResetLocal();
    }

    // ------------------------------------------------------------------ internals

    private static void ResetLocal()
    {
        Requested = false;
        Active = false;
        s_sinceDamageDealt = 0f;
        s_sinceAggro = 0f;
        s_loggedNotOwner = false;
    }

    private static void SetRequested(bool value)
    {
        // Only the request layer lives here. The quiet conditions are deliberately NOT
        // snapshotted: Tick re-checks them live while waiting and latches the status once
        // granted (wasActive), so "hit something -> ask -> wait" ends up activating.
        Requested = value;
    }

    private static bool AreConditionsMet()
    {
        return s_sinceDamageDealt >= AfkConfig.SecondsSinceDamageDealt.Value
            && s_sinceAggro >= AfkConfig.SecondsSinceAggro.Value;
    }

    /// <summary>
    /// The pending status message with its live countdown ("AFK: waiting - no damage
    /// dealt, no enemy aggro (12s)"). Recomputed from the timers on every call, so the
    /// HUD indicator shows the seconds actually ticking down; identical to the TopLeft
    /// Notify text.
    /// </summary>
    internal static string WaitingMessage()
    {
        return $"AFK: waiting - {PendingReason()}";
    }

    private static string? StatusMessage(
        bool wasRequested,
        bool wasActive,
        bool notifyRequestChange
    )
    {
        if (Active != wasActive)
        {
            if (Active)
            {
                return "AFK: active";
            }
            // The status is latched while requested: the only way to lose it without
            // releasing the request is the master switch. The timers have nothing to do
            // with it.
            return Requested ? "AFK: paused (mod disabled)" : "AFK: off";
        }
        if (Requested != wasRequested && notifyRequestChange)
        {
            return Requested ? $"AFK: waiting - {PendingReason()}" : "AFK: off";
        }
        return null;
    }

    /// <summary>What is still missing, and for how long, when the status cannot be granted.</summary>
    private static string PendingReason()
    {
        float damageLeft = AfkConfig.SecondsSinceDamageDealt.Value - s_sinceDamageDealt;
        float aggroLeft = AfkConfig.SecondsSinceAggro.Value - s_sinceAggro;
        float left = Mathf.Max(damageLeft, aggroLeft);
        string reasons = "";
        if (damageLeft > 0f)
        {
            reasons = "no damage dealt";
        }
        if (aggroLeft > 0f)
        {
            reasons += (reasons.Length > 0 ? ", " : "") + "no enemy aggro";
        }
        return $"{reasons} ({Mathf.Max(1, Mathf.CeilToInt(left))}s)";
    }

    private static void Notify(Player player, string message)
    {
        AFK.LogDebug(message);
        if (AfkConfig.Notify.Value)
        {
            player.Message(MessageHud.MessageType.TopLeft, message);
        }
    }

    /// <summary>
    /// Writes the desired value of the GhostKey flag on the player's own ZDO. ZDO.Set()
    /// bumps the data revision only when the value really changes, so the steady state
    /// costs nothing, and it propagates owner -&gt; server -&gt; all clients. Explicit
    /// false, never a removal: removals do not propagate (ZDO.Deserialize only
    /// adds/overwrites), overwrites do.
    /// </summary>
    private static void PublishGhostFlag(Player player)
    {
        ZDO? zdo = GetPlayerZDO(player);
        if (zdo == null || !zdo.IsValid())
        {
            return;
        }
        bool desired = Active && AfkConfig.InvisibleToEnemies.Value;
        if (zdo.GetBool(GhostKey, false) == desired)
        {
            return;
        }
        if (!zdo.IsOwner())
        {
            if (!s_loggedNotOwner)
            {
                s_loggedNotOwner = true;
                AFK.LogWarning(
                    $"Your player ZDO is owned by peer {zdo.GetOwner()}: the AFK flag cannot be published from here."
                );
            }
            return;
        }
        zdo.Set(GhostKey, desired);
    }

    private static ZDO? GetPlayerZDO(Player player)
    {
        ZNetView? nview = player.GetComponent<ZNetView>();
        return nview?.GetZDO();
    }

    // ------------------------------------------------------------------ input

    private static bool PressedShortcut(KeyboardShortcut shortcut)
    {
        if (!ZInput.GetKeyDown(shortcut.MainKey, false))
        {
            return false;
        }
        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (!ZInput.GetKey(modifier, false))
            {
                return false;
            }
        }
        return true;
    }

    private static bool PressedCrouch()
    {
        return ZInput.GetButtonDown("Crouch") || ZInput.GetButtonDown("JoyCrouch");
    }

    private static bool ActionInputPressed()
    {
        foreach (string button in ActionButtons)
        {
            if (ZInput.GetButton(button))
            {
                return true;
            }
        }
        // Left stick deflection = walking, whatever the bindings say.
        return ZInput.GetJoyLeftStick().sqrMagnitude > 0.09f;
    }

    /// <summary>
    /// True while the game itself ignores input (chat, console, menus, inventory, radial
    /// wheel): the triggers must not fire then, or typing "f8" in the chat would toggle AFK.
    /// </summary>
    private static bool IsInputBlocked()
    {
        if (Console.IsVisible() || Menu.IsVisible() || TextInput.IsVisible())
        {
            return true;
        }
        if (InventoryGui.IsVisible() || Hud.InRadial())
        {
            return true;
        }
        return Chat.instance != null && Chat.instance.HasFocus();
    }
}
