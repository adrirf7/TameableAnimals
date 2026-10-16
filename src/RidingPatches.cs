using HarmonyLib;

namespace TameableAnimals
{
    /// <summary>
    /// Un animal domesticado solo ataca a enemigos cerca de su punto de "quédate aquí" (o de a quien sigue).
    /// Al montarlo, ese punto ya no tiene sentido: se borra para que ataque a lo que tenga cerca vaya donde vaya.
    /// (En vanilla el lox nunca tenía ese punto porque no se le podía mandar quedarse.)
    /// </summary>
    [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_RequestControl))]
    internal static class Sadle_RPC_RequestControl_Patch
    {
        // Prefix: aún somos dueños del animal (al final de la RPC el dueño pasa al jinete).
        private static void Prefix(Sadle __instance)
        {
            if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner() || __instance.m_monsterAI == null)
            {
                return;
            }
            __instance.m_monsterAI.ResetPatrolPoint();
            __instance.m_monsterAI.SetFollowTarget(null);
            __instance.m_nview.GetZDO().Set(ZDOVars.s_follow, "");
        }
    }

    /// <summary>
    /// Al bajarse, el animal se queda quieto donde se le deja: ese sitio pasa a ser su punto de "quédate aquí",
    /// así que ataca a los enemigos que se acerquen allí (y no vuelve a un punto antiguo).
    /// </summary>
    [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_ReleaseControl))]
    internal static class Sadle_RPC_ReleaseControl_Patch
    {
        private static void Postfix(Sadle __instance)
        {
            if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner()
                || __instance.m_monsterAI == null || __instance.HaveValidUser())
            {
                return;
            }
            __instance.m_monsterAI.SetFollowTarget(null);
            __instance.m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            __instance.m_monsterAI.SetPatrolPoint();
        }
    }
}
