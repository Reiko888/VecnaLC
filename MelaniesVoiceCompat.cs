using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace Vecna
{
    public static class MelaniesVoiceCompat
    {
        private static PropertyInfo piPlayerAudioGroup;
        private static PropertyInfo piPlayerScript;
        private static PropertyInfo piGroupVolume;

        public static void Patch(Harmony harmony)
        {
            try
            {
                Type voiceControllerType = Type.GetType("com.github.zehsteam.MelaniesVoice.MonoBehaviours.VoiceController, com.github.zehsteam.MelaniesVoice");
                if (voiceControllerType != null)
                {
                    MethodInfo updateVoiceVolume = AccessTools.Method(voiceControllerType, "UpdateVoiceVolume");
                    if (updateVoiceVolume != null)
                    {
                        piPlayerAudioGroup = AccessTools.Property(voiceControllerType, "PlayerAudioGroup");
                        piPlayerScript = AccessTools.Property(voiceControllerType, "PlayerScript");

                        HarmonyMethod postfix = new HarmonyMethod(AccessTools.Method(typeof(MelaniesVoiceCompat), nameof(UpdateVoiceVolumePostfix)));
                        _ = harmony.Patch(updateVoiceVolume, postfix: postfix);
                        //Debug.Log("VECNA: Melanie's Voice soft compatibility patch applied successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VECNA: Failed to apply Melanie's Voice patch: {ex}");
            }
        }

        private static void UpdateVoiceVolumePostfix(object __instance)
        {
            if (__instance == null) return;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null || !localPlayer.isPlayerControlled || localPlayer.isPlayerDead) return;

            PlayerControllerB targetedPlayer = piPlayerScript?.GetValue(__instance, null) as PlayerControllerB;
            if (targetedPlayer != null && targetedPlayer != localPlayer)
            {
                bool localInTrance = VecnaAI.IsPlayerInUpsideDown(localPlayer);
                bool targetInTrance = VecnaAI.IsPlayerInUpsideDown(targetedPlayer);

                if (localInTrance != targetInTrance)
                {
                    object playerAudioGroup = piPlayerAudioGroup?.GetValue(__instance, null);
                    if (playerAudioGroup != null)
                    {
                        if (piGroupVolume == null)
                            piGroupVolume = AccessTools.Property(playerAudioGroup.GetType(), "Volume");

                        float currentVolume = piGroupVolume != null ? (float)piGroupVolume.GetValue(playerAudioGroup, null) : 0f;
                        if (currentVolume > 0f)
                        {
                            bool usingWalkieTalkie = (localPlayer.speakingToWalkieTalkie && targetedPlayer.holdingWalkieTalkie) || 
                                                     (targetedPlayer.speakingToWalkieTalkie && localPlayer.holdingWalkieTalkie);
                            
                            float multiplier = localInTrance
                                ? (usingWalkieTalkie ? 1f : 0.1f)
                                : (usingWalkieTalkie ? 1f : 0f);

                            if (piGroupVolume != null && piGroupVolume.CanWrite)
                                piGroupVolume.SetValue(playerAudioGroup, currentVolume * multiplier, null);
                        }
                    }
                }
            }
        }
    }
}
