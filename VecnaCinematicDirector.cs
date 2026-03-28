using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;

namespace Vecna
{
    [Serializable]
    public class VecnaCinematicDirector
    {
        private VecnaAI vecnaBrain;
        
        public GameObject activeFakeBody = null;

        public VecnaCinematicDirector(VecnaAI brain)
        {
            this.vecnaBrain = brain;
            VecnaVFXHelper.PrewarmPools();
        }

        public IEnumerator ExecuteCinematicKill()
        {
            if (this.vecnaBrain.IsServer) this.vecnaBrain.currentPhase.Value = VecnaAI.VecnaPhase.ExecutingKill;

            float killDuration = 6.8f;
            float elapsed = 0f;

            while (elapsed < killDuration)
            {
                if (this.vecnaBrain.cursingPlayer == null || this.vecnaBrain.cursingPlayer.isPlayerDead) break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            this.vecnaBrain.ResetHaunt(repelledByMusic: false, playerKilled: true);

            this.vecnaBrain.cursingPlayer = null;
        }

        public IEnumerator CinematicClockDespawnRoutine(GameObject clockToDestroy, bool isFinalClock)
        {
            if (clockToDestroy == null) yield break;

            bool canSeeHallucinations = this.vecnaBrain.IsVictimOrSpectatingVictim();

            if (isFinalClock)
            {
                if (canSeeHallucinations) VecnaVFXHelper.CreateMassiveBloodSplash(clockToDestroy.transform.position, false);
            }
            else
            {
                if (canSeeHallucinations) VecnaVFXHelper.CreateMindFlayerDust(clockToDestroy.transform.position);
            }

            if (this.activeFakeBody != null)
            {
                UnityEngine.Object.Destroy(this.activeFakeBody);
                this.activeFakeBody = null;
            }

            UnityEngine.Object.Destroy(clockToDestroy);
        }
    }
}