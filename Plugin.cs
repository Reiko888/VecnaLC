using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Dawn;
using Dawn.Utils;
using Dusk;

namespace Vecna
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(DawnLib.PLUGIN_GUID)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string modGUID = "Reiko888.Vecna";
        public const string modName = "Vecna";
        public const string modVersion = "2.0.0";

        public static Plugin Instance = null!;
        internal static new ManualLogSource Logger = null!;
        internal static readonly Harmony harmony = new Harmony(modGUID);
        internal static DuskMod mod = null!;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            Logger = base.Logger;
            AssetBundle mainBundle = AssetBundleUtils.LoadBundle(Assembly.GetExecutingAssembly(), "vecna_cont");
            mod = DuskMod.RegisterMod(this, mainBundle);
            mod.RegisterContentHandlers();
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            MelaniesVoiceCompat.Patch(harmony);
            ModelReplacementAPISoftCompat.Patch(harmony);
            MoreCompanySoftCompat.Patch(harmony);
            Logger.LogInfo($"Plugin {modName} is loaded!");
        }
    }
}
