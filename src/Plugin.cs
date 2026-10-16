using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TameableAnimals
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class TameableAnimalsPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "adrirf7.TameableAnimals";
        public const string ModName = "TameableAnimals";
        public const string ModVersion = "1.1.0";

        // Nombre de los archivos en BepInEx/config (el GUID no cambia, para no romper instalaciones).
        internal const string FilePrefix = "TameableAnimals.adrirf7";

        internal static TameableAnimalsPlugin Instance;
        internal static ManualLogSource Log;

        // ConfiguraciÃ³n general (archivo propio en vez de BaseUnityPlugin.Config, que usarÃ­a el GUID como nombre).
        internal static ConfigFile MainConfig;

        // Archivo de animales (quÃ© se puede domesticar)
        internal static ConfigFile AnimalsConfig;
        internal static ConfigEntry<bool> AutoDetectAnimals;
        internal static ConfigEntry<string> ExtraCreatures;

        // Animales no domesticables en vanilla que aparecen siempre en el archivo de animales.
        // Los de IA de animal (huyen) y los de IA de monstruo (atacan, se domestican como el lobo).
        private static readonly string[] s_knownAnimals =
        {
            "Deer", "Hare", "Seal",
            "Bjorn", "Unbjorn", "Bat"
        };

        // Prefabs que el mod no toca nunca (ni por detecciÃ³n automÃ¡tica ni por ExtraCreatures).
        // Goblin_Gem y Deer_White solo aparecen por comandos.
        private static readonly string[] s_blockedCreatures = { "Goblin_Gem", "Deer_White" };

        // DomesticaciÃ³n
        internal static ConfigEntry<float> TamingTime;
        internal static ConfigEntry<float> FedDuration;
        internal static ConfigEntry<float> FoodSearchRange;
        internal static ConfigEntry<bool> TamedFleeFromMonsters;
        internal static ConfigEntry<float> TamedFearRange;
        internal static ConfigEntry<bool> DebugScareLog;
        internal static ConfigEntry<bool> FollowCommandEnabled;

        // ReproducciÃ³n
        internal static ConfigEntry<float> PregnancyDuration;
        internal static ConfigEntry<float> PregnancyChance;
        internal static ConfigEntry<int> RequiredLovePoints;
        internal static ConfigEntry<int> MaxCreatures;
        internal static ConfigEntry<float> TotalCheckRange;
        internal static ConfigEntry<float> PartnerCheckRange;

        // CrÃ­as
        internal static ConfigEntry<float> GrowTime;
        internal static ConfigEntry<float> BabyScale;
        internal static ConfigEntry<float> BabyHealthFactor;

        // Portales
        internal static ConfigEntry<bool> PortalTravelEnabled;
        internal static ConfigEntry<float> PortalTravelRadius;
        internal static ConfigEntry<int> PortalTravelMaxAnimals;
        internal static ConfigEntry<bool> PortalTravelVanillaTames;

        // Monturas
        internal static ConfigEntry<bool> SaddleColorsEnabled;
        internal static ConfigEntry<bool> BearSaddleEnabled;
        internal static ConfigEntry<string> BearSaddleRecipe;
        internal static ConfigEntry<string> BearSaddleStation;
        internal static ConfigEntry<int> BearSaddleStationLevel;
        internal static ConfigEntry<string> BearSaddleOffset;
        internal static ConfigEntry<float> BearSaddleScale;
        internal static ConfigEntry<string> BearSaddleAttachOffset;

        private static readonly Dictionary<string, SpeciesSettings> s_species = new Dictionary<string, SpeciesSettings>();

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MigrateOldFiles();
            MainConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, FilePrefix + ".cfg"), true, Info.Metadata);

            // Archivo propio con la lista de animales; BepInEx lo crea en BepInEx/config al cargar el mod.
            AnimalsConfig = new ConfigFile(Path.Combine(Paths.ConfigPath, FilePrefix + ".Animals.cfg"), true, Info.Metadata);
            AutoDetectAnimals = AnimalsConfig.Bind("0 - General", "AutoDetectOtherAnimals", true,
                "Also make tameable any other passive animals (animal AI, the ones that flee) found in the game, besides the ones listed below. " +
                "When detected, a [Creature X] section is added here so they can be disabled. " +
                "Animals that are tameable in vanilla (boar, wolf, lox, hen, asksvin...) are never modified and are always tameable.");
            ExtraCreatures = AnimalsConfig.Bind("0 - General", "ExtraCreatures", "",
                "Comma-separated list of additional prefabs to make tameable. Bosses are not allowed. " +
                "The full list of creatures that can be added is in " + ModGuid + ".AvailableCreatures.txt " +
                "(generated when entering a world). Creatures that are already tameable in vanilla are ignored.");
            foreach (string animal in s_knownAnimals)
            {
                GetSpecies(animal);
            }

            TamingTime = MainConfig.Bind("2 - Taming", "TamingTime", 900f,
                "Seconds of taming (while fed and calm) needed to tame the animal. Vanilla boar: 1800.");
            FedDuration = MainConfig.Bind("2 - Taming", "FedDuration", 600f,
                "Seconds the animal stays fed after eating.");
            FoodSearchRange = MainConfig.Bind("2 - Taming", "FoodSearchRange", 10f,
                "Distance at which the animal notices food dropped on the ground.");
            TamedFleeFromMonsters = MainConfig.Bind("2 - Taming", "TamedFleeFromMonsters", true,
                "true = tamed passive animals (deer, hare, seal...) still flee from nearby monsters and predators; " +
                "false = once tamed they are never scared. They are never scared of other passive animals or other pets.");
            TamedFearRange = MainConfig.Bind("2 - Taming", "TamedFearRange", 10f,
                "Distance (m) a monster must be within to scare a tamed animal. " +
                "Wild animals use the normal vanilla sight and hearing.");
            FollowCommandEnabled = MainConfig.Bind("7 - Follow", "EnableFollowCommand", true,
                "Follow / stay command for ALL tamed animals (this mod's and vanilla ones: boar, lox, hen, moose, asksvin...).\n" +
                "true = press Use (E) to pet, HOLD Use (E) to make the animal follow you or stay.\n" +
                "false = disabled: animals can only be petted and never follow you (so they don't travel through portals with you either). " +
                "Wolves keep their vanilla behaviour (a single press of E) either way.");

            PregnancyDuration = MainConfig.Bind("3 - Breeding", "PregnancyDuration", 600f,
                "Seconds the pregnancy lasts before the offspring is born.");
            PregnancyChance = MainConfig.Bind("3 - Breeding", "PregnancyChance", 0.5f,
                "Chance (0-1) that each attempt to gain 'love' is SKIPPED (same as vanilla: higher = slower).");
            RequiredLovePoints = MainConfig.Bind("3 - Breeding", "RequiredLovePoints", 4,
                "Love points needed to become pregnant (one every ~10 s on success).");
            MaxCreatures = MainConfig.Bind("3 - Breeding", "MaxCreatures", 6,
                "Maximum adults + offspring of the same species within range for breeding to continue.");
            TotalCheckRange = MainConfig.Bind("3 - Breeding", "TotalCheckRange", 10f,
                "Radius in which the population is counted for MaxCreatures.");
            PartnerCheckRange = MainConfig.Bind("3 - Breeding", "PartnerCheckRange", 3f,
                "Maximum distance to the partner.");

            GrowTime = MainConfig.Bind("4 - Offspring", "GrowTime", 1800f,
                "Seconds it takes for offspring to grow into an adult.");
            BabyScale = MainConfig.Bind("4 - Offspring", "BabyScale", 0.5f,
                "Offspring size relative to the adult (0.5 = half size).");
            BabyHealthFactor = MainConfig.Bind("4 - Offspring", "BabyHealthFactor", 0.5f,
                "Offspring health multiplier relative to the adult.");

            PortalTravelEnabled = MainConfig.Bind("5 - Portals", "AnimalsCanUsePortals", true,
                "true = tamed animals following you travel through portals with you; " +
                "false = no animal travels through portals (vanilla behaviour: they stay behind). " +
                "Each of this mod's animals can also be set with CanUsePortals in " + ModGuid + ".Animals.cfg.");
            PortalTravelRadius = MainConfig.Bind("5 - Portals", "Radius", 20f,
                "Maximum distance from you for an animal to travel with you when you enter the portal.");
            PortalTravelMaxAnimals = MainConfig.Bind("5 - Portals", "MaxAnimals", 10,
                "Maximum animals that travel with you per teleport (the closest ones are chosen).");
            PortalTravelVanillaTames = MainConfig.Bind("5 - Portals", "IncludeVanillaTames", true,
                "Also include vanilla tameables (wolves, boars, lox...), not only this mod's animals.");

            SaddleColorsEnabled = MainConfig.Bind("6 - Saddles", "SaddleColors", true,
                "true = saddles (lox, moose, asksvin and bear) can be crafted in blue, red, green, black, white, yellow or orange " +
                "using the style button at the crafting station, like capes and shields.");
            BearSaddleEnabled = MainConfig.Bind("6 - Saddles", "BearSaddle", true,
                "true = adds a bear saddle that lets you ride tamed Bjorn like a lox.");
            BearSaddleRecipe = MainConfig.Bind("6 - Saddles", "BearSaddleRecipe", "BjornHide:4,LeatherScraps:8,Bronze:2",
                "Bear saddle ingredients, as Item:Amount separated by commas (item prefab names).");
            BearSaddleStation = MainConfig.Bind("6 - Saddles", "BearSaddleStation", "piece_workbench",
                "Crafting station prefab for the bear saddle (e.g. piece_workbench, forge).");
            BearSaddleStationLevel = MainConfig.Bind("6 - Saddles", "BearSaddleStationLevel", 2,
                "Minimum crafting station level for the bear saddle.");
            BearSaddleOffset = MainConfig.Bind("6 - Saddles", "BearSaddleOffset", "0,0,0",
                "Extra position (x,y,z in metres) of the saddle on the bear, added to the automatic fit. " +
                "Easiest to tune in game with the console command ta_bearsaddle.");
            BearSaddleScale = MainConfig.Bind("6 - Saddles", "BearSaddleScale", 1f,
                "Size multiplier of the saddle on the bear, applied to the automatic fit.");
            BearSaddleAttachOffset = MainConfig.Bind("6 - Saddles", "BearSaddleAttachOffset", "0,0,0",
                "Extra position (x,y,z in metres) of the rider on the bear saddle.");

            DebugScareLog = MainConfig.Bind("9 - Debug", "DebugScareLog", false,
                "true = writes to BepInEx/LogOutput.log what scares each tamed animal ([Scare] lines).");

            new Harmony(ModGuid).PatchAll();
            // Por si el idioma ya se cargÃ³ antes que el mod.
            Translations.Register(Localization.instance, Localization.instance.GetSelectedLanguage());
            Log.LogInfo(ModName + " " + ModVersion + " loaded.");
        }

        /// <summary>Renombra los archivos de versiones anteriores (adrirf7.TameableAnimals.*) conservando los ajustes.</summary>
        private static void MigrateOldFiles()
        {
            string[] suffixes = { ".cfg", ".Animals.cfg", ".AvailableCreatures.txt" };
            foreach (string suffix in suffixes)
            {
                string oldPath = Path.Combine(Paths.ConfigPath, ModGuid + suffix);
                string newPath = Path.Combine(Paths.ConfigPath, FilePrefix + suffix);
                try
                {
                    if (!File.Exists(oldPath))
                    {
                        continue;
                    }
                    if (File.Exists(newPath))
                    {
                        File.Delete(oldPath);
                    }
                    else
                    {
                        File.Move(oldPath, newPath);
                    }
                }
                catch (Exception e)
                {
                    Log.LogWarning("Could not migrate " + oldPath + ": " + e.Message);
                }
            }
        }

        internal static SpeciesSettings GetSpecies(string prefabName)
        {
            if (s_species.TryGetValue(prefabName, out SpeciesSettings settings))
            {
                return settings;
            }

            SpeciesDefaults defaults = SpeciesDefaults.For(prefabName);
            string section = "Creature " + prefabName;
            ConfigFile config = AnimalsConfig;
            settings = new SpeciesSettings
            {
                Enabled = config.Bind(section, "Enabled", defaults.Enabled,
                    "true = " + prefabName + " can be tamed and bred; false = it stays wild as in vanilla."),
                Foods = config.Bind(section, "Foods", defaults.Foods,
                    "Food prefabs (comma-separated) to drop on the ground to feed it."),
                CanUsePortals = config.Bind(section, "CanUsePortals", true,
                    "true = a tamed " + prefabName + " travels through portals with you while following you; false = it stays behind."),
                BabyName = config.Bind(section, "BabyName", "",
                    "Name shown for the offspring. Empty = automatic name in the game's language (e.g. Fawn / Cervatillo)."),
                BabyModel = config.Bind(section, "BabyModel", defaults.BabyModel,
                    "Prefab whose model the offspring uses. Empty = the adult's own model, scaled down (BabyScale). " +
                    "If a vanilla baby prefab is given (e.g. Seal_Pup) its model is used at normal size."),
                BabyTint = config.Bind(section, "BabyTint", defaults.BabyTint,
                    "Colour multiplied over the offspring texture, format R,G,B (1,1,1 = no change; values >1 brighten).")
            };
            s_species[prefabName] = settings;
            return settings;
        }

        internal static bool IsKnownAnimal(string prefabName)
        {
            return Array.IndexOf(s_knownAnimals, prefabName) >= 0;
        }

        internal static bool IsBlocked(string prefabName)
        {
            return Array.IndexOf(s_blockedCreatures, prefabName) >= 0;
        }

        /// <summary>Prefabs usados como modelo de crÃ­a por alguna especie activa (p. ej. Seal_Pup): no se tratan como adultos.</summary>
        internal static bool IsBabyModel(string prefabName)
        {
            foreach (SpeciesSettings settings in s_species.Values)
            {
                if (settings.Enabled.Value && string.Equals(settings.BabyModel.Value.Trim(), prefabName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        internal static HashSet<string> ParseList(string value)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }
            foreach (string part in value.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }

        internal static Vector3 ParseVector3(string value)
        {
            string[] parts = (value ?? "").Split(',');
            if (parts.Length >= 3
                && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return new Vector3(x, y, z);
            }
            return Vector3.zero;
        }

        internal static string FormatVector3(Vector3 v)
        {
            return v.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.###", CultureInfo.InvariantCulture)
                + "," + v.z.ToString("0.###", CultureInfo.InvariantCulture);
        }

        internal static Color ParseTint(string value)
        {
            string[] parts = (value ?? "").Split(',');
            if (parts.Length >= 3
                && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
                && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
                && float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            {
                return new Color(r, g, b, 1f);
            }
            Log.LogWarning("Invalid BabyTint '" + value + "', using 1,1,1.");
            return Color.white;
        }
    }

    internal class SpeciesSettings
    {
        public ConfigEntry<bool> Enabled;
        public ConfigEntry<string> Foods;
        public ConfigEntry<bool> CanUsePortals;
        public ConfigEntry<string> BabyName;
        public ConfigEntry<string> BabyModel;
        public ConfigEntry<string> BabyTint;
    }

    internal struct SpeciesDefaults
    {
        public bool Enabled;
        public string Foods;
        public string BabyModel;
        public string BabyTint;

        public static SpeciesDefaults For(string prefabName)
        {
            var defaults = new SpeciesDefaults
            {
                Enabled = true,
                Foods = "Raspberry,Blueberries,Carrot,Turnip,RawMeat",
                BabyModel = "",
                BabyTint = "1.25,1.05,0.8"
            };
            switch (prefabName)
            {
                case "Deer":
                    defaults.Foods = "Raspberry,Blueberries,Carrot,Turnip,Cloudberry";
                    defaults.BabyTint = "1.3,1.05,0.7";
                    break;
                case "Hare":
                    defaults.Foods = "Carrot,Turnip,Onion,Cloudberry,Raspberry,Blueberries";
                    defaults.BabyTint = "1.25,1.0,0.75";
                    break;
                case "Seal":
                    defaults.Foods = "FishRaw,Fish1,Fish2,Fish3,Fish6,Fish7,Fish10";
                    defaults.BabyModel = "Seal_Pup";
                    defaults.BabyTint = "1.1,1.1,1.15";
                    break;
                case "Bjorn":
                    defaults.Foods = "RawMeat,DeerMeat,FishRaw,Honey,Raspberry,Blueberries";
                    defaults.BabyTint = "1.3,1.1,0.8";
                    break;
                case "Unbjorn":
                    // Desactivado por defecto; se puede activar en el config.
                    defaults.Enabled = false;
                    defaults.Foods = "Entrails,RawMeat,DeerMeat";
                    defaults.BabyTint = "1.2,1.2,1.25";
                    break;
                case "Bat":
                    defaults.Foods = "Raspberry,Blueberries,Cloudberry,Entrails";
                    break;
            }
            return defaults;
        }
    }
}
