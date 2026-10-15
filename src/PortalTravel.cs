using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TameableAnimals
{
    /// <summary>
    /// Al cruzar un portal, los animales domesticados que siguen al jugador y están cerca viajan con él.
    /// Se recuerdan al empezar el teletransporte y se colocan alrededor del jugador cuando termina de llegar.
    /// </summary>
    internal static class PortalTravel
    {
        private static readonly List<ZDOID> s_pending = new List<ZDOID>();

        internal static void OnTeleportStarted(Player player)
        {
            s_pending.Clear();
            if (!TameableAnimalsPlugin.PortalTravelEnabled.Value)
            {
                return;
            }

            string playerName = player.GetPlayerName();
            float radius = TameableAnimalsPlugin.PortalTravelRadius.Value;
            var followers = new List<KeyValuePair<float, ZDOID>>();
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character.IsPlayer() || character.IsDead() || !character.IsTamed())
                {
                    continue;
                }
                Tameable tameable = character.GetComponent<Tameable>();
                if (tameable == null || tameable.HaveRider())
                {
                    continue;
                }
                if (character.GetComponent<TameableAnimal>() == null)
                {
                    if (!TameableAnimalsPlugin.PortalTravelVanillaTames.Value)
                    {
                        continue;
                    }
                }
                else if (!TameableAnimalsPlugin.GetSpecies(TameableAnimal.GetSpeciesName(character.gameObject)).CanUsePortals.Value)
                {
                    continue;
                }
                ZNetView nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || nview.GetZDO().GetString(ZDOVars.s_follow) != playerName)
                {
                    continue;
                }
                float distance = Vector3.Distance(character.transform.position, player.transform.position);
                if (distance <= radius)
                {
                    followers.Add(new KeyValuePair<float, ZDOID>(distance, nview.GetZDO().m_uid));
                }
            }

            followers.Sort((a, b) => a.Key.CompareTo(b.Key));
            int max = TameableAnimalsPlugin.PortalTravelMaxAnimals.Value;
            for (int i = 0; i < followers.Count && i < max; i++)
            {
                s_pending.Add(followers[i].Value);
            }
        }

        internal static void OnTeleportUpdated(Player player)
        {
            if (s_pending.Count == 0 || player.IsTeleporting())
            {
                return;
            }

            Vector3 center = player.transform.position;
            Vector3 back = -player.transform.forward;
            long sessionId = ZDOMan.GetSessionID();
            for (int i = 0; i < s_pending.Count; i++)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(s_pending[i]);
                if (zdo == null)
                {
                    continue;
                }

                // Se reparten en abanico detrás del jugador.
                float angle = (i - (s_pending.Count - 1) * 0.5f) * 35f;
                Vector3 position = center + Quaternion.Euler(0f, angle, 0f) * back * (2.5f + i * 0.3f);
                position.y = ZoneSystem.instance.GetSolidHeight(position) + 0.2f;

                GameObject instance = ZNetScene.instance.FindInstance(s_pending[i]);
                if (instance != null)
                {
                    instance.GetComponent<ZNetView>().ClaimOwnership();
                    instance.transform.position = position;
                    Rigidbody body = instance.GetComponent<Rigidbody>();
                    if (body != null)
                    {
                        body.linearVelocity = Vector3.zero;
                    }
                }
                else
                {
                    // El animal se descargó al alejarse el jugador: se mueve su ZDO y el juego lo vuelve a crear aquí.
                    zdo.SetOwner(sessionId);
                }
                zdo.SetPosition(position);
            }
            s_pending.Clear();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
    internal static class Player_TeleportTo_Patch
    {
        private static void Postfix(Player __instance, bool distantTeleport, bool __result)
        {
            // Solo teletransportes lejanos (portales), no entradas a mazmorras.
            if (__result && distantTeleport && __instance == Player.m_localPlayer)
            {
                PortalTravel.OnTeleportStarted(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
    internal static class Player_UpdateTeleport_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                PortalTravel.OnTeleportUpdated(__instance);
            }
        }
    }
}
