using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TameableAnimals
{
    /// <summary>
    /// Añade a los animales con AnimalAI lo que Tameable espera de MonsterAI:
    /// comer del suelo, progreso de domesticación, seguir/quedarse y huir solo de enemigos reales.
    /// En criaturas con MonsterAI solo actúa como marcador (vanilla ya lo gestiona).
    /// </summary>
    public class TameableAnimal : MonoBehaviour
    {
        private static readonly Dictionary<string, HashSet<string>> s_foodNamesBySpecies = new Dictionary<string, HashSet<string>>();

        private ZNetView m_nview;
        private Character m_character;
        private AnimalAI m_animalAI;
        private Tameable m_tameable;

        private GameObject m_follow;
        private float m_useHoldStart;
        private bool m_useHoldHandled = true;
        private ItemDrop m_consumeTarget;
        private float m_consumeSearchTimer;

        internal bool UsesAnimalAI => m_animalAI != null;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_character = GetComponent<Character>();
            m_animalAI = GetComponent<AnimalAI>();
            m_tameable = GetComponent<Tameable>();
            m_consumeSearchTimer = UnityEngine.Random.Range(0f, 3f);
        }

        // ---------------------------------------------------------------- IA

        /// <summary>Sustituye a AnimalAI.UpdateAI.</summary>
        internal bool UpdateAI(float dt)
        {
            AnimalAI ai = m_animalAI;
            if (!BaseAIReverse.UpdateAI(ai, dt))
            {
                return false;
            }

            if (ai.m_afraidOfFire && ai.AvoidFire(dt, null, superAfraid: true))
            {
                return true;
            }

            ai.m_updateTargetTimer -= dt;
            if (ai.m_updateTargetTimer <= 0f)
            {
                ai.m_updateTargetTimer = Character.IsCharacterInRange(transform.position, 32f) ? 2f : 10f;
                Character threat = FindThreat();
                if (threat)
                {
                    ai.m_target = threat;
                }
            }

            // Al domesticarse, el jugador deja de ser enemigo y ya no huye de él.
            if (ai.m_target && (ai.m_target.IsDead() || !IsThreat(ai.m_target)))
            {
                ai.m_target = null;
            }

            if (ai.m_target)
            {
                bool sensed = ai.CanSenseTarget(ai.m_target);
                ai.SetTargetInfo(ai.m_target.GetZDOID());
                if (sensed)
                {
                    LogScare(ai.m_target);
                    ai.SetAlerted(true);
                }
            }
            else
            {
                ai.SetTargetInfo(ZDOID.None);
            }

            if (ai.IsAlerted())
            {
                ai.m_inDangerTimer += dt;
                if (ai.m_inDangerTimer > ai.m_timeToSafe)
                {
                    ai.m_target = null;
                    ai.SetAlerted(false);
                }
            }

            if (ai.m_target)
            {
                ai.Flee(dt, ai.m_target.transform.position);
                ai.m_target.OnTargeted(sensed: false, alerted: false);
                return true;
            }

            if (UpdateConsumeItem(dt))
            {
                return true;
            }

            if (m_character.IsTamed() && m_follow)
            {
                ai.Follow(m_follow, dt);
                return true;
            }

            ai.IdleMovement(dt);
            return true;
        }

        // ---------------------------------------------------------- Amenazas

        /// <summary>
        /// Como BaseAI.FindEnemy, pero sin contar a otros animales pacíficos (AnimalAI) ni a otras mascotas.
        /// En vanilla un domesticado y un salvaje son siempre enemigos, así que un ciervo domesticado
        /// vivía asustado de cada ciervo o liebre salvaje cercano (y los salvajes huían de él).
        /// </summary>
        private Character FindThreat()
        {
            Character closest = null;
            float closestDistance = float.MaxValue;
            foreach (Character other in Character.GetAllCharacters())
            {
                if (other.IsDead() || other.m_aiSkipTarget || !IsThreat(other))
                {
                    continue;
                }
                if (other == Player.m_localPlayer && CinematicsManager.IsPlaying())
                {
                    continue;
                }
                BaseAI otherAI = other.GetBaseAI();
                if ((otherAI != null && otherAI.IsSleeping()) || !m_animalAI.CanSenseTarget(other))
                {
                    continue;
                }
                float distance = Vector3.Distance(other.transform.position, transform.position);
                if (distance < closestDistance)
                {
                    closest = other;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private float m_lastScareLogTime = -999f;

        private void LogScare(Character threat)
        {
            if (!TameableAnimalsPlugin.DebugScareLog.Value || !m_character.IsTamed() || Time.time - m_lastScareLogTime < 5f)
            {
                return;
            }
            m_lastScareLogTime = Time.time;
            float distance = Vector3.Distance(threat.transform.position, transform.position);
            TameableAnimalsPlugin.Log.LogInfo("[Scare] " + m_tameable.GetHoverName() + " (" + GetSpeciesName(gameObject) + ") is scared of "
                + Utils.GetPrefabName(threat.gameObject) + " at " + distance.ToString("0.0") + " m");
        }

        private bool IsThreat(Character other)
        {
            if (other == m_character || !m_animalAI.IsEnemy(other))
            {
                return false;
            }
            // Los animales pacíficos no asustan a otros animales.
            if (other.GetBaseAI() is AnimalAI)
            {
                return false;
            }
            if (m_character.IsTamed())
            {
                // Las mascotas no se asustan unas de otras, y opcionalmente tampoco de los monstruos.
                if (other.IsTamed() || !TameableAnimalsPlugin.TamedFleeFromMonsters.Value)
                {
                    return false;
                }
                // Solo amenazas cercanas: el oído vanilla detecta monstruos a decenas de metros.
                if (Vector3.Distance(other.transform.position, transform.position) > TameableAnimalsPlugin.TamedFearRange.Value)
                {
                    return false;
                }
            }
            return true;
        }

        // ---------------------------------------------------------- Comida

        private bool UpdateConsumeItem(float dt)
        {
            if (m_tameable == null || m_animalAI.IsAlerted())
            {
                m_consumeTarget = null;
                return false;
            }

            m_consumeSearchTimer += dt;
            if (m_consumeSearchTimer > 3f)
            {
                m_consumeSearchTimer = 0f;
                m_consumeTarget = m_tameable.IsHungry() ? FindClosestFood(TameableAnimalsPlugin.FoodSearchRange.Value) : null;
            }

            if (!m_consumeTarget)
            {
                return false;
            }

            Vector3 foodPosition = m_consumeTarget.transform.position;
            float reach = 1f + m_character.GetRadius();
            if (Utils.DistanceXZ(foodPosition, transform.position) <= reach + 0.5f)
            {
                m_animalAI.StopMoving();
                m_animalAI.LookAt(foodPosition);
                if (m_animalAI.IsLookingAt(foodPosition, 40f) && m_consumeTarget.RemoveOne())
                {
                    m_tameable.OnConsumedItem(m_consumeTarget);
                    m_consumeTarget = null;
                    m_consumeSearchTimer = 0f;
                }
            }
            else if (m_animalAI.MoveTo(dt, foodPosition, reach, run: false))
            {
                // MoveTo devuelve true también cuando no encuentra camino (agua, hielo, orilla...): se va en línea recta.
                Vector3 direction = foodPosition - transform.position;
                direction.y = 0f;
                m_animalAI.MoveTowards(direction.normalized, run: false);
            }
            return true;
        }

        private ItemDrop FindClosestFood(float range)
        {
            HashSet<string> foods = GetFoodSharedNames();
            if (foods.Count == 0)
            {
                return null;
            }
            // Se recorren todos los objetos tirados (no solo la capa "item"), para incluir peces vivos en el suelo.
            // No se exige camino: los animales de agua/hielo (focas) a menudo no tienen ruta calculable.
            ItemDrop closest = null;
            float closestDistance = range;
            foreach (ItemDrop item in ItemDrop.s_instances)
            {
                if (item == null || !foods.Contains(item.m_itemData.m_shared.m_name))
                {
                    continue;
                }
                float distance = Vector3.Distance(item.transform.position, transform.position);
                if (distance > closestDistance)
                {
                    continue;
                }
                ZNetView itemView = item.GetComponent<ZNetView>();
                if (itemView == null || !itemView.IsValid())
                {
                    continue;
                }
                closest = item;
                closestDistance = distance;
            }
            return closest;
        }

        private HashSet<string> GetFoodSharedNames()
        {
            string species = GetSpeciesName(gameObject);
            if (s_foodNamesBySpecies.TryGetValue(species, out HashSet<string> cached))
            {
                return cached;
            }

            var names = new HashSet<string>();
            if (ZNetScene.instance == null)
            {
                return names;
            }
            foreach (string prefabName in TameableAnimalsPlugin.ParseList(TameableAnimalsPlugin.GetSpecies(species).Foods.Value))
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                ItemDrop item = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (item != null)
                {
                    names.Add(item.m_itemData.m_shared.m_name);
                }
                else
                {
                    TameableAnimalsPlugin.Log.LogWarning("Unknown food for " + species + ": " + prefabName);
                }
            }
            s_foodNamesBySpecies[species] = names;
            return names;
        }

        internal static string GetSpeciesName(GameObject go)
        {
            string name = Utils.GetPrefabName(go);
            if (name.EndsWith(CreatureSetup.BabySuffix))
            {
                name = name.Substring(0, name.Length - CreatureSetup.BabySuffix.Length);
            }
            return name;
        }

        // --------------------------------------------------- Domesticación

        /// <summary>Sustituye a Tameable.TamingUpdate (que exige MonsterAI).</summary>
        internal void TamingUpdate()
        {
            if (m_nview.IsValid() && m_nview.IsOwner() && !m_tameable.IsTamed() && !m_tameable.IsHungry() && !m_animalAI.IsAlerted())
            {
                m_tameable.DecreaseRemainingTime(3f);
                if (m_tameable.GetRemainingTime() <= 0f)
                {
                    Tame();
                }
                else
                {
                    m_tameable.m_sootheEffect.Create(transform.position, transform.rotation);
                }
            }
        }

        /// <summary>Sustituye a Tameable.Tame (que exige MonsterAI).</summary>
        internal void Tame()
        {
            Game.instance.IncrementPlayerStat(PlayerStatType.CreatureTamed);
            if (m_nview.IsValid() && m_nview.IsOwner() && !m_tameable.IsTamed())
            {
                m_character.SetTamed(true);
                m_animalAI.m_target = null;
                m_animalAI.SetAlerted(false);
                m_tameable.m_tamedEffect.Create(transform.position, transform.rotation);
                Player closestPlayer = Player.GetClosestPlayer(transform.position, 30f);
                if (closestPlayer)
                {
                    closestPlayer.Message(MessageHud.MessageType.Center, m_character.m_name + " $hud_tamedone");
                }
            }
        }

        internal string GetStatusString()
        {
            if (m_animalAI.IsAlerted())
            {
                return "$hud_tamefrightened";
            }
            if (m_tameable.IsHungry())
            {
                return "$hud_tamehungry";
            }
            if (m_character.IsTamed())
            {
                return "$hud_tamehappy";
            }
            return "$hud_tameinprogress";
        }

        // ------------------------------------------------- Seguir / quedarse

        private const float HoldToCommandSeconds = 0.6f;

        /// <summary>
        /// Pulsar Usar acaricia (lo hace vanilla). Mantener Usar alterna seguir/quedarse.
        /// Devuelve null para dejar actuar a vanilla, o el resultado de la interacción si la gestiona el mod.
        /// </summary>
        internal bool? OnInteract(Humanoid user, bool hold, bool alt)
        {
            if (!TameableAnimalsPlugin.HoldUseToFollow.Value || alt)
            {
                return null;
            }
            if (!hold)
            {
                m_useHoldStart = Time.time;
                m_useHoldHandled = false;
                return null;
            }
            if (m_useHoldHandled || !m_tameable.IsTamed() || Time.time - m_useHoldStart < HoldToCommandSeconds)
            {
                return false;
            }
            m_useHoldHandled = true;
            m_tameable.Command(user);
            return true;
        }

        internal string GetCommandHoverText()
        {
            if (!TameableAnimalsPlugin.HoldUseToFollow.Value || !m_tameable.IsTamed())
            {
                return "";
            }
            return Localization.instance.Localize("\n[<color=yellow><b>$ta_hold $KEY_Use</b></color>] $ta_followstay");
        }

        /// <summary>Sustituye a Tameable.RPC_Command (que exige MonsterAI).</summary>
        internal void Command(ZDOID characterID, bool message)
        {
            Player player = m_tameable.GetPlayer(characterID);
            if (player == null)
            {
                return;
            }

            if (m_follow)
            {
                m_follow = null;
                m_animalAI.SetPatrolPoint();
                if (m_nview.IsOwner())
                {
                    m_nview.GetZDO().Set(ZDOVars.s_follow, "");
                }
                if (message)
                {
                    player.Message(MessageHud.MessageType.Center, m_tameable.GetHoverName() + " $hud_tamestay");
                }
            }
            else
            {
                m_animalAI.ResetPatrolPoint();
                m_follow = player.gameObject;
                if (m_nview.IsOwner())
                {
                    m_nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
                }
                if (message)
                {
                    player.Message(MessageHud.MessageType.Center, m_tameable.GetHoverName() + " $hud_tamefollow");
                }
            }
            m_tameable.m_unsummonTime = 0f;
        }

        /// <summary>Sustituye a Tameable.UpdateSavedFollowTarget: recupera a quién seguía tras cambiar de dueño de red.</summary>
        internal void UpdateSavedFollowTarget()
        {
            if (m_follow != null || !m_nview.IsValid() || !m_nview.IsOwner())
            {
                return;
            }
            string followName = m_nview.GetZDO().GetString(ZDOVars.s_follow);
            if (string.IsNullOrEmpty(followName))
            {
                return;
            }
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player.GetPlayerName() == followName)
                {
                    m_tameable.Command(player, message: false);
                    return;
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class BaseAIReverse
    {
        /// <summary>Llama a BaseAI.UpdateAI sin despacho virtual (equivale a base.UpdateAI dentro de AnimalAI).</summary>
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.UpdateAI))]
        internal static bool UpdateAI(BaseAI instance, float dt)
        {
            throw new NotImplementedException("Stub de reverse patch");
        }
    }
}
