using UnityEngine;

namespace Vecna
{
    public interface IVecnaState
    {
        void Enter();
        void Update();
        void Exit();
    }

    public class VecnaCooldownState : IVecnaState
    {
        private VecnaAI brain;
        public VecnaCooldownState(VecnaAI brain) { this.brain = brain; }
        public void Enter() { }
        public void Exit() { }
        public void Update()
        {
            if (!brain.IsServer) return;

            if (brain.hauntCooldownTimer > 0f)
            {
                brain.hauntCooldownTimer -= Time.deltaTime;
            }
            
            if (brain.hauntCooldownTimer <= 0f)
            {
                if (brain.cursingPlayer == null || brain.cursingPlayer.isPlayerDead || !brain.cursingPlayer.isPlayerControlled)
                {
                    Debug.Log("VECNA: Target missing/dead. Choosing new victim!");
                    brain.ChoosePlayerToCurse();
                }
                brain.currentPhase.Value = VecnaAI.VecnaPhase.ClockStalking;
            }
        }
    }

    public class VecnaClockStalkingState : IVecnaState
    {
        private VecnaAI brain;
        public VecnaClockStalkingState(VecnaAI brain) { this.brain = brain; }
        public void Enter() { }
        public void Exit() { }
        public void Update()
        {
            if (!brain.IsServer || brain.cursingPlayer == null) return;
            brain.environmentTools.FlickerNearbyLights(Time.deltaTime, brain.cursingPlayer);
            if (brain.currentClock == null)
            {
                bool isFinalClockNext = (brain.clocksSpawned >= brain.stats.clocksBeforeChase - 1);

                if (isFinalClockNext && brain.IsMusicPlayingNearVictim())
                {
                    if (brain.spawnTimer >= (brain.stats.spawnInterval - 0.5f) && Time.frameCount % 60 == 0)
                    {
                        Debug.Log("VECNA: Final clock spawn is currently being suppressed by Boombox music!");
                    }
                    brain.spawnTimer = Mathf.Min(brain.spawnTimer, brain.stats.spawnInterval - 0.5f);
                }
                if (brain.spawnTimer > brain.stats.spawnInterval)
                {
                    bool success = brain.TrySpawningClock();
                    brain.spawnTimer = success ? 0f : brain.stats.spawnInterval - 1f;
                }
                else brain.spawnTimer += Time.deltaTime;
            }
            else
            {
                brain.unspottedTimer += Time.deltaTime;

                if (brain.unspottedTimer >= brain.stats.maxUnspottedTime)
                {
                    Debug.Log("Vecna: Clock expired without being seen. Vanishing.");
                    brain.MissedClockWarningClientRpc((int)brain.cursingPlayer.playerClientId);
                    brain.DisappearClock();
                    return;
                }

                if (brain.JobifiedClockLineOfSightCheck())
                {
                    brain.currentPhase.Value = VecnaAI.VecnaPhase.ClockSpotted;
                    brain.unspottedTimer = 0f;
                    brain.SpotClockClientRpc((int)brain.cursingPlayer.playerClientId, brain.clocksSpawned);
                    Debug.Log("VECNA: Victim has spotted the clock.");
                }
            }
        }
    }

    public class VecnaClockSpottedState : IVecnaState
    {
        private VecnaAI brain;
        public VecnaClockSpottedState(VecnaAI brain) { this.brain = brain; }
        public void Enter() { }
        public void Exit() { }
        public void Update()
        {
            if (!brain.IsServer || brain.cursingPlayer == null || brain.currentClock == null) return;
            
            brain.environmentTools.FlickerNearbyLights(Time.deltaTime, brain.cursingPlayer);
            brain.stareTimer += Time.deltaTime;

            float currentMaxStareTime = (brain.clocksSpawned == 2) ? 4f : brain.stats.maxStareTime;
            if (brain.stareTimer > currentMaxStareTime)
            {
                Debug.Log($"Vecna: {currentMaxStareTime} seconds passed since spotting. Vanishing.");
                brain.DisappearClock();
            }
        }
    }

    public class VecnaVehicleCinematicState : IVecnaState
    {
        private VecnaAI brain;
        private bool hasTriggeredLiftAnim = false;
        public VecnaVehicleCinematicState(VecnaAI brain) { this.brain = brain; }
        public void Enter() { hasTriggeredLiftAnim = false; }
        public void Exit() { }
        public void Update()
        {
            if (brain.cinematicVehicle == null)
            {
                if (brain.IsServer) brain.EndCinematicAndWaitForExit();
                return;
            }

            if (brain.IsServer)
            {
                bool playerInCar = (brain.cinematicVehicle.currentDriver == brain.cursingPlayer ||
                                    brain.cinematicVehicle.currentPassenger == brain.cursingPlayer ||
                                    brain.cursingPlayer.GetComponentInParent<VehicleController>() == brain.cinematicVehicle);

                if (brain.cursingPlayer == null || !playerInCar || brain.cursingPlayer.isPlayerDead)
                {
                    brain.EndCinematicAndWaitForExit();
                    return;
                }
            }

            if (brain.cinematicTimer <= -10f) return;

            if (!brain.vehicleReachedApex)
            {
                if (brain.cinematicTimer < 0f)
                {
                    brain.cinematicTimer += Time.deltaTime;

                    if (brain.cinematicTimer >= -0.5f && !hasTriggeredLiftAnim)
                    {
                        hasTriggeredLiftAnim = true;

                        if (brain.creatureAnimator != null)
                        {
                            brain.creatureAnimator.SetTrigger("blastDoor");
                        }

                        if (brain.liftTelekinesisClip != null && brain.cinematicVehicle.vehicleEngineAudio != null)
                        {
                            brain.cinematicVehicle.vehicleEngineAudio.PlayOneShot(brain.liftTelekinesisClip, 1f);
                        }
                    }

                    return;
                }

                brain.cinematicTimer += Time.deltaTime * 0.8f;

                if (brain.cinematicTimer >= 0.5f && !brain.isCinematicLiftStarted)
                {
                    brain.isCinematicLiftStarted = true;
                    if (brain.cursingLocalPlayer) brain.ToggleGhostVisuals(false);
                }

                if (brain.cinematicVehicle.mainRigidbody != null)
                {
                    Vector3 nextPos = Vector3.Lerp(brain.vehicleStartPos, brain.vehicleTargetPos, brain.cinematicTimer);
                    brain.cinematicVehicle.mainRigidbody.MovePosition(nextPos);

                    Quaternion deltaRotation = Quaternion.Euler(Vector3.up * (Time.deltaTime * 15f));
                    brain.cinematicVehicle.mainRigidbody.MoveRotation(brain.cinematicVehicle.mainRigidbody.rotation * deltaRotation);
                }

                if (brain.cinematicTimer >= 1f)
                {
                    brain.vehicleReachedApex = true;
                    brain.cinematicTimer = 0f;
                }
            }
            else
            {
                brain.cinematicTimer += Time.deltaTime;

                if (brain.cinematicTimer > 2.5f && brain.IsServer)
                {
                    brain.EndCinematicAndWaitForExit();
                }
            }
        }
    }

    public class VecnaWaitingForExitState : IVecnaState
    {
        private VecnaAI brain;
        public VecnaWaitingForExitState(VecnaAI brain) { this.brain = brain; }
        public void Enter() { }
        public void Exit() { }
        public void Update()
        {
            if (!brain.IsServer) return;

            if (brain.cursingPlayer == null || brain.cursingPlayer.isPlayerDead)
            {
                brain.ResetHaunt(repelledByMusic: false, playerKilled: false);
                return;
            }

            VehicleController car = brain.GetPlayerVehicle(brain.cursingPlayer);
            if (car == null)
            {
                Debug.Log("VECNA: Victim left the vehicle wreckage. Initiating Phase 2.");
                brain.StartChase();
            }
        }
    }

    public class VecnaChaseState : IVecnaState
    {
        private VecnaAI brain;
        private Vector3 serverPortalPos;
        private bool portalIsOpenOnServer = false;

