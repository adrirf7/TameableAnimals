using HarmonyLib;

namespace TameableAnimals
{
    // Tameable vanilla depende de MonsterAI. Para animales con AnimalAI se redirige cada parte a TameableAnimal.

    internal static class AnimalTameLookup
    {
        internal static TameableAnimal Get(Tameable tameable)
        {
            if (tameable.m_monsterAI != null)
            {
                return null;
            }
            TameableAnimal animal = tameable.GetComponent<TameableAnimal>();
            return animal != null && animal.UsesAnimalAI ? animal : null;
        }
    }

    [HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
    internal static class AnimalAI_UpdateAI_Patch
    {
        private static bool Prefix(AnimalAI __instance, float dt, ref bool __result)
        {
            TameableAnimal animal = __instance.GetComponent<TameableAnimal>();
            if (animal == null)
            {
                return true;
            }
            __result = animal.UpdateAI(dt);
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.TamingUpdate))]
    internal static class Tameable_TamingUpdate_Patch
    {
        private static bool Prefix(Tameable __instance)
        {
            TameableAnimal animal = AnimalTameLookup.Get(__instance);
            if (animal == null)
            {
                return true;
            }
            animal.TamingUpdate();
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Tame))]
    internal static class Tameable_Tame_Patch
    {
        private static bool Prefix(Tameable __instance)
        {
            TameableAnimal animal = AnimalTameLookup.Get(__instance);
            if (animal == null)
            {
                return true;
            }
            animal.Tame();
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetStatusString))]
    internal static class Tameable_GetStatusString_Patch
    {
        private static bool Prefix(Tameable __instance, ref string __result)
        {
            TameableAnimal animal = AnimalTameLookup.Get(__instance);
            if (animal == null)
            {
                return true;
            }
            __result = animal.GetStatusString();
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.RPC_Command))]
    internal static class Tameable_RPC_Command_Patch
    {
        private static bool Prefix(Tameable __instance, ZDOID characterID, bool message)
        {
            TameableAnimal animal = AnimalTameLookup.Get(__instance);
            if (animal == null)
            {
                return true;
            }
            animal.Command(characterID, message);
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.UpdateSavedFollowTarget))]
    internal static class Tameable_UpdateSavedFollowTarget_Patch
    {
        private static bool Prefix(Tameable __instance)
        {
            TameableAnimal animal = AnimalTameLookup.Get(__instance);
            if (animal == null)
            {
                return true;
            }
            animal.UpdateSavedFollowTarget();
            return false;
        }
    }
}
