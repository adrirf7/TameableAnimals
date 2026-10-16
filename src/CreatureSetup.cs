using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TameableAnimals
{
    /// <summary>
    /// Modifica los prefabs de los animales antes de que ZNetScene los registre:
    /// les añade Tameable + Procreation y crea un prefab de cría (clon más pequeño y tintado).
    /// </summary>
    internal static class CreatureSetup
    {
        internal const string BabySuffix = "_TA_Baby";

        private static GameObject s_prefabHolder;
        private static readonly Dictionary<string, GameObject> s_babies = new Dictionary<string, GameObject>();

        internal static void Setup(List<GameObject> prefabs)
        {
            var byName = new Dictionary<string, GameObject>();
            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null && !byName.ContainsKey(prefab.name))
                {
                    byName[prefab.name] = prefab;
                }
            }

            // Se reutilizan los efectos (corazones, sonidos) del jabalí vanilla.
            byName.TryGetValue("Boar", out GameObject boar);
            Tameable boarTameable = boar ? boar.GetComponent<Tameable>() : null;
            Procreation boarProcreation = boar ? boar.GetComponent<Procreation>() : null;

            HashSet<string> extra = TameableAnimalsPlugin.ParseList(TameableAnimalsPlugin.ExtraCreatures.Value);

            var candidates = new List<GameObject>();
            var vanillaTames = new List<string>();
            var available = new List<string>();
            foreach (GameObject prefab in byName.Values)
            {
                switch (Classify(prefab))
                {
                    case CreatureKind.VanillaTame:
                        vanillaTames.Add(prefab.name);
                        if (extra.Contains(prefab.name))
                        {
                            TameableAnimalsPlugin.Log.LogInfo(prefab.name + " is already tameable in vanilla; ignored in ExtraCreatures.");
                        }
                        break;
                    case CreatureKind.Compatible:
                        if (IsCandidate(prefab, extra))
                        {
                            candidates.Add(prefab);
                        }
                        else
                        {
                            available.Add(prefab.name);
                        }
                        break;
                    default:
                        if (extra.Contains(prefab.name) || TameableAnimalsPlugin.IsKnownAnimal(prefab.name))
                        {
                            TameableAnimalsPlugin.Log.LogWarning(prefab.name + " cannot be made tameable (boss, blocked or incompatible AI).");
                        }
                        break;
                }
            }
            var modTames = new List<string>();

            foreach (GameObject adult in candidates)
            {
                // El prefab ya se procesó en una carga anterior de ZNetScene: solo hay que volver a registrar la cría.
                if (adult.GetComponent<TameableAnimal>() != null)
                {
                    if (s_babies.TryGetValue(adult.name, out GameObject existingBaby) && !prefabs.Contains(existingBaby))
                    {
                        prefabs.Add(existingBaby);
                    }
                    modTames.Add(adult.name);
                    continue;
                }

                SpeciesSettings settings = TameableAnimalsPlugin.GetSpecies(adult.name);
                if (!settings.Enabled.Value)
                {
                    available.Add(adult.name + "   (disabled with Enabled = false)");
                    continue;
                }
                modTames.Add(adult.name);

                MakeTameable(adult, settings, byName, boarTameable);
                GameObject baby = CreateBaby(adult, settings, byName);
                AddProcreation(adult, baby, boarProcreation);
                // Después de crear la cría, para que el osezno no lleve montura.
                if (adult.name == "Bjorn")
                {
                    BearSaddle.AttachToBear(adult, byName, prefabs);
                }

                s_babies[adult.name] = baby;
                prefabs.Add(baby);
                TameableAnimalsPlugin.Log.LogInfo("Now tameable and breedable: " + adult.name + " (offspring: " + baby.name + ")");
            }

            BearSaddle.EnsureItemRegistered(prefabs);
            AddSaddleColors(byName.Values);
            WriteCreatureReport(modTames, vanillaTames, available);
        }

        /// <summary>Todos los animales con montura (lox, alce, asksvin y oso) guardan y muestran su color.</summary>
        private static void AddSaddleColors(IEnumerable<GameObject> prefabs)
        {
            if (!SaddleColors.Enabled)
            {
                return;
            }
            foreach (GameObject prefab in prefabs)
            {
                Tameable tameable = prefab.GetComponent<Tameable>();
                bool hasSaddle = tameable != null && (tameable.m_saddle != null || prefab.GetComponent<BearSaddleMarker>() != null);
                if (hasSaddle && tameable.m_saddleItem != null
                    && SaddleColors.GetSpec(tameable.m_saddleItem.name) != null && prefab.GetComponent<SaddleColor>() == null)
                {
                    prefab.AddComponent<SaddleColor>();
                }
            }
        }

        private enum CreatureKind
        {
            Ignored,       // no es criatura, es jugador, jefe, cría, bloqueada o incompatible
            VanillaTame,   // ya domesticable en vanilla: no se toca nunca
            Compatible     // se puede hacer domesticable con este mod
        }

        private static CreatureKind Classify(GameObject prefab)
        {
            if (prefab.name.EndsWith(BabySuffix) || TameableAnimalsPlugin.IsBlocked(prefab.name) || TameableAnimalsPlugin.IsBabyModel(prefab.name))
            {
                return CreatureKind.Ignored;
            }

            Character character = prefab.GetComponent<Character>();
            if (character == null || character is Player || character.m_boss)
            {
                return CreatureKind.Ignored;
            }

            // Ya procesado por nosotros en una carga anterior.
            if (prefab.GetComponent<TameableAnimal>() != null)
            {
                return CreatureKind.Compatible;
            }

            if (prefab.GetComponent<Growup>() != null)
            {
                return CreatureKind.Ignored;
            }
            if (prefab.GetComponent<Tameable>() != null)
            {
                return CreatureKind.VanillaTame;
            }

            // AnimalAI lo gestiona TameableAnimal; MonsterAI usa la domesticación vanilla, que exige Humanoid.
            bool compatible = prefab.GetComponent<AnimalAI>() != null
                || (prefab.GetComponent<MonsterAI>() != null && character is Humanoid);
            return compatible ? CreatureKind.Compatible : CreatureKind.Ignored;
        }

        private static bool IsCandidate(GameObject prefab, HashSet<string> extra)
        {
            if (prefab.GetComponent<TameableAnimal>() != null)
            {
                return true;
            }
            string name = prefab.name;
            if (TameableAnimalsPlugin.IsKnownAnimal(name) || extra.Contains(name))
            {
                return true;
            }
            return TameableAnimalsPlugin.AutoDetectAnimals.Value && prefab.GetComponent<AnimalAI>() != null;
        }

        /// <summary>Escribe en BepInEx/config la lista de criaturas del juego para saber qué se puede añadir a ExtraCreatures.</summary>
        private static void WriteCreatureReport(List<string> modTames, List<string> vanillaTames, List<string> available)
        {
            modTames.Sort();
            vanillaTames.Sort();
            available.Sort();
            var text = new System.Text.StringBuilder();
            text.AppendLine("# Generated automatically by TameableAnimals when entering a world. No need to edit it.");
            text.AppendLine("# To make a creature from the last list tameable, add its name to ExtraCreatures");
            text.AppendLine("# in " + TameableAnimalsPlugin.FilePrefix + ".Animals.cfg (comma-separated) and restart the game.");
            text.AppendLine();
            text.AppendLine("== Tameable thanks to this mod ==");
            modTames.ForEach(name => text.AppendLine(name));
            text.AppendLine();
            text.AppendLine("== Tameable in vanilla (not modified by this mod) ==");
            vanillaTames.ForEach(name => text.AppendLine(name));
            text.AppendLine();
            text.AppendLine("== Other compatible creatures (add to ExtraCreatures) ==");
            available.ForEach(name => text.AppendLine(name));

            string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, TameableAnimalsPlugin.FilePrefix + ".AvailableCreatures.txt");
            try
            {
                System.IO.File.WriteAllText(path, text.ToString());
            }
            catch (System.Exception e)
            {
                TameableAnimalsPlugin.Log.LogWarning("Could not write " + path + ": " + e.Message);
            }
        }

        private static void MakeTameable(GameObject adult, SpeciesSettings settings, Dictionary<string, GameObject> byName, Tameable reference)
        {
            Tameable tameable = adult.AddComponent<Tameable>();
            tameable.m_tamingTime = TameableAnimalsPlugin.TamingTime.Value;
            tameable.m_fedDuration = TameableAnimalsPlugin.FedDuration.Value;
            // Pulsar Usar = acariciar ("te quiere"); seguir/quedarse se hace manteniendo Usar (ver Tameable_Interact_Patch).
            tameable.m_commandable = false;
            if (reference != null)
            {
                tameable.m_tamedEffect = reference.m_tamedEffect;
                tameable.m_sootheEffect = reference.m_sootheEffect;
                tameable.m_petEffect = reference.m_petEffect;
            }

            adult.AddComponent<TameableAnimal>();

            // Las criaturas con MonsterAI usan el sistema de comida vanilla.
            MonsterAI monsterAI = adult.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                var foods = new List<ItemDrop>();
                foreach (string foodName in TameableAnimalsPlugin.ParseList(settings.Foods.Value))
                {
                    if (byName.TryGetValue(foodName, out GameObject food) && food.GetComponent<ItemDrop>() != null)
                    {
                        foods.Add(food.GetComponent<ItemDrop>());
                    }
                    else
                    {
                        TameableAnimalsPlugin.Log.LogWarning("Unknown food for " + adult.name + ": " + foodName);
                    }
                }
                monsterAI.m_consumeItems = foods;
            }
        }

        private static void AddProcreation(GameObject adult, GameObject baby, Procreation reference)
        {
            Procreation procreation = adult.AddComponent<Procreation>();
            procreation.m_offspring = baby;
            procreation.m_pregnancyDuration = TameableAnimalsPlugin.PregnancyDuration.Value;
            procreation.m_pregnancyChance = TameableAnimalsPlugin.PregnancyChance.Value;
            procreation.m_requiredLovePoints = TameableAnimalsPlugin.RequiredLovePoints.Value;
            procreation.m_maxCreatures = TameableAnimalsPlugin.MaxCreatures.Value;
            procreation.m_totalCheckRange = TameableAnimalsPlugin.TotalCheckRange.Value;
            procreation.m_partnerCheckRange = TameableAnimalsPlugin.PartnerCheckRange.Value;
            procreation.m_spawnOffset = 1f;
            if (reference != null)
            {
                procreation.m_loveEffects = reference.m_loveEffects;
                procreation.m_birthEffects = reference.m_birthEffects;
            }
        }

        private static GameObject CreateBaby(GameObject adult, SpeciesSettings settings, Dictionary<string, GameObject> byName)
        {
            // Modelo de la cría: el del adulto reducido, o un prefab de cría del juego (p. ej. Seal_Pup) a su tamaño.
            GameObject model = adult;
            string modelName = settings.BabyModel.Value.Trim();
            if (modelName.Length > 0)
            {
                if (byName.TryGetValue(modelName, out GameObject customModel) && customModel.GetComponent<Character>() != null)
                {
                    model = customModel;
                }
                else
                {
                    TameableAnimalsPlugin.Log.LogWarning("BabyModel '" + modelName + "' not found for " + adult.name + "; using the scaled-down adult.");
                }
            }

            // Se clona bajo un padre inactivo para que no se ejecute Awake (ZNetView, IA...) en el prefab.
            GameObject baby = Object.Instantiate(model, GetPrefabHolder().transform, false);
            baby.name = adult.name + BabySuffix;
            baby.transform.localPosition = Vector3.zero;
            baby.transform.localScale = model == adult
                ? adult.transform.localScale * TameableAnimalsPlugin.BabyScale.Value
                : model.transform.localScale;

            if (model != adult)
            {
                // El modelo de cría del juego no trae nuestros componentes: se le añaden como al adulto.
                foreach (Procreation procreation in baby.GetComponents<Procreation>())
                {
                    Object.DestroyImmediate(procreation);
                }
                if (baby.GetComponent<Tameable>() == null)
                {
                    Tameable source = adult.GetComponent<Tameable>();
                    Tameable tameable = baby.AddComponent<Tameable>();
                    tameable.m_tamingTime = source.m_tamingTime;
                    tameable.m_fedDuration = source.m_fedDuration;
                    tameable.m_commandable = false;
                    tameable.m_tamedEffect = source.m_tamedEffect;
                    tameable.m_sootheEffect = source.m_sootheEffect;
                    tameable.m_petEffect = source.m_petEffect;
                }
                if (baby.GetComponent<TameableAnimal>() == null)
                {
                    baby.AddComponent<TameableAnimal>();
                }
            }

            Character character = baby.GetComponent<Character>();
            // Vacío = nombre traducido al idioma del juego; si no, el texto del config tal cual.
            string babyName = settings.BabyName.Value.Trim();
            character.m_name = babyName.Length > 0
                ? babyName
                : Translations.GetBabyNameToken(adult.name, adult.GetComponent<Character>().m_name);
            character.m_health = Mathf.Max(1f, character.m_health * TameableAnimalsPlugin.BabyHealthFactor.Value);

            // Las crías no sueltan botín.
            CharacterDrop drops = baby.GetComponent<CharacterDrop>();
            if (drops != null)
            {
                Object.DestroyImmediate(drops);
            }

            Growup growup = baby.GetComponent<Growup>();
            if (growup == null)
            {
                growup = baby.AddComponent<Growup>();
            }
            growup.m_grownPrefab = adult;
            growup.m_growTime = TameableAnimalsPlugin.GrowTime.Value;
            growup.m_inheritTame = true;

            TintRenderers(baby, TameableAnimalsPlugin.ParseTint(settings.BabyTint.Value));
            return baby;
        }

        private static readonly string[] s_colorProperties = { "_Color", "_BaseColor", "_MainColor", "_TintColor" };

        private static void TintRenderers(GameObject baby, Color tint)
        {
            if (tint == Color.white)
            {
                return;
            }

            var copies = new Dictionary<Material, Material>();
            foreach (Renderer renderer in baby.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null)
                    {
                        continue;
                    }
                    if (!copies.TryGetValue(original, out Material copy))
                    {
                        copy = new Material(original) { name = original.name + BabySuffix };
                        foreach (string property in s_colorProperties)
                        {
                            if (copy.HasProperty(property))
                            {
                                copy.SetColor(property, copy.GetColor(property) * tint);
                            }
                        }
                        copies[original] = copy;
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        internal static GameObject GetPrefabHolder()
        {
            if (s_prefabHolder == null)
            {
                s_prefabHolder = new GameObject("TameableAnimals_Prefabs");
                s_prefabHolder.SetActive(false);
                Object.DontDestroyOnLoad(s_prefabHolder);
            }
            return s_prefabHolder;
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    internal static class ZNetScene_Awake_Patch
    {
        // Prefix: los prefabs nuevos deben estar en m_prefabs antes de que Awake construya el diccionario de nombres.
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ZNetScene __instance)
        {
            CreatureSetup.Setup(__instance.m_prefabs);
        }
    }
}