        public VecnaChaseState(VecnaAI brain) { this.brain = brain; }
        public void Enter()
        {
            brain.boomboxRescueTimer = 0f;
            brain.isPortalOpen = false;
            this.portalIsOpenOnServer = false;
        }
        public void Exit() { }
        public void Update()
        {
            if (brain.cursingPlayer == null) return;
            if (brain.currentLocalPhase != VecnaAI.VecnaPhase.Chasing) return;

            brain.environmentTools.FlickerNearbyLights(Time.deltaTime, brain.cursingPlayer);

            if (brain.IsOwner && brain.agent.isActiveAndEnabled && brain.agent.isOnNavMesh)
            {
                brain.agent.speed = brain.stats.chaseSpeed;
                VehicleController car = brain.GetPlayerVehicle(brain.cursingPlayer);
                Vector3 targetPos = car != null ? car.transform.position : brain.cursingPlayer.transform.position;
                brain.SetDestinationToPosition(brain.cursingPlayer.transform.position, checkForPath: true);
            }

            if (!brain.IsServer) return;

            brain.chaseTimer -= Time.deltaTime;
            if (brain.chaseTimer <= 0f)
            {
                brain.ResetHaunt(repelledByMusic: false, playerKilled: false);
                return;
            }

            if (brain.cachedBoomboxes != null && brain.activeClone != null)
            {
                float safeRadiusSquared = brain.stats.boomboxRescueRadius * brain.stats.boomboxRescueRadius;
                BoomboxItem rescuingBoombox = null;

                foreach (BoomboxItem boombox in brain.cachedBoomboxes)
                {
                    if (boombox != null && boombox.isPlayingMusic)
                    {
                        float distToCloneSq = (boombox.transform.position - brain.activeClone.transform.position).sqrMagnitude;
                        if (distToCloneSq <= safeRadiusSquared)
                        {
                            rescuingBoombox = boombox;
                            break;
                        }
                    }
                }

                if (rescuingBoombox != null)
                {
                    if (!this.portalIsOpenOnServer)
                    {
                        this.portalIsOpenOnServer = true;
                        this.serverPortalPos = CalculatePortalPosition();
                        brain.TogglePortalClientRpc(true, rescuingBoombox.NetworkObject, this.serverPortalPos);
                    }

                    float distToPortal = Vector3.Distance(brain.cursingPlayer.transform.position, this.serverPortalPos);
                    if (distToPortal < 3.0f)
                    {
                        Debug.Log("VECNA: Victim reached the portal! Escape successful.");
                        brain.ResetHaunt(repelledByMusic: true);
                        return;
                    }
                }
                else if (this.portalIsOpenOnServer)
                {
                    this.portalIsOpenOnServer = false;
                    brain.TogglePortalClientRpc(false, default(Unity.Netcode.NetworkObjectReference), Vector3.zero);
                }
            }

            brain.environmentTools.BlastDoorsOpen();

            VehicleController playerCar = brain.GetPlayerVehicle(brain.cursingPlayer);
            float killDistSq = playerCar != null ? 100f : brain.stats.killRangeSquared;
            Vector3 checkPos = playerCar != null ? playerCar.transform.position : brain.cursingPlayer.transform.position;

            float distToPlayerSq = (brain.transform.position - checkPos).sqrMagnitude;
            if (brain.canKill && distToPlayerSq <= killDistSq)
            {
                brain.TriggerCinematicKill(brain.cursingPlayer);
            }
        }

        private Vector3 CalculatePortalPosition()
        {
            GameObject[] nodesToCheck = brain.cursingPlayer.isInsideFactory ? brain.insideNodes : brain.outsideNodes;

            if (nodesToCheck != null)
            {
                foreach (GameObject node in nodesToCheck)
                {
                    if (node == null) continue;
                    float dist = Vector3.Distance(brain.cursingPlayer.transform.position, node.transform.position);
                    if (dist > 10f && dist < 35f)
                    {
                        return node.transform.position + (Vector3.up * 1.5f);
                    }
                }
            }
            return brain.cursingPlayer.transform.position + (brain.cursingPlayer.transform.forward * 15f) + (Vector3.up * 1.5f);
        }
    }

    public class VecnaExecutingKillState : IVecnaState
    {
        private VecnaAI brain;
        public VecnaExecutingKillState(VecnaAI brain) { this.brain = brain; }
        public void Enter() { }
        public void Exit() { }
        public void Update() 
        { 
            if (!brain.IsServer) return;
        }
    }
}