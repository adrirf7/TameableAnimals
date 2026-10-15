using System.Collections.Generic;
using HarmonyLib;

namespace TameableAnimals
{
    /// <summary>
    /// Textos que el mod muestra dentro del juego. Se registran en el sistema de localización de Valheim,
    /// así que siguen el idioma elegido en el juego. Inglés por defecto para cualquier idioma sin traducción.
    /// </summary>
    internal static class Translations
    {
        private const string DefaultLanguage = "English";

        // clave (sin $) -> idioma -> texto
        private static readonly Dictionary<string, Dictionary<string, string>> s_words = new Dictionary<string, Dictionary<string, string>>
        {
            ["ta_hold"] = Words("Hold", "Mantener"),
            ["ta_followstay"] = Words("Follow / Stay", "Seguir / Quedarse"),
            // Prefijo para crías de especies sin nombre propio: "Young Neck" / "Cría de Neck".
            ["ta_young"] = Words("Young", "Cría de"),
            ["ta_baby_deer"] = Words("Fawn", "Cervatillo"),
            ["ta_baby_deer_white"] = Words("White fawn", "Cervatillo blanco"),
            ["ta_baby_hare"] = Words("Leveret", "Lebrato"),
            ["ta_baby_seal"] = Words("Seal pup", "Cría de foca"),
            ["ta_baby_bjorn"] = Words("Bear cub", "Osezno"),
            ["ta_baby_unbjorn"] = Words("Undead bear cub", "Osezno no muerto"),
            ["ta_baby_bat"] = Words("Baby bat", "Murciélago bebé"),
        };

        private static Dictionary<string, string> Words(string english, string spanish)
        {
            return new Dictionary<string, string> { [DefaultLanguage] = english, ["Spanish"] = spanish };
        }

        /// <summary>Nombre localizable de la cría: token propio de la especie, o "Cría de" + nombre del adulto.</summary>
        internal static string GetBabyNameToken(string speciesPrefab, string adultName)
        {
            string key = "ta_baby_" + speciesPrefab.ToLowerInvariant();
            return s_words.ContainsKey(key) ? "$" + key : "$ta_young " + adultName;
        }

        internal static void Register(Localization localization, string language)
        {
            foreach (KeyValuePair<string, Dictionary<string, string>> word in s_words)
            {
                if (!word.Value.TryGetValue(language, out string text))
                {
                    text = word.Value[DefaultLanguage];
                }
                localization.AddWord(word.Key, text);
            }
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
    internal static class Localization_SetupLanguage_Patch
    {
        private static void Postfix(Localization __instance, string language)
        {
            Translations.Register(__instance, language);
        }
    }
}
