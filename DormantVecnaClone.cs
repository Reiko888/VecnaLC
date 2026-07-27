using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;

namespace Vecna
{
    public class DormantVecnaClone : MonoBehaviour, IHittable
    {
        public Animator cloneAnimator;
        public float detectionRadius = 15f;
        public Transform vecnaSpawnPoint;

        private bool hasTriggered = false;
        private Dictionary<PlayerControllerB, float> playerStayTimes = new Dictionary<PlayerControllerB, float>();

        public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit, bool playHitSFX = false, int hitID = -1)
        {
            if (!hasTriggered)
            {
                //Debug.Log("DormantVecnaClone: Was hit! Waking up instantly.");
                TriggerWakeUpServerRpc();
                return true;
            }
            return false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (hasTriggered) return;
            if (other.CompareTag("Player"))
            {
                PlayerControllerB player = other.GetComponent<PlayerControllerB>();
                if (player != null && !player.isPlayerDead)
                {
                    if (!playerStayTimes.ContainsKey(player)) playerStayTimes[player] = 0f;
                    playerStayTimes[player] += Time.deltaTime;
                    if (playerStayTimes[player] >= 5f)
                    {
                        //Debug.Log($"DormantVecnaClone: Player {player.playerUsername} stayed too long. Waking up");
                        TriggerWakeUpServerRpc();
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                PlayerControllerB player = other.GetComponent<PlayerControllerB>();
                if (player != null && playerStayTimes.ContainsKey(player))
                {
                    playerStayTimes.Remove(player);
                }
            }
        }

        public void TriggerWakeUpServerRpc()
        {
            if (hasTriggered) return;
            hasTriggered = true; // Prevent spam while waiting for ClientRpc
            bool sent = false;
            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null)
                {
                    vecna.TriggerCloneWakeUpServerRpc();
                    sent = true;
                }
            }
            if (!sent)
            {
                VecnaAI vecnaFallback = UnityEngine.Object.FindObjectOfType<VecnaAI>();
                if (vecnaFallback != null)
                {
                    vecnaFallback.TriggerCloneWakeUpServerRpc();
                }
            }
        }

        public void DetachCloneLocally()
        {
            //Debug.Log("DormantVecnaClone: Detaching clone locally");
            hasTriggered = true;
            if (cloneAnimator != null)
            {
                cloneAnimator.SetTrigger("detach");
            }
        }

        public void StartWakeUpRoutine()
        {
            StartCoroutine(WakeUpRoutine());
        }

        private IEnumerator WakeUpRoutine()
        {
            //Debug.Log("DormantVecnaClone: Starting wait routine before waking true Vecna");
            yield return new WaitForSeconds(5.5f);

            //Debug.Log("DormantVecnaClone: Wait finished. Finding true Vecna...");
            foreach (VecnaAI vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null)
                {
                    Vector3 spawnPos = vecnaSpawnPoint != null ? vecnaSpawnPoint.position : transform.position;
                    //Debug.Log($"DormantVecnaClone: True Vecna found! Waking him up at {spawnPos}");
                    vecna.WakeUpInLair(spawnPos);
                }
            }
            
            // Delete the clone locally
            Destroy(gameObject);
        }
    }
}

