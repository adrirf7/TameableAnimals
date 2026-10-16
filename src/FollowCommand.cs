using HarmonyLib;
using UnityEngine;

namespace TameableAnimals
{
    /// <summary>
    /// Pulsar Usar acaricia (vanilla). Mantener Usar alterna seguir / quedarse en cualquier animal domesticado,
    /// también en los domesticables vanilla (jabalí, lox, gallina...). Los que ya se mandan con un toque
    /// (lobo, m_commandable) se dejan como en vanilla.
    /// </summary>
    internal static class FollowCommand
    {
        private const float HoldSeconds = 0.6f;

        // Solo el jugador local interactúa, así que basta con un estado global.
        private static Tameable s_holdTarget;
        private static float s_holdStart;
        private static bool s_holdHandled = true;

        internal static bool Enabled => TameableAnimalsPlugin.FollowCommandEnabled.Value;

        private static bool Applies(Tameable tameable)
        {
            return Enabled && tameable != null && !tameable.m_commandable && tameable.GetComponent<Character>() != null;
        }

        /// <summary>null = dejar actuar a vanilla; si no, el resultado de la interacción.</summary>
        internal static bool? OnInteract(Tameable tameable, Humanoid user, bool hold, bool alt)
        {
            if (alt || !Applies(tameable))
            {
                return null;
            }
            if (!hold)
            {
                s_holdTarget = tameable;
                s_holdStart = Time.time;
                s_holdHandled = false;
                return null;
            }
            if (s_holdTarget != tameable || s_holdHandled || !tameable.IsTamed() || Time.time - s_holdStart < HoldSeconds)
            {
                return false;
            }
            s_holdHandled = true;
            tameable.Command(user);
            return true;
        }

        internal static string GetHoverText(Tameable tameable)
        {
            if (!Applies(tameable) || !tameable.IsTamed())
            {
                return "";
            }
            return Localization.instance.Localize("\n[<color=yellow><b>$ta_hold $KEY_Use</b></color>] $ta_followstay");
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    internal static class Tameable_Interact_Patch
    {
        private static bool Prefix(Tameable __instance, Humanoid user, bool hold, bool alt, ref bool __result)
        {
            bool? handled = FollowCommand.OnInteract(__instance, user, hold, alt);
            if (handled == null)
            {
                return true;
            }
            __result = handled.Value;
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    internal static class Tameable_GetHoverText_Patch
    {
        private static void Postfix(Tameable __instance, ref string __result)
        {
            __result += FollowCommand.GetHoverText(__instance);
        }
    }
}
