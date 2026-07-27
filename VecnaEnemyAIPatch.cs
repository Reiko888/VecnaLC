
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

    }

    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    public class VecnaVoiceMutePatch
    {
        private static HashSet<PlayerControllerB> mutedByVecna = new HashSet<PlayerControllerB>();

        [HarmonyPostfix]
        public static void MuteUpsideDownVoices(PlayerControllerB __instance)
        {
            if (__instance == null || !__instance.isPlayerControlled || __instance.isPlayerDead)
            {
                if (mutedByVecna.Contains(__instance))
                {
                    if (__instance != null && __instance.currentVoiceChatAudioSource != null)
                    {
                        __instance.currentVoiceChatAudioSource.volume = 1f;
                    }
                    mutedByVecna.Remove(__instance);
                }
                return;
            }

            if (__instance.currentVoiceChatAudioSource == null) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

            if (localPlayer == null || localPlayer == __instance || localPlayer.isPlayerDead)
            {
                if (mutedByVecna.Contains(__instance))
                {
                    __instance.currentVoiceChatAudioSource.volume = 1f;
                    mutedByVecna.Remove(__instance);
                }
                return;
            }

            bool localInTrance = VecnaAI.IsPlayerInUpsideDown(localPlayer);
            bool targetInTrance = VecnaAI.IsPlayerInUpsideDown(__instance);

            if (localInTrance != targetInTrance)
            {
                __instance.currentVoiceChatAudioSource.volume = 0f;
                mutedByVecna.Add(__instance);
            }
            else if (mutedByVecna.Contains(__instance))
            {
                __instance.currentVoiceChatAudioSource.volume = 1f;
                mutedByVecna.Remove(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "TeleportPlayer")]
    public class VecnaTeleportInterceptPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(PlayerControllerB __instance, Vector3 pos)
        {
            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null && vecna.cursingPlayer == __instance)
                {
                    if (vecna.isTeleportingVictimFromVecna) return true;

                    bool isTranced = (vecna.currentLocalPhase == VecnaPhase.HauntChase);

                    if (isTranced)
                    {
                        vecna.localVictimClonePos = pos;
                        vecna.cloneWasTeleportedToShip = true;
                        if (vecna.activeClone != null)
                        {
                            vecna.activeClone.transform.position = pos;
                        }
                        //Debug.Log($"VECNA: Teleporter intercepted. Clone moved to {pos}, Victim stayed.");
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
                        bool isInvisiblePhase = vecna.isHuntingEveryone || (vecna.currentLocalPhase == VecnaPhase.HauntChase);

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
                        value *= 0f;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayAudioAnimationEvent), "PlayAudio1RandomClip")]
    public class VecnaPlayerCloneSnapParticlesPatch
    {
        private static int bloodSpurtIndex = 0;

        [HarmonyPostfix]
        public static void Postfix(PlayAudioAnimationEvent __instance)
        {
            VecnaAI matchingVecna = null;

            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null && vecna.activeClone != null && __instance.transform.IsChildOf(vecna.activeClone.transform))
                {
                    matchingVecna = vecna;
                    break;
                }
            }

            if (matchingVecna == null) return;

            string targetParticleName = "BloodSpurt" + (bloodSpurtIndex + 1);
            Transform particleTransform = FindChildRecursive(matchingVecna.activeClone.transform, targetParticleName);
            if (particleTransform != null)
            {
                ParticleSystem ps = particleTransform.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                }
            }

            bloodSpurtIndex = (bloodSpurtIndex + 1) % 4;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform result = FindChildRecursive(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(RoundManager))]
    public class VecnaScrapSyncPatch
    {
        [HarmonyPatch("SyncScrapValuesClientRpc")]
        [HarmonyPostfix]
        public static void Postfix(Unity.Netcode.NetworkObjectReference[] spawnedScrap)
        {
            if (spawnedScrap == null) return;
            foreach (var scrapRef in spawnedScrap)
            {
                if (scrapRef.TryGet(out Unity.Netcode.NetworkObject netObj))
                {
                    GrabbableObject go = netObj.GetComponent<GrabbableObject>();
                    if (go != null)
                    {
                        VecnaAI.levelSpawnedScrap.Add(go);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "KillPlayerServerRpc")]
    public class VecnaKillPlayerServerRpcPatch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerControllerB __instance, int playerId)
        {
            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null && vecna.IsServer && vecna.cursingPlayer == __instance && vecna.currentLocalPhase == VecnaPhase.HauntChase)
                {
                    vecna.ResetHaunt(repelledByMusic: false, playerKilled: true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "SpawnDeadBody")]
    public class VecnaSpawnDeadBodyPatch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerControllerB deadPlayerController, ref Vector3 positionOffset)
        {
            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null && vecna.cursingPlayer == deadPlayerController && vecna.currentLocalPhase == VecnaPhase.HauntChase)
                {
                    Vector3 clonePos = (vecna.activeClone != null) ? vecna.activeClone.transform.position 
                                     : (vecna.localVictimClonePos != Vector3.zero ? vecna.localVictimClonePos 
                                     : deadPlayerController.transform.position);

                    positionOffset = (clonePos + Vector3.up * 0.1f) - deadPlayerController.thisPlayerBody.position;
                }
            }
        }
    }
}
