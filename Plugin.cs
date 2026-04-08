﻿﻿﻿using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalLib.Modules;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Vecna.Configuration;

namespace Vecna
{
    [BepInPlugin("Reiko88.Vecna", "Vecna", "1.1.0")]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;
        internal static PluginConfig BoundConfig { get; private set; } = null!;

        public static AssetBundle? ModAssets;
        public static GameObject? ClockPrefab;
        public static GameObject? ClonePrefab;

        private void Awake()
        {
            Harmony harmony = new Harmony("Reiko88.Vecna");
            harmony.PatchAll(typeof(TerminalKeywordPatch));
            harmony.PatchAll(typeof(VecnaEnemyAIPatch));
            harmony.PatchAll(typeof(VecnaAudioMixerPatch));
            harmony.PatchAll(typeof(VecnaEventManager));
            harmony.PatchAll(typeof(VecnaTeleportInterceptPatch));
            
            Logger = base.Logger;
            BoundConfig = new PluginConfig(base.Config);
            InitializeNetworkBehaviours();

            string mainBundlePath = Path.Combine(Path.GetDirectoryName(Info.Location), "vecnabundle");
            ModAssets = AssetBundle.LoadFromFile(mainBundlePath);
            if (ModAssets == null)
            {
                Logger.LogError("Failed to load vecnabundle bundle.");
                return;
            }

            ClockPrefab = ModAssets.LoadAsset<GameObject>("GrandfatherClock");
            Logger.LogInfo("Vecna Clock Prefab loaded successfully!");

            ClonePrefab = ModAssets.LoadAsset<GameObject>("Scavenger");
            Logger.LogInfo("player clone Prefab loaded successfully!");

            

            var vecnaEnemy = ModAssets.LoadAsset<EnemyType>("vecnaEnemy");
            var vecnaTN = ModAssets.LoadAsset<TerminalNode>("VecnaTN");
            var vecnaTK = ModAssets.LoadAsset<TerminalKeyword>("VecnaTK");

            if (vecnaEnemy == null)
            {
                Logger.LogError("Failed to load Vecna EnemyType from Asset Bundle!");
                return;
            }

            NetworkPrefabs.RegisterNetworkPrefab(vecnaEnemy.enemyPrefab);

            Dictionary<Levels.LevelTypes, int> vanillaWeights = new Dictionary<Levels.LevelTypes, int>
            {
                { Levels.LevelTypes.TitanLevel, 50 },
                { Levels.LevelTypes.RendLevel, 75 },
                { Levels.LevelTypes.DineLevel, 55 },
                { Levels.LevelTypes.OffenseLevel, 30 },
                { Levels.LevelTypes.MarchLevel, 30 },
                { Levels.LevelTypes.ExperimentationLevel, 10 },
                { Levels.LevelTypes.AssuranceLevel, 20 },
                { Levels.LevelTypes.VowLevel, 20 },
                { Levels.LevelTypes.ArtificeLevel, 65 },
                { Levels.LevelTypes.EmbrionLevel, 100 },
                { Levels.LevelTypes.AdamanceLevel, 55 },
                { Levels.LevelTypes.Modded, 45 }
        };
            Dictionary<string, int> customWeights = new Dictionary<string, int>();

            Enemies.RegisterEnemy(vecnaEnemy, vanillaWeights, customWeights, vecnaTN, vecnaTK);

            Logger.LogInfo("Plugin Vecna is loaded and assets are registered!");
        }

        private static void InitializeNetworkBehaviours()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0) method.Invoke(null, null);
                }
            }
        }

        //TERMNINAL KEYWORD INJECTION PATCH TO ENSURE THEY'RE REGISTERED
        [HarmonyPatch(typeof(Terminal))]
        public class TerminalKeywordPatch
        {
            [HarmonyPatch("Awake")]
            [HarmonyPostfix]
            public static void AddExtraKeywords(Terminal __instance)
            {
                var henryTK = ModAssets.LoadAsset<TerminalKeyword>("HenryTK");
                var creelTK = ModAssets.LoadAsset<TerminalKeyword>("CreelTK");

                if (henryTK == null || creelTK == null) return;

                var keywordList = __instance.terminalNodes.allKeywords.ToList();

                if (!keywordList.Contains(henryTK)) keywordList.Add(henryTK);
                if (!keywordList.Contains(creelTK)) keywordList.Add(creelTK);

                __instance.terminalNodes.allKeywords = keywordList.ToArray();

                Plugin.Logger.LogInfo("Successfully injected extra Vecna keywords into the Terminal!");
            }
            //TESTING CODE TO UNLOCK BESTIARY PAGE
            //[HarmonyPatch("Start")]
            //[HarmonyPostfix]
            //public static void AutoUnlockVecna(Terminal __instance)
            //{
            //    if (__instance.scannedEnemyIDs == null)
            //    {
            //        __instance.scannedEnemyIDs = new System.Collections.Generic.List<int>();
            //    }

            //    foreach (TerminalNode node in __instance.enemyFiles)
            //    {
            //        if (node != null && node.creatureName == "vecna")
            //        {
            //            if (!__instance.scannedEnemyIDs.Contains(node.creatureFileID))
            //            {
            //                __instance.scannedEnemyIDs.Add(node.creatureFileID);
            //                Plugin.Logger.LogInfo("VECNA: Bestiary page auto-unlocked for testing!");
            //            }
            //            break;
            //        }
            //    }
            //}
        }
    }
}