using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Vecna
{
    public static class ModelReplacementAPISoftCompat
    {
        private static FieldInfo fiController;
        private static Type viewStateType;
        public static bool BypassCosmeticPrefix = false;

        public static void Patch(Harmony harmony)
        {
            try
            {
                Type viewStateManagerType = Type.GetType("ModelReplacement.ViewStateManager, ModelReplacementAPI");
                if (viewStateManagerType != null)
                {
                    MethodInfo setPlayerRenderers = AccessTools.Method(viewStateManagerType, "SetPlayerRenderers");
                    if (setPlayerRenderers != null)
                    {
                        HarmonyMethod prefix = new HarmonyMethod(AccessTools.Method(typeof(ModelReplacementAPISoftCompat), nameof(SetPlayerRenderers)));
                        harmony.Patch(setPlayerRenderers, prefix: prefix);
                    }

                    MethodInfo getViewState = AccessTools.Method(viewStateManagerType, "GetViewState");
                    if (getViewState != null)
                    {
                        HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(ModelReplacementAPISoftCompat), nameof(GetViewState)));
                        harmony.Patch(getViewState, postfix: postfix);
                        viewStateType = Type.GetType("ModelReplacement.ViewState, ModelReplacementAPI");
                    }

                    Type managerBaseType = Type.GetType("ModelReplacement.Monobehaviors.ManagerBase, ModelReplacementAPI");
                    if (managerBaseType != null)
                        fiController = AccessTools.Field(managerBaseType, "controller");

                    //Plugin.Logger.LogInfo("VECNA: Successfully patched ModelReplacementAPI reflectively for haunt culling.");
                }

                Type cosmeticManagerType = Type.GetType("ModelReplacement.MoreCompanyCosmeticManager, ModelReplacementAPI");
                if (cosmeticManagerType != null)
                {
                    string[] methodsToPatch = { "Update", "LateUpdate", "UpdateModelReplacement" };
                    foreach (string methodName in methodsToPatch)
                    {
                        MethodInfo method = AccessTools.Method(cosmeticManagerType, methodName);
                        if (method != null)
                        {
                            HarmonyMethod prefix = new HarmonyMethod(AccessTools.Method(typeof(ModelReplacementAPISoftCompat), nameof(MoreCompanyCosmeticManagerPrefix)));
                            harmony.Patch(method, prefix: prefix);
                            //Plugin.Logger.LogInfo($"VECNA: Successfully patched MoreCompanyCosmeticManager.{methodName} reflectively.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"VECNA: Error patching ModelReplacementAPI: {ex}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void SetPlayerRenderers(object __instance, ref bool enabled, ref bool helmetShadow)
        {
            PlayerControllerB player = (PlayerControllerB)(fiController?.GetValue(__instance));
            if (player != null && HauntVisibilityRegistry.IsHidden(player.gameObject))
            {
                if (enabled) enabled = false;
                if (helmetShadow) helmetShadow = false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void GetViewState(object __instance, ref object __result)
        {
            if (viewStateType != null)
            {
                PlayerControllerB player = (PlayerControllerB)(fiController?.GetValue(__instance));
                if (player != null && HauntVisibilityRegistry.IsHidden(player.gameObject))
                    __result = Enum.Parse(viewStateType, "None");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static bool MoreCompanyCosmeticManagerPrefix(object __instance)
        {
            if (BypassCosmeticPrefix) return true;
            if (__instance is MonoBehaviour behaviour)
            {
                PlayerControllerB player = behaviour.GetComponentInParent<PlayerControllerB>();
                if (player != null && HauntVisibilityRegistry.IsHidden(player.gameObject))
                {
                    // Force disable cosmetics reflectively
                    try
                    {
                        Type cosmeticAppType = Type.GetType("MoreCompany.Cosmetics.CosmeticApplication, MoreCompany");
                        if (cosmeticAppType != null)
                        {
                            var cosmeticApp = player.GetComponentInChildren(cosmeticAppType);
                            if (cosmeticApp != null)
                            {
                                var spawnedCosmeticsField = cosmeticAppType.GetField("spawnedCosmetics", BindingFlags.Public | BindingFlags.Instance);
                                if (spawnedCosmeticsField != null)
                                {
                                    var list = spawnedCosmeticsField.GetValue(cosmeticApp) as System.Collections.IList;
                                    if (list != null)
                                    {
                                        int disabledCount = 0;
                                        foreach (var cosmeticObj in list)
                                        {
                                            if (cosmeticObj is MonoBehaviour cosmeticBehaviour)
                                            {
                                                if (cosmeticBehaviour.gameObject.activeSelf)
                                                {
                                                    cosmeticBehaviour.gameObject.SetActive(false);
                                                    disabledCount++;
                                                }
                                            }
                                        }
                                        if (disabledCount > 0)
                                        {
                                            //Plugin.Logger.LogInfo($"VECNA: Force disabled {disabledCount} MoreCompany cosmetics inside MoreCompanyCosmeticManagerPrefix.");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Plugin.Logger.LogError($"VECNA: Error reflectively disabling cosmetics in MoreCompanyCosmeticManagerPrefix: {ex}");
                    }
                    return false; // Skip coordinator update so cosmetics stay hidden
                }
            }
            return true;
        }
    }

    public static class MoreCompanySoftCompat
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                Type cosmeticApplicationType = Type.GetType("MoreCompany.Cosmetics.CosmeticApplication, MoreCompany");
                if (cosmeticApplicationType != null)
                {
                    MethodInfo updateAllCosmeticVisibilities = AccessTools.Method(cosmeticApplicationType, "UpdateAllCosmeticVisibilities");
                    if (updateAllCosmeticVisibilities != null)
                    {
                        HarmonyMethod prefix = new HarmonyMethod(AccessTools.Method(typeof(MoreCompanySoftCompat), nameof(UpdateAllCosmeticVisibilities)));
                        harmony.Patch(updateAllCosmeticVisibilities, prefix: prefix);
                        //Plugin.Logger.LogInfo("VECNA: Successfully patched MoreCompany reflectively for haunmt culling.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"VECNA: Error patching MoreCompany: {ex}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool UpdateAllCosmeticVisibilities(object __instance)
        {
            if (__instance is MonoBehaviour behaviour)
            {
                GameObject owner =
                    behaviour.GetComponentInParent<PlayerControllerB>()?.gameObject
                    ?? behaviour.GetComponentInParent<EnemyAI>()?.gameObject
                    ?? behaviour.transform.root.gameObject;

                if (owner != null && HauntVisibilityRegistry.IsHidden(owner.gameObject))
                {
                    // Access spawnedCosmetics reflectively and force disable themm
                    try
                    {
                        var spawnedCosmeticsField = __instance.GetType().GetField("spawnedCosmetics", BindingFlags.Public | BindingFlags.Instance);
                        if (spawnedCosmeticsField != null)
                        {
                            var list = spawnedCosmeticsField.GetValue(__instance) as System.Collections.IList;
                            if (list != null)
                            {
                                int disabledCount = 0;
                                foreach (var cosmeticObj in list)
                                {
                                    if (cosmeticObj is MonoBehaviour cosmeticBehaviour)
                                    {
                                        if (cosmeticBehaviour.gameObject.activeSelf)
                                        {
                                            cosmeticBehaviour.gameObject.SetActive(false);
                                            disabledCount++;
                                        }
                                    }
                                }
                                if (disabledCount > 0)
                                {
                                    //Plugin.Logger.LogInfo($"VECNA: Force disabled {disabledCount} MoreCompany cosmetics on hidden entity.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Plugin.Logger.LogError($"VECNA: Error reflectively disabling MoreCompany cosmetics: {ex}");
                    }
                    return false; // Skip MoreCompany's own enabling/ddisabling loops
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GrabbableObject))]
    public static class GrabbableObjectPatch
    {
        [HarmonyPatch(nameof(GrabbableObject.EnableItemMeshes))]
        [HarmonyPrefix]
        private static void PrefixEnableItemMeshes(GrabbableObject __instance, ref bool enable)
        {
            if (__instance != null && __instance.playerHeldBy != null && HauntVisibilityRegistry.IsHidden(__instance.playerHeldBy.gameObject))
            {
                enable = false;
                HauntVisibilityRegistry.Hide(__instance.gameObject, "VecnaHaunt");
            }
        }

        [HarmonyPatch(nameof(GrabbableObject.DiscardItemOnClient))]
        [HarmonyPostfix]
        private static void PostfixDiscardItemOnClient(GrabbableObject __instance)
        {
            if (__instance != null)
            {
                // Remove item from registry since it is no longer held
                HauntVisibilityRegistry.Restore(__instance.gameObject, "VecnaHaunt");

                // If haunt is active and local player is the victim, ground items should be hidden from then
                foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
                {
                    if (vecna != null && vecna.currentLocalPhase == VecnaPhase.HauntChase && vecna.IsVictimOrSpectatingVictim())
                    {
                        //Plugin.Logger.LogInfo($"VECNA: Re-hiding dropped item {__instance.itemProperties.itemName} for local victim player.");
                        HauntVisibilityRegistry.Hide(__instance.gameObject, "VecnaHaunt");
                        break;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB))]
    public static class PlayerControllerBPatch
    {
        [HarmonyPatch("GrabObjectClientRpc")]
        [HarmonyPostfix]
        private static void PostfixGrabObjectClientRpc(PlayerControllerB __instance, bool grabValidated, Unity.Netcode.NetworkObjectReference grabbedObject)
        {
            if (grabValidated && __instance != null && HauntVisibilityRegistry.IsHidden(__instance.gameObject))
            {
                Unity.Netcode.NetworkObjectReference localRef = grabbedObject;
                if (localRef.TryGet(out var networkObject))
                {
                    GrabbableObject component = networkObject.gameObject.GetComponentInChildren<GrabbableObject>();
                    if (component != null)
                    {
                        //Plugin.Logger.LogInfo($"VECNA: Hiding grabbed item {component.itemProperties.itemName} for hidden player: {__instance.playerUsername}");
                        HauntVisibilityRegistry.Hide(component.gameObject, "VecnaHaunt");
                    }
                }
            }
        }

        [HarmonyPatch(nameof(PlayerControllerB.ChangeHelmetLight))]
        [HarmonyPrefix]
        private static void PrefixChangeHelmetLight(PlayerControllerB __instance, ref bool enable)
        {
            if (__instance != null && HauntVisibilityRegistry.IsHidden(__instance.gameObject))
            {
                //Plugin.Logger.LogInfo($"VECNA: Intercepted and disabled ChangeHelmetLight for hidden player: {__instance.playerUsername}");
                enable = false;
            }
        }
    }

    [HarmonyPatch(typeof(FlashlightItem))]
    public static class FlashlightItemPatch
    {
        [HarmonyPatch(nameof(FlashlightItem.SwitchFlashlight))]
        [HarmonyPrefix]
        private static void PrefixSwitchFlashlight(FlashlightItem __instance, ref bool on)
        {
            if (__instance != null && __instance.playerHeldBy != null && HauntVisibilityRegistry.IsHidden(__instance.playerHeldBy.gameObject))
            {
                //Plugin.Logger.LogInfo($"VECNA: Forcing SwitchFlashlight to false for hidden player: {__instance.playerHeldBy.playerUsername}");
                on = false; // Intercept helmet light and bulb light activations
            }
        }

        [HarmonyPatch(nameof(FlashlightItem.SwitchFlashlight))]
        [HarmonyPostfix]
        private static void PostfixSwitchFlashlight(FlashlightItem __instance)
        {
            if (__instance != null && __instance.playerHeldBy != null && HauntVisibilityRegistry.IsHidden(__instance.playerHeldBy.gameObject))
            {
                HauntVisibilityRegistry.Hide(__instance.gameObject, "VecnaHaunt");
            }
        }

        [HarmonyPatch(nameof(FlashlightItem.PocketFlashlightClientRpc))]
        [HarmonyPostfix]
        private static void PostfixPocketFlashlight(FlashlightItem __instance)
        {
            if (__instance != null && __instance.playerHeldBy != null && HauntVisibilityRegistry.IsHidden(__instance.playerHeldBy.gameObject))
            {
                HauntVisibilityRegistry.Hide(__instance.gameObject, "VecnaHaunt");
            }
        }

        [HarmonyPatch(nameof(FlashlightItem.PocketItem))]
        [HarmonyPostfix]
        private static void PostfixPocketItem(FlashlightItem __instance)
        {
            if (__instance != null && __instance.playerHeldBy != null && HauntVisibilityRegistry.IsHidden(__instance.playerHeldBy.gameObject))
            {
                //Plugin.Logger.LogInfo($"VECNA: Disabling helmet light on pocket for hidden player: {__instance.playerHeldBy.playerUsername}");
                __instance.playerHeldBy.helmetLight.enabled = false;
            }
        }

        [HarmonyPatch(nameof(FlashlightItem.PocketFlashlightClientRpc))]
        [HarmonyPostfix]
        private static void PostfixPocketFlashlightClientRpc(FlashlightItem __instance)
        {
            if (__instance != null && __instance.playerHeldBy != null && HauntVisibilityRegistry.IsHidden(__instance.playerHeldBy.gameObject))
            {
                //Plugin.Logger.LogInfo($"VECNA: Disabling helmet light on pocket RPC for hidden player: {__instance.playerHeldBy.playerUsername}");
                __instance.playerHeldBy.helmetLight.enabled = false;
            }
        }
    }
}

