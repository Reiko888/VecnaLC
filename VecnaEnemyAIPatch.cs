
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Vecna
{
    [HarmonyPatch(typeof(EnemyAI))]
    public class VecnaEnemyAIPatch
    {
        [HarmonyPatch("PlayerIsTargetable")]
        [HarmonyPostfix]
        private static void PreventTargeting(EnemyAI __instance, ref bool __result, PlayerControllerB playerScript)
        {
            if (__instance is VecnaAI) return;

            if (__result && VecnaAI.IsPlayerInUpsideDown(playerScript))
            {
                __result = false;
            }
        }

        [HarmonyPatch("CheckLineOfSightForClosestPlayer")]
        [HarmonyPostfix]
        private static void PreventLineOfSight(EnemyAI __instance, ref PlayerControllerB __result)
        {
            if (__instance is VecnaAI) return;

            if (__result != null && VecnaAI.IsPlayerInUpsideDown(__result))
            {
                __result = null;
            }
        }

        [HarmonyPatch("CheckLineOfSightForPlayer")]
        [HarmonyPostfix]
        private static void PreventSpecificLineOfSight(EnemyAI __instance, ref PlayerControllerB __result)
        {
            if (__instance is VecnaAI) return;

            if (__result != null && VecnaAI.IsPlayerInUpsideDown(__result))
            {
                __result = null;
            }
        }

        [HarmonyPatch("GetClosestPlayer")]
        [HarmonyPostfix]
        private static void PreventProximity(EnemyAI __instance, ref PlayerControllerB __result)
        {
            if (__instance is VecnaAI) return;

            if (__result != null && VecnaAI.IsPlayerInUpsideDown(__result))
            {
                __result = null;
            }
        }

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void HideNewSpawns(EnemyAI __instance)
        {
            if (__instance is VecnaAI) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer != null && VecnaAI.IsPlayerInUpsideDown(localPlayer))
            {
                __instance.EnableEnemyMesh(false, false);
            }
        }

        [HarmonyPatch("EnableEnemyMesh")]
        [HarmonyPrefix]
        private static void PreventEnemyVisibility(EnemyAI __instance, ref bool enable)
        {
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null) return;

            if (__instance is VecnaAI vecna)
            {
                bool shouldSeeVecna = ((vecna.currentLocalPhase == VecnaAI.VecnaPhase.Chasing) ||
                                       (vecna.currentLocalPhase == VecnaAI.VecnaPhase.ExecutingKill) ||
                                       (vecna.currentLocalPhase == VecnaAI.VecnaPhase.VehicleCinematic && !vecna.isCinematicLiftStarted))
                                       && vecna.IsVictimOrSpectatingVictim();
                enable = shouldSeeVecna;

                return;
            }

            if (VecnaAI.IsPlayerInUpsideDown(localPlayer))
            {
                enable = false;
            }
        }
    }

    [HarmonyPatch(typeof(AudioMixer), "SetFloat")]
    [HarmonyBefore(new string[] { "me.swipez.melonloader.morecompany" })] 
    public class VecnaAudioMixerPatch
    {
        [HarmonyPrefix]
        private static void UpdatePlayerVolume(string name, ref float value)
        {
            if (StartOfRound.Instance == null || !name.StartsWith("PlayerVolume")) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null || !localPlayer.isPlayerControlled || localPlayer.isPlayerDead) return;

            int length = "PlayerVolume".Length;
            string s = name.Substring(length, name.Length - length);

            if (int.TryParse(s, out var result) && result >= 0 && result < StartOfRound.Instance.allPlayerScripts.Length)
            {
                PlayerControllerB targetPlayer = StartOfRound.Instance.allPlayerScripts[result];

                if (targetPlayer != null && targetPlayer != localPlayer)
                {
                    bool localInTrance = VecnaAI.IsPlayerInUpsideDown(localPlayer);
                    bool targetInTrance = VecnaAI.IsPlayerInUpsideDown(targetPlayer);

                    if (localInTrance != targetInTrance)
                    {
                        // INSPIRED BY LEGA STRANGER THINGS MOD, CREDIT TO LEGA FOR THIS SOLUTION
                        //MULTIPLY THE VOLUME BY 0 TO MUTE IT, INSTEAD OF SETTING IT TO 0, TO PREVENT OTHER MODS/METHODS FROM OVERRIDING THIS CHANGE
                        value *= 0f;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "TeleportPlayer")]
    public class VecnaTeleportInterceptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerControllerB __instance, Vector3 pos)
        {
            if (Vector3.Distance(__instance.transform.position, pos) < 15f)
            {
                return true; 
            }

            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null && vecna.cursingPlayer == __instance)
                {
                    bool isTranced = (vecna.currentLocalPhase == VecnaAI.VecnaPhase.Chasing ||
                                      vecna.currentLocalPhase == VecnaAI.VecnaPhase.ExecutingKill);

                    if (isTranced && vecna.activeClone != null)
                    {
                        vecna.activeClone.transform.position = pos;
                        Debug.Log($"VECNA: Teleporter intercepted. Clone moved to {pos}, Victim stayed in Upside Down.");
                        return false;
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RoundManager), "PlayAudibleNoise")]
    public class VecnaNoiseSuppressionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Vector3 noisePosition)
        {
            try
            {
                foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
                {
                    if (vecna != null && vecna.cursingPlayer != null)
                    {
                        bool isInvisiblePhase = (vecna.currentPhase.Value == VecnaAI.VecnaPhase.Chasing ||
                                                 vecna.currentPhase.Value == VecnaAI.VecnaPhase.ExecutingKill);

                        if (isInvisiblePhase)
                        {
                            float distanceToVictim = Vector3.Distance(noisePosition, vecna.cursingPlayer.transform.position);
                            if (distanceToVictim < 2f)
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("VECNA: Safely caught an error in the noise patch: " + e.Message);
            }

            return true;
        }
    }

}