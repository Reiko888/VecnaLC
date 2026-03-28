using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using Unity.Collections;
using Unity.Jobs;

namespace Vecna
{
    public class VecnaAI : EnemyAI
    {
        private static readonly int AnimStartWalk = Animator.StringToHash("startWalk");
        private static readonly int AnimBlastDoor = Animator.StringToHash("blastDoor");
        private static readonly int AnimBlastDoorDone = Animator.StringToHash("blastDoorDone");
        private static readonly int AnimSwingAttack = Animator.StringToHash("swingAttack");
        private static readonly int AnimHasDied = Animator.StringToHash("hasDied");

        private readonly WaitForSeconds wait5Seconds = new WaitForSeconds(5f);
        private readonly WaitForSeconds wait1Point5Seconds = new WaitForSeconds(1.5f);
        private readonly WaitForSeconds wait2Seconds = new WaitForSeconds(2.0f);
        private readonly WaitForSeconds wait0Point21Seconds = new WaitForSeconds(0.21f);
        private readonly WaitForSeconds wait0Point15Seconds = new WaitForSeconds(0.15f);
        private readonly WaitForEndOfFrame waitEndOfFrame = new WaitForEndOfFrame();

        public static List<VecnaAI> ActiveInstances = new List<VecnaAI>();
        public VecnaEnvironmentManipulator environmentTools;
        public VecnaAudioManager audioTools;
        public VecnaStats stats;
        public AudioSource vecnaSnapAudioSource;
        public AudioClip playerSnapClip;
        public AudioSource breathingAudioSource;
        public AudioClip[] breathingClips;
        public AudioClip[] executionVoiceLines;
        public AudioClip[] clockSpotTaunts;
        public AudioClip[] escapeVoiceLines;
        public AudioClip finalChimeClip;
        public AudioClip doorTelekinesisClip;
        public AudioClip liftTelekinesisClip;
        public AudioClip vecnafpexecution;

        public float SFXVolumeLerpTo = 1f;
        public AudioSource chimechase;
        public float maxChaseMusicVolume = 1.0f;
        public float musicFadeRadius = 25f;
        public float fadeSpeed = 2f;
        private bool hasPlayedShipTaunt = false;
        private Coroutine shipTauntCoroutine;

        public float spawnTimer = 0f;
        public float unspottedTimer = 0f;


        public BoomboxItem[] cachedBoomboxes;
        private Light[] cachedLights;
        private DoorLock[] cachedDoors;
        private float slowScanTimer = 0f;
        private const float SLOW_SCAN_INTERVAL = 2.0f;

        public GameObject activeClone = null;
        public Animator activeCloneAnim = null;

        public float boomboxRescueTimer = 0f;
        public bool isPortalOpen = false;
        public bool spectatorInUpsideDown = false;

        public GameObject currentClock;

        public float stareTimer = 0f;

        public PlayerControllerB cursingPlayer;
        public bool cursingLocalPlayer;
        public HashSet<Renderer> hiddenCosmetics = new HashSet<Renderer>();
        public Dictionary<Light, float> hiddenLights = new Dictionary<Light, float>();
        public Dictionary<Renderer, int> hiddenTeammateLayers = new Dictionary<Renderer, int>();
        private bool isVecnaVisible = true;
        public int storedCameraMask = -1;
        public Camera storedCamera = null;
        public const int PORTAL_ONLY_LAYER = 31;
        public const int UPSIDE_DOWN_LAYER = 30;

        private int timesChoosingAPlayer;
        private System.Random vecnaCurseRandom;
        private bool initializedRandomSeed;
        public float hauntCooldownTimer = 0f;

        public VecnaPortalManager portalManager;

        public GameObject[] outsideNodes;
        public GameObject[] insideNodes;

        public float chaseTimer = 0f;

        public int clocksSpawned = 0;

        
        public VecnaCinematicDirector cinematicDirector;

        [ServerRpc(RequireOwnership = false)]
        public void RequestChaseStartServerRpc(int victimId, Vector3 spawnPos)
        {
            SyncChaseStartClientRpc(victimId, spawnPos);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestHauntEndServerRpc(bool repelledByMusic, bool playerKilled)
        {
            SyncHauntEndClientRpc(repelledByMusic, playerKilled);
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestCinematicKillServerRpc(int victimId, Vector3 stopPos, Vector3 lookDir)
        {
            SyncCinematicKillClientRpc(victimId,stopPos, lookDir);
        }

        public enum VecnaPhase
        {
            Cooldown,
            ClockStalking,
            ClockSpotted,
            Chasing,
            ExecutingKill
        }

        [HideInInspector]
        public NetworkVariable<VecnaPhase> currentPhase = new NetworkVariable<VecnaPhase>(VecnaPhase.Cooldown);
        public VecnaPhase currentLocalPhase = VecnaPhase.Cooldown;
        private IVecnaState currentState;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            this.currentPhase.OnValueChanged += OnPhaseChanged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            this.currentPhase.OnValueChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(VecnaPhase oldPhase, VecnaPhase newPhase)
        {
            if (currentState != null) currentState.Exit();
            switch (newPhase)
            {
                case VecnaPhase.Cooldown: currentState = new VecnaCooldownState(this); break;
                case VecnaPhase.ClockStalking: currentState = new VecnaClockStalkingState(this); break;
                case VecnaPhase.ClockSpotted: currentState = new VecnaClockSpottedState(this); break;
                case VecnaPhase.Chasing: currentState = new VecnaChaseState(this); break;
                case VecnaPhase.ExecutingKill: currentState = new VecnaExecutingKillState(this); break;
            }
            if (currentState != null) currentState.Enter();

        }

        private void Awake()
        {
            this.environmentTools = new VecnaEnvironmentManipulator(this);
            this.audioTools = new VecnaAudioManager(this);
            this.cinematicDirector = new VecnaCinematicDirector(this);
            this.portalManager = new VecnaPortalManager(this);
        }

        public override void Start()
        {
            base.Start();
            
            if (currentState == null)
            {
                currentState = new VecnaCooldownState(this);
                currentState.Enter();
            }

            if (Plugin.ClockPrefab != null)
            {
                foreach (AudioSource audio in Plugin.ClockPrefab.GetComponentsInChildren<AudioSource>(true))
                {
                    if (audio != null && audio.clip != null && audio.clip.loadState != AudioDataLoadState.Loaded)
                    {
                        audio.clip.LoadAudioData();
                    }
                }
            }

            // FAILSAFE
            if (this.stats == null)
            {
                Debug.LogWarning("VECNA: Stats ScriptableObject not assigned! Creating a fallback instance.");
                this.stats = ScriptableObject.CreateInstance<VecnaStats>();
            }

            Plugin.BoundConfig.ApplyTo(this.stats);

            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(PORTAL_ONLY_LAYER, i, true);
                Physics.IgnoreLayerCollision(UPSIDE_DOWN_LAYER, i, true);
            }

            if (ActiveInstances.Count > 0 && ActiveInstances[0] != this)
            {
                if (ActiveInstances[0] == null || ActiveInstances[0].isEnemyDead || !ActiveInstances[0].gameObject.activeInHierarchy)
                {
                    ActiveInstances[0] = this; 
                }
                else
                {
                    Debug.LogWarning("VECNA: A duplicate Vecna tried to spawn!.");
                    this.ToggleGhostVisuals(false);
                    if (IsServer) this.GetComponent<NetworkObject>().Despawn(true);
                    return;
                }
            }
            else if (!ActiveInstances.Contains(this))
            {
                ActiveInstances.Add(this);
            }

            if (!RoundManager.Instance.hasInitializedLevelRandomSeed)
            {
                RoundManager.Instance.InitializeRandomNumberGenerators();
            }

            this.outsideNodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
            this.insideNodes = GameObject.FindGameObjectsWithTag("AINode");

            if (IsServer)
            {
                this.ChoosePlayerToCurse();
            }

            this.ToggleGhostVisuals(false);
            Debug.Log("!!!VECNA CURSE TAKEN HOLD!!!");

            ScanNodeProperties scanNode = GetComponentInChildren<ScanNodeProperties>(true);
            Terminal terminal = FindObjectOfType<Terminal>();

            if (scanNode != null && terminal != null)
            {
                foreach (TerminalNode node in terminal.enemyFiles)
                {
                    if (node != null && node.creatureName == "Vecna")
                    {
                        scanNode.creatureScanID = node.creatureFileID;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            VecnaEventManager.OnShipLeft += OnShipLeft;
            VecnaEventManager.OnPlayerDied += OnVictimDied;
            VecnaEventManager.OnPlayerDisconnect += OnVictimDisconnected;
        }

        private void OnDisable()
        {
            VecnaEventManager.OnShipLeft -= OnShipLeft;
            VecnaEventManager.OnPlayerDied -= OnVictimDied;
            VecnaEventManager.OnPlayerDisconnect -= OnVictimDisconnected;
        }

        private void OnShipLeft()
        {
            if (this.currentPhase.Value == VecnaPhase.Chasing || this.currentPhase.Value == VecnaPhase.ClockStalking)
                ResetHaunt(repelledByMusic: true);
        }

        private void OnVictimDied(PlayerControllerB deadPlayer)
        {
            if (!IsServer || this.cursingPlayer != deadPlayer) return;

            if (this.currentPhase.Value != VecnaPhase.ExecutingKill && this.currentPhase.Value != VecnaPhase.Cooldown)
            {
                ResetHaunt(repelledByMusic: false);
            }
            else if (this.currentPhase.Value == VecnaPhase.Cooldown && this.hauntCooldownTimer <= 0f)
            {
                Debug.Log("VECNA: Target missing/dead. Choosing new victim!");
                this.ChoosePlayerToCurse();
                this.currentPhase.Value = VecnaPhase.ClockStalking;
            }
        }

        private void OnVictimDisconnected(PlayerControllerB disconnectedPlayer)
        {
            OnVictimDied(disconnectedPlayer); 
        }

        private void PerformSlowEnvironmentScan()
        {
            this.slowScanTimer += Time.deltaTime;
            if (this.slowScanTimer >= SLOW_SCAN_INTERVAL)
            {
                this.slowScanTimer = 0f;

                this.cachedBoomboxes = FindObjectsOfType<BoomboxItem>();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (this.currentClock != null) Destroy(this.currentClock);
            if (this.activeClone != null)
            {
                Destroy(this.activeClone);
                this.activeClone = null;
            }
            if (this.cinematicDirector.activeFakeBody != null)
            {
                Destroy(this.cinematicDirector.activeFakeBody);
                this.cinematicDirector.activeFakeBody = null;
            }

            this.environmentTools?.RestoreLights();
            this.audioTools?.StopChaseMusic();

            UpsideDownPlayers.Clear();

            if (ActiveInstances.Contains(this)) ActiveInstances.Remove(this);

            Debug.Log("VECNA: Destroyed. Round wiped and memory cleared.");
        }

        public void ChoosePlayerToCurse()
        {
            this.timesChoosingAPlayer++;
            this.SFXVolumeLerpTo = 0f;


            if (this.timesChoosingAPlayer <= 1)
            {
                this.spawnTimer = this.stats.spawnInterval - 10f;
                Debug.Log("VECNA: Level started. First clock spawning in 10 seconds...");
            }
            else
            {
                this.spawnTimer = this.stats.spawnInterval - 5f;
                Debug.Log("VECNA: Target eliminated. Acquiring new target in 5 seconds...");
            }

            this.clocksSpawned = 0;
            this.hasPlayedShipTaunt = false;

            if (this.creatureVoice != null) this.creatureVoice.Stop();

            if (!this.initializedRandomSeed)
            {
                this.vecnaCurseRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 158);
                this.initializedRandomSeed = true;
            }

            float maxInsanity = 0f;
            float insanePlayerIndex = 0f;
            int maxTurns = 0;
            int turningPlayerIndex = 0;

            for (int i = 0; i < 4; i++)
            {
                PlayerControllerB p = StartOfRound.Instance.allPlayerScripts[i];
                if (p == null) continue;

                // FAILSAFE
                if (StartOfRound.Instance.gameStats != null && StartOfRound.Instance.gameStats.allPlayerStats != null && i < StartOfRound.Instance.gameStats.allPlayerStats.Length)
                {
                    if (StartOfRound.Instance.gameStats.allPlayerStats[i].turnAmount > maxTurns)
                    {
                        maxTurns = StartOfRound.Instance.gameStats.allPlayerStats[i].turnAmount;
                        turningPlayerIndex = i;
                    }
                }
                if (p.insanityLevel > maxInsanity)
                {
                    maxInsanity = p.insanityLevel;
                    insanePlayerIndex = (float)i;
                }
            }

            int[] playerTickets = new int[4];
            for (int j = 0; j < 4; j++)
            {
                PlayerControllerB p = StartOfRound.Instance.allPlayerScripts[j];
                if (p == null || !p.isPlayerControlled || p.isPlayerDead)
                {
                    playerTickets[j] = 0;
                }
                else
                {
                    playerTickets[j] += 80;

                    if (insanePlayerIndex == (float)j && maxInsanity > 1f) playerTickets[j] += 50;

                    if (turningPlayerIndex == j) playerTickets[j] += 30;

                    if (!p.hasBeenCriticallyInjured) playerTickets[j] += 10;

                    if (p.currentlyHeldObjectServer != null && p.currentlyHeldObjectServer.scrapValue > 150)
                    {
                        playerTickets[j] += 30;
                    }
                }
            }

            int winningIndex = RoundManager.Instance.GetRandomWeightedIndex(playerTickets, this.vecnaCurseRandom);
            this.cursingPlayer = StartOfRound.Instance.allPlayerScripts[winningIndex];

            // FAILSAFE
            if (this.cursingPlayer == null)
            {
                this.cursingPlayer = GameNetworkManager.Instance.localPlayerController;
            }

            if (IsServer && this.cursingPlayer != null)
            {
                SyncVictimClientRpc((int)this.cursingPlayer.playerClientId);
            }

            if (this.cursingPlayer != null)
            {
                base.ChangeOwnershipOfEnemy(this.cursingPlayer.actualClientId);
                this.cursingLocalPlayer = (GameNetworkManager.Instance.localPlayerController == this.cursingPlayer);
            }

        }

        private IEnumerator NosebleedRoutine(PlayerControllerB victim)
        {
            yield return wait5Seconds;

            if (victim != null && !victim.isPlayerDead)
            {
                victim.bloodDropTimer = -1f;
                victim.DropBlood(Vector3.down, leaveBlood: true, leaveFootprint: false);
                yield return wait1Point5Seconds;

                if (victim != null && !victim.isPlayerDead)
                {
                    victim.bloodDropTimer = -1f;
                    victim.DropBlood(Vector3.down, leaveBlood: true, leaveFootprint: false);
                }
                yield return wait2Seconds;

                if (victim != null && !victim.isPlayerDead)
                {
                    victim.bloodDropTimer = -1f;
                    victim.DropBlood(Vector3.down, leaveBlood: true, leaveFootprint: false);
                }
            }
        }

        [ClientRpc]
        public void SyncVictimClientRpc(int victimPlayerId)
        {
            this.cursingPlayer = StartOfRound.Instance.allPlayerScripts[victimPlayerId];

            this.cursingLocalPlayer = (GameNetworkManager.Instance.localPlayerController == this.cursingPlayer);

            StartCoroutine(NosebleedRoutine(this.cursingPlayer));

            Debug.Log($"VECNA: Network synced! The victim is {this.cursingPlayer.playerUsername}. Local: {this.cursingLocalPlayer}");
        }

        public override void Update()
        {
            base.Update();
            if (this.isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

            this.environmentTools.UpdateScanner(Time.deltaTime);
            this.PerformSlowEnvironmentScan();


            UpdateGlobalVisuals();

            currentState?.Update();
        }

        private void UpdateGlobalVisuals()
        {
            bool shouldSeeVecna = (this.currentLocalPhase == VecnaPhase.Chasing || this.currentLocalPhase == VecnaPhase.ExecutingKill) && IsVictimOrSpectatingVictim();

            if (this.skinnedMeshRenderers != null && this.skinnedMeshRenderers.Length > 0)
            {
                if (this.skinnedMeshRenderers[0].enabled != shouldSeeVecna)
                {
                    this.ToggleGhostVisuals(shouldSeeVecna);
                }
            }
            else if (this.isVecnaVisible != shouldSeeVecna)
            {
                this.ToggleGhostVisuals(shouldSeeVecna);
            }
            if (GameNetworkManager.Instance.localPlayerController.isPlayerDead)
            {
                bool spectatingVictim = IsVictimOrSpectatingVictim();

                if (this.currentClock != null)
                {
                    Renderer[] clockRenderers = this.currentClock.GetComponentsInChildren<Renderer>(true);
                    if (clockRenderers.Length > 0 && clockRenderers[0].enabled != spectatingVictim)
                    {
                        foreach (Renderer r in clockRenderers) r.enabled = spectatingVictim;
                        AudioSource[] clockAudios = this.currentClock.GetComponentsInChildren<AudioSource>(true);
                        foreach (AudioSource audio in clockAudios) audio.volume = spectatingVictim ? 1f : 0f;
                    }
                }

                if (this.activeClone != null)
                {
                    Renderer[] cloneRenderers = this.activeClone.GetComponentsInChildren<Renderer>(true);
                    if (cloneRenderers.Length > 0 && cloneRenderers[0].enabled == spectatingVictim)
                    {
                        foreach (Renderer r in cloneRenderers) r.enabled = !spectatingVictim;
                        Canvas[] cloneCanvases = this.activeClone.GetComponentsInChildren<Canvas>(true);
                        foreach (Canvas c in cloneCanvases) c.enabled = !spectatingVictim;
                    }
                }

                if (this.cinematicDirector.activeFakeBody != null)
                {
                    Renderer[] bodyRenderers = this.cinematicDirector.activeFakeBody.GetComponentsInChildren<Renderer>(true);
                    if (bodyRenderers.Length > 0 && bodyRenderers[0].enabled != spectatingVictim)
                    {
                        foreach (Renderer r in bodyRenderers) r.enabled = spectatingVictim;
                        Canvas[] bodyCanvases = this.cinematicDirector.activeFakeBody.GetComponentsInChildren<Canvas>(true);
                        foreach (Canvas c in bodyCanvases) c.enabled = spectatingVictim;
                    }
                }
                if (!this.cursingLocalPlayer)
                {
                    if (shouldSeeVecna && !this.spectatorInUpsideDown)
                    {
                        this.spectatorInUpsideDown = true;

                        if (this.currentLocalPhase == VecnaPhase.Chasing) this.audioTools.StartChaseMusic(0.6f);
                        VecnaVFXHelper.ToggleTeammatesForVictim(this, false);

                        foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
                        {
                            if (enemy != null && enemy != this)
                            {
                                foreach (AudioSource source in enemy.GetComponentsInChildren<AudioSource>(true))
                                {
                                    if (source != null) source.mute = true;
                                }
                            }
                        }
                        foreach (BoomboxItem boombox in FindObjectsOfType<BoomboxItem>())
                        {
                            if (boombox != null && boombox.boomboxAudio != null) boombox.boomboxAudio.mute = true;
                        }
                    }
                    else if (!shouldSeeVecna && this.spectatorInUpsideDown)
                    {
                        this.spectatorInUpsideDown = false;

                        this.audioTools.StopChaseMusic();
                        VecnaVFXHelper.ToggleTeammatesForVictim(this, true);

                        foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
                        {
                            if (enemy != null && enemy != this && !enemy.isEnemyDead)
                            {
                                foreach (AudioSource source in enemy.GetComponentsInChildren<AudioSource>(true))
                                {
                                    if (source != null) source.mute = false;
                                }
                            }
                        }
                        foreach (BoomboxItem boombox in FindObjectsOfType<BoomboxItem>())
                        {
                            if (boombox != null && boombox.boomboxAudio != null) boombox.boomboxAudio.mute = false;
                        }
                    }
                }
            }

            if ((this.currentLocalPhase == VecnaPhase.Chasing || this.currentLocalPhase == VecnaPhase.ExecutingKill) && this.cursingPlayer != null)
            {
                VecnaVFXHelper.TogglePlayerThirdPersonModel(this, this.cursingPlayer, false);
                this.cursingPlayer.timeSinceMakingLoudNoise = 100f;

                bool isVictimOrSpectator = this.cursingLocalPlayer || (GameNetworkManager.Instance.localPlayerController.isPlayerDead && shouldSeeVecna);

                if (isVictimOrSpectator && this.currentPhase.Value == VecnaPhase.Chasing)
                {
                    this.audioTools.HandleBreathing();
                    VecnaVFXHelper.EnforceTeammateHeldItems(this);
                }
            }

            if (this.cursingLocalPlayer && this.cursingPlayer != null)
            {
                if (this.cursingPlayer.isInHangarShipRoom && !this.hasPlayedShipTaunt)
                {
                    this.hasPlayedShipTaunt = true;
                    if (this.shipTauntCoroutine != null) StopCoroutine(this.shipTauntCoroutine);
                    this.shipTauntCoroutine = StartCoroutine(ShipTransmissionRoutine());
                }
            }
        }

        private IEnumerator ShipTransmissionRoutine()
        {
            yield return new WaitForSeconds(10.0f);
            if (this.cursingPlayer != null && this.cursingPlayer.isInHangarShipRoom && this.currentLocalPhase != VecnaPhase.ExecutingKill)
            {
                if (HUDManager.Instance != null && HUDManager.Instance.signalTranslatorText != null)
                {
                    if (HUDManager.Instance.signalTranslatorAnimator != null) HUDManager.Instance.signalTranslatorAnimator.SetBool("transmitting", true);
                    HUDManager.Instance.signalTranslatorText.text = "";
                    yield return new WaitForSeconds(1.2f);
                    string message = "YOU CANT HIDE";
                    float delay = 2.5f / message.Length;
                    for (int i = 0; i < message.Length; i++)
                    {
                        HUDManager.Instance.signalTranslatorText.text += message[i];
                        yield return new WaitForSeconds(delay);
                    }
                    yield return new WaitForSeconds(3.0f);
                    if (HUDManager.Instance.signalTranslatorAnimator != null) HUDManager.Instance.signalTranslatorAnimator.SetBool("transmitting", false);
                }
            }
        }

        private void LateUpdate()
        {
            this.portalManager?.UpdatePortalRotation();
        }

        [ClientRpc]
        public void TogglePortalClientRpc(bool open, NetworkObjectReference boomboxRef, Vector3 position)
        {
            if (!IsVictimOrSpectatingVictim()) return;

            if (open)
            {
                BoomboxItem boombox = null;

                if (boomboxRef.TryGet(out NetworkObject netObj))
                {
                    boombox = netObj.GetComponent<BoomboxItem>();
                }

                if (boombox == null)
                {
                    foreach (BoomboxItem b in FindObjectsOfType<BoomboxItem>())
                    {
                        if (b.isPlayingMusic) { boombox = b; break; }
                    }
                }

                this.portalManager.TogglePortal(true, boombox, position);
            }
            else
            {
                this.portalManager.TogglePortal(false, null, position);
            }
        }

        public bool IsVictimOrSpectatingVictim()
        {
            if (this.cursingPlayer == null) return false;

            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null) return false;

            if (localPlayer == this.cursingPlayer) return true;

            if (localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript == this.cursingPlayer) return true;

            return false;
        }

        public bool JobifiedClockLineOfSightCheck()
        {
            if (this.cursingPlayer == null || this.currentClock == null) return false;

            Camera playerCam = this.cursingPlayer.gameplayCamera;
            if (playerCam == null) return false;

            Vector3 camPos = playerCam.transform.position;
            Vector3 camForward = playerCam.transform.forward;

            Vector3 targetA = this.currentClock.transform.position;
            Vector3 targetB = this.currentClock.transform.position + Vector3.up * 1.5f;

            Vector3 dirA = (targetA - camPos).normalized;
            Vector3 dirB = (targetB - camPos).normalized;

            bool validA = Vector3.Dot(camForward, dirA) > 0.5f;
            bool validB = Vector3.Dot(camForward, dirB) > 0.5f;

            if (!validA && !validB) return false;

            int mask = StartOfRound.Instance.collidersAndRoomMask;
            QueryParameters query = new QueryParameters(mask, false, QueryTriggerInteraction.Ignore, false);

            NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(2, Allocator.TempJob);
            NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(2, Allocator.TempJob);

            commands[0] = new RaycastCommand(camPos, dirA, query, Mathf.Max(0f, Vector3.Distance(camPos, targetA) - 0.1f));
            commands[1] = new RaycastCommand(camPos, dirB, query, Mathf.Max(0f, Vector3.Distance(camPos, targetB) - 0.1f));

            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1);
            handle.Complete();

            bool hitA = results[0].collider != null;
            bool hitB = results[1].collider != null;

            commands.Dispose();
            results.Dispose();

            return (validA && !hitA) || (validB && !hitB);
        }

        public bool TrySpawningClock()
        {
            if (this.cursingPlayer == null || this.insideNodes == null || this.outsideNodes == null) return false;

            GameObject[] nodesToCheck = this.cursingPlayer.isInsideFactory ? this.insideNodes : this.outsideNodes;
            List<GameObject> validBlindSpots = new List<GameObject>();

            foreach (GameObject node in nodesToCheck)
            {
                if (node == null) continue;

                float distance = Vector3.Distance(this.cursingPlayer.transform.position, node.transform.position);
                if (distance > 3f && distance < 15f)
                {
                    if (!this.cursingPlayer.HasLineOfSightToPosition(node.transform.position, 80f, 100))
                    {
                        validBlindSpots.Add(node);
                    }
                }
            }

            if (validBlindSpots.Count > 0)
            {
                int randomSpot = vecnaCurseRandom.Next(validBlindSpots.Count);
                Vector3 rawSpawnPos = validBlindSpots[randomSpot].transform.position;
                Vector3 finalSpawnPos = rawSpawnPos;

                if (NavMesh.SamplePosition(rawSpawnPos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                {
                    finalSpawnPos = navHit.position;
                }

                if (IsServer)
                {
                    SpawnClockClientRpc(finalSpawnPos, this.clocksSpawned);
                    if (this.clocksSpawned == 1)
                    {
                        SpawnFakeBodyClientRpc(finalSpawnPos);
                    }
                }
                this.stareTimer = 0f;
                this.unspottedTimer = 0f;

                return true;
            }

            return false;
        }

        public bool IsMusicPlayingNearVictim()
        {
            if (this.cursingPlayer == null || this.cachedBoomboxes == null) return false;

            float safeRadiusSq = this.stats.boomboxRescueRadius * this.stats.boomboxRescueRadius;

            foreach (BoomboxItem boombox in this.cachedBoomboxes)
            {
                if (boombox != null && boombox.isPlayingMusic)
                {
                    float distSq = (boombox.transform.position - this.cursingPlayer.transform.position).sqrMagnitude;
                    if (distSq <= safeRadiusSq)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [ClientRpc]
        public void SpawnFakeBodyClientRpc(Vector3 clockPos)
        {
            Vector3 dirToPlayer = Vector3.forward;
            if (this.cursingPlayer != null)
            {
                dirToPlayer = (this.cursingPlayer.transform.position - clockPos);
                dirToPlayer.y = 0f;
                if (dirToPlayer != Vector3.zero) dirToPlayer.Normalize();
            }

            Vector3 targetFloorPos = clockPos + (dirToPlayer * 2.5f);

            if (Physics.Raycast(clockPos + Vector3.up * 0.5f, dirToPlayer, out RaycastHit hit, 2.5f, StartOfRound.Instance.collidersAndRoomMask))
            {
                targetFloorPos = hit.point - (dirToPlayer * 0.5f);
            }

            if (UnityEngine.AI.NavMesh.SamplePosition(targetFloorPos, out UnityEngine.AI.NavMeshHit navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            {
                targetFloorPos = navHit.position;
            }

            Vector3 bodyPos = targetFloorPos + (Vector3.up * 1.5f);
            Quaternion bodyRot = Quaternion.LookRotation(dirToPlayer);

            GameObject nativeRagdollPrefab = this.cursingPlayer.playersManager.playerRagdolls[0];
            this.cinematicDirector.activeFakeBody = Instantiate(nativeRagdollPrefab, bodyPos, bodyRot);

            List<PlayerControllerB> alivePlayers = new List<PlayerControllerB>();
            foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
            {
                if (p != null && p.isPlayerControlled && !p.isPlayerDead) alivePlayers.Add(p);
            }

            PlayerControllerB randomVictim = this.cursingPlayer;
            if (alivePlayers.Count > 0) randomVictim = alivePlayers[UnityEngine.Random.Range(0, alivePlayers.Count)];

            DeadBodyInfo bodyInfo = this.cinematicDirector.activeFakeBody.GetComponent<DeadBodyInfo>();
            if (bodyInfo != null)
            {
                bodyInfo.playerObjectId = (int)randomVictim.playerClientId;

                bodyInfo.overrideSpawnPosition = true;

                bodyInfo.setMaterialToPlayerSuit = true;

                if (bodyInfo.grabBodyObject != null)
                {
                    bodyInfo.grabBodyObject.grabbable = false;
                    bodyInfo.grabBodyObject.grabbableToEnemies = false;
                }
            }

            StartCoroutine(FinalizeFakeBodyRoutine(this.cinematicDirector.activeFakeBody, randomVictim, dirToPlayer, this.cursingLocalPlayer));
        }

        private IEnumerator FinalizeFakeBodyRoutine(GameObject fakeBody, PlayerControllerB victim, Vector3 dirToPlayer, bool isLocalPlayer)
        {
            yield return waitEndOfFrame;
            if (fakeBody == null || victim == null) yield break;

            ScanNodeProperties scanNode = fakeBody.GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null)
            {
                scanNode.headerText = "Body of " + victim.playerUsername;
                scanNode.subText = "Cause of death: Unknown";
            }

            try
            {
                VecnaVFXHelper.DressCloneLikePlayer(fakeBody, victim);
            }
            catch (Exception e)
            {
                Debug.LogWarning("VECNA: Safely caught cosmetic error: " + e.Message);
            }

            foreach (Rigidbody rb in fakeBody.GetComponentsInChildren<Rigidbody>())
            {
                rb.AddForce(dirToPlayer * 5f + Vector3.down * 4f, ForceMode.Impulse);
            }

            if (!isLocalPlayer)
            {
                Renderer[] bodyRenderers = fakeBody.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in bodyRenderers) r.enabled = false;

                Canvas[] bodyCanvases = fakeBody.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas c in bodyCanvases) c.enabled = false;
            }
        }

        [ClientRpc]
        public void SpawnClockClientRpc(Vector3 spawnPos, int currentClockCount)
        {
            try
            {
                if (Plugin.ClockPrefab == null) return;

                this.currentClock = Instantiate(Plugin.ClockPrefab, spawnPos, Quaternion.identity);
                if (this.currentClock == null) return;

                AudioSource[] clockAudios = this.currentClock.GetComponentsInChildren<AudioSource>(true);
                foreach (AudioSource audio in clockAudios)
                {
                    if (audio != null) audio.dopplerLevel = 0f;
                    if (SoundManager.Instance != null && SoundManager.Instance.diageticMixer != null)
                    {
                        audio.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("Master")[0];
                    }
                }

                Vector3 bestDirection = Vector3.forward;

                Vector3 dirToPlayer = Vector3.forward;
                if (this.cursingPlayer != null)
                {
                    dirToPlayer = (this.cursingPlayer.transform.position - spawnPos);
                    dirToPlayer.y = 0f;
                    if (dirToPlayer != Vector3.zero) dirToPlayer.Normalize();
                    else dirToPlayer = Vector3.forward;
                }

                if (this.cursingPlayer != null && !this.cursingPlayer.isInsideFactory)
                {
                    bestDirection = dirToPlayer;
                }
                else
                {
                    float bestScore = -1f;
                    int wallMask = StartOfRound.Instance.collidersAndRoomMask;
                    int numRays = 8; // 360 / 45

                    NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(numRays, Allocator.TempJob);
                    NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(numRays, Allocator.TempJob);
                    
                    Vector3 rayOrigin = spawnPos + Vector3.up * 0.5f;
                    QueryParameters query = new QueryParameters(wallMask, false, QueryTriggerInteraction.Ignore, false);

                    for (int i = 0; i < numRays; i++)
                    {
                        Vector3 checkDir = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                        commands[i] = new RaycastCommand(rayOrigin, checkDir, query, 20f);
                    }

                    JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1);
                    handle.Complete();

                    for (int i = 0; i < numRays; i++)
                    {
                        Vector3 checkDir = Quaternion.Euler(0, i * 45, 0) * Vector3.forward;
                        float distance = results[i].collider != null ? results[i].distance : 20f;

                        float playerAlignment = Vector3.Dot(checkDir, dirToPlayer);
                        float score = distance * (playerAlignment + 1.5f);

                        if (distance < 1.5f) score *= 0.1f;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestDirection = checkDir;
                        }
                    }

                    commands.Dispose();
                    results.Dispose();
                }

                if (bestDirection != Vector3.zero)
                {
                    this.currentClock.transform.rotation = Quaternion.LookRotation(bestDirection);
                    this.currentClock.transform.Rotate(0, 90f, 0, Space.Self);
                    GameObject clockLightObj = new GameObject("VecnaClockLight");
                    clockLightObj.transform.position = this.currentClock.transform.position + (Vector3.up * 0.1f) + (bestDirection * 0.8f);
                    clockLightObj.transform.SetParent(this.currentClock.transform, true);

                    Light turquoiseLight = clockLightObj.AddComponent<Light>();
                    turquoiseLight.type = LightType.Spot;
                    turquoiseLight.spotAngle = 110f;
                    turquoiseLight.innerSpotAngle = 40f;
                    turquoiseLight.color = new Color(0.1f, 0.9f, 0.8f, 1f);
                    turquoiseLight.intensity = 20f;
                    turquoiseLight.range = 12f;
                    turquoiseLight.shadows = LightShadows.Soft;

                    Vector3 clockFacePos = this.currentClock.transform.position + (Vector3.up * 2.2f);
                    clockLightObj.transform.LookAt(clockFacePos);
                }


                if (!this.cursingLocalPlayer)
                {
                    Renderer[] clockRenderers = this.currentClock.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in clockRenderers) r.enabled = false;

                    foreach (AudioSource audio in clockAudios) audio.volume = 0f;

                    Light[] clockLights = this.currentClock.GetComponentsInChildren<Light>(true);
                    foreach (Light l in clockLights) l.enabled = false;

                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("VECNA: Safely caught visual setup error in Clock Spawn: " + e.Message);
            }
        }

        [ClientRpc]
        public void SpotClockClientRpc(int victimId, int currentClockCount)
        {
            PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[victimId];

            if (GameNetworkManager.Instance.localPlayerController == victim)
            {
                victim.JumpToFearLevel(0.8f, true);
                CancelInvoke(nameof(PlayDelayedChime));
                ;

                if (currentClockCount == 0 && this.finalChimeClip != null)
                {
                    Invoke(nameof(PlayDelayedChime), 10f);
                }

                if (currentClockCount == 1)
                {
                    this.audioTools.PlayClockSpotTaunt();
                }
            }

        }

        private void PlayDelayedChime()
        {
            this.audioTools.PlayClockChime();
        }

        [ClientRpc]
        public void MissedClockWarningClientRpc(int victimId)
        {
            PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[victimId];

            if (GameNetworkManager.Instance.localPlayerController == victim)
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.DisplayTip("???", "A clock tolled in the distance... your end is near.", isWarning: true);
                }
            }
        }

        public void DisappearClock()
        {
            if (!IsServer) return;

            if (this.currentClock == null) return;

            this.currentPhase.Value = VecnaPhase.ClockStalking;
            this.stareTimer = 0f;
            this.spawnTimer = 0f;
            this.unspottedTimer = 0f;

            this.clocksSpawned++;
            Debug.Log($"Vecna: Clock vanished. Count is now {this.clocksSpawned}/{this.stats.clocksBeforeChase}");

            bool isFinalClock = (this.clocksSpawned >= this.stats.clocksBeforeChase);

            DespawnClockClientRpc(isFinalClock);

            if (isFinalClock)
            {
                this.StartChase();
            }
        }

        [ClientRpc]
        public void DespawnClockClientRpc(bool isFinalClock)
        {
            if (this.currentClock != null)
            {
                StartCoroutine(this.cinematicDirector.CinematicClockDespawnRoutine(this.currentClock, isFinalClock));

                this.currentClock = null;
            }
        }

        public static List<PlayerControllerB> UpsideDownPlayers = new List<PlayerControllerB>();

        public static bool IsPlayerInUpsideDown(PlayerControllerB playerToCheck)
        {
            return playerToCheck != null && UpsideDownPlayers.Contains(playerToCheck);
        }

        private void StartChase()
        {
            if (this.cursingPlayer == null) return;

            this.targetPlayer = this.cursingPlayer;

            GameObject[] nodesToCheck = this.cursingPlayer.isInsideFactory ? this.insideNodes : this.outsideNodes;
            List<GameObject> validSpawnNodes = new List<GameObject>();

            if (nodesToCheck != null)
            {
                foreach (GameObject node in nodesToCheck)
                {
                    float distance = Vector3.Distance(this.cursingPlayer.transform.position, node.transform.position);
                    if (distance > 15f && distance < 40f)
                    {
                        if (!this.cursingPlayer.HasLineOfSightToPosition(node.transform.position, 80f, 100))
                        {
                            validSpawnNodes.Add(node);
                        }
                    }
                }
            }

            Transform spawnNode = null;
            if (validSpawnNodes.Count > 0)
            {
                spawnNode = validSpawnNodes[UnityEngine.Random.Range(0, validSpawnNodes.Count)].transform;
            }
            else
            {
                spawnNode = base.ChooseFarthestNodeFromPosition(this.cursingPlayer.transform.position, avoidLineOfSight: true);
                if (spawnNode == null && this.allAINodes != null && this.allAINodes.Length > 0)
                {
                    spawnNode = this.allAINodes[UnityEngine.Random.Range(0, this.allAINodes.Length)].transform;
                }
            }

            Vector3 finalSpawnPos = this.transform.position;
            if (spawnNode != null)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(spawnNode.position, out hit, 5f, NavMesh.AllAreas))
                {
                    finalSpawnPos = hit.position;
                }
                else
                {
                    finalSpawnPos = spawnNode.position;
                }
            }

            int playerId = (int)this.cursingPlayer.playerClientId;
            if (IsServer)
            {
                SyncChaseStartClientRpc(playerId, finalSpawnPos);
            }
            else
            {
                RequestChaseStartServerRpc(playerId, finalSpawnPos);
            }

            Debug.Log("Vecna: PHASE 2 INITIATED. The chase begins.");
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);

            this.TriggerCinematicKill(this.cursingPlayer);
        }

        [ClientRpc]
        public void SyncChaseStartClientRpc(int victimId, Vector3 spawnPos)
        {
            PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[victimId];

            if (!UpsideDownPlayers.Contains(victim)) UpsideDownPlayers.Add(victim);
            if (IsServer) this.currentPhase.Value = VecnaPhase.Chasing;
            this.currentLocalPhase = VecnaPhase.Chasing;
            this.cursingPlayer = StartOfRound.Instance.allPlayerScripts[victimId];
            this.cursingLocalPlayer = (GameNetworkManager.Instance.localPlayerController == this.cursingPlayer);

            float calculatedChaseTime = this.audioTools.GetChaseMusicLength();
            if (this.cursingLocalPlayer)
            {
                this.audioTools.StartChaseMusic(0.6f);
            }
            this.chaseTimer = calculatedChaseTime;
            this.boomboxRescueTimer = 0f;
            this.isPortalOpen = false;
            this.serverPosition = spawnPos;
            this.transform.position = spawnPos;
            if (this.agent.isActiveAndEnabled)
            {
                this.agent.Warp(spawnPos);
            }
            this.agent.speed = this.stats.chaseSpeed;

            if (this.cursingLocalPlayer)
            {
                this.ToggleGhostVisuals(true);
                this.cursingPlayer.JumpToFearLevel(1f, true);
                VecnaVFXHelper.ToggleTeammatesForVictim(this, false);

                foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
                {
                    if (enemy != null && enemy != this)
                    {

                        foreach (AudioSource source in enemy.GetComponentsInChildren<AudioSource>(true))
                        {
                            if (source != null) source.mute = true;
                        }
                    }
                }

                foreach (BoomboxItem boombox in FindObjectsOfType<BoomboxItem>())
                {
                    if (boombox != null && boombox.boomboxAudio != null) boombox.boomboxAudio.mute = true;
                }
            }
            else this.ToggleGhostVisuals(false);

            if (this.creatureAnimator != null) this.creatureAnimator.SetTrigger(AnimStartWalk);

            if (HUDManager.Instance != null && this.cursingLocalPlayer)
            {
                HUDManager.Instance.DisplayTip("VECNA", "He is here... survive 60 seconds.", isWarning: true);
            }

            if (this.activeClone != null)
            {
                Destroy(this.activeClone);
                this.activeClone = null;
            }

            if (Plugin.ClonePrefab != null)
            {
                Vector3 cloneSpawnPos = this.cursingPlayer.transform.position;
                this.activeClone = Instantiate(Plugin.ClonePrefab, cloneSpawnPos, this.cursingPlayer.transform.rotation);

                this.activeClone.transform.localScale = Vector3.one;

                foreach (var c in this.activeClone.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var cc in this.activeClone.GetComponentsInChildren<CharacterController>(true)) Destroy(cc);

                this.activeCloneAnim = this.activeClone.GetComponentInChildren<Animator>();
                if (this.activeCloneAnim != null)
                {
                    this.activeCloneAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    this.activeCloneAnim.enabled = true;
                    if (Plugin.ModAssets != null)
                    {
                        this.activeCloneAnim.runtimeAnimatorController = Plugin.ModAssets.LoadAsset<RuntimeAnimatorController>("TranceSnap");
                    }
                }

                foreach (var renderer in this.activeClone.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.gameObject.name.Contains("CloneNametag")) continue;

                    if (renderer.transform.localScale.sqrMagnitude > 10f)
                    {
                        renderer.transform.localScale = new Vector3(1f, 1f, 1f);
                    }
                }

                VecnaVFXHelper.DressCloneLikePlayer(this.activeClone, this.cursingPlayer);

                if (this.cursingPlayer.usernameCanvas != null)
                {
                    Transform cloneHead = null;
                    foreach (Transform t in this.activeClone.GetComponentsInChildren<Transform>())
                    {
                        string cleanName = t.name.ToLower().Replace("_", ".");
                        if (cleanName.Contains("spine.004") && !cleanName.Contains("end"))
                        {
                            cloneHead = t;
                            break;
                        }
                    }
                    if (cloneHead == null) cloneHead = this.activeClone.transform;

                    GameObject stolenNametag = Instantiate(this.cursingPlayer.usernameCanvas.gameObject);
                    stolenNametag.name = "CloneNametag";

                    stolenNametag.transform.SetParent(cloneHead);

                    Vector3 targetGlobalScale = this.cursingPlayer.usernameCanvas.transform.lossyScale;
                    Vector3 parentGlobalScale = cloneHead.lossyScale;

                    stolenNametag.transform.localScale = new Vector3(
                        parentGlobalScale.x > 0 ? targetGlobalScale.x / parentGlobalScale.x : 0f,
                        parentGlobalScale.y > 0 ? targetGlobalScale.y / parentGlobalScale.y : 0f,
                        parentGlobalScale.z > 0 ? targetGlobalScale.z / parentGlobalScale.z : 0f
                    );

                    stolenNametag.transform.position = cloneHead.position + new Vector3(0, 0.6f, 0);

                    stolenNametag.transform.rotation = this.activeClone.transform.rotation;

                    Canvas nametagCanvas = stolenNametag.GetComponent<Canvas>();
                    if (nametagCanvas != null) nametagCanvas.enabled = true;

                    CanvasGroup canvasGroup = stolenNametag.GetComponent<CanvasGroup>();
                    if (canvasGroup != null) canvasGroup.alpha = 1f;

                    foreach (var comp in stolenNametag.GetComponentsInChildren<MonoBehaviour>())
                    {
                        if (!(comp is TMPro.TextMeshProUGUI) && !(comp is CanvasGroup) && comp.GetType().Name != "PlayerNameBillboard")
                        {
                            DestroyImmediate(comp);
                        }
                    }

                    TMPro.TextMeshProUGUI textComp = stolenNametag.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (textComp != null)
                    {
                        textComp.text = this.cursingPlayer.playerUsername;
                        textComp.enabled = true;
                    }

                    stolenNametag.SetActive(true);
                }
            }

            if (this.cursingLocalPlayer && this.cursingPlayer != null)
            {
                if (this.activeClone != null)
                {
                    Renderer[] cloneRenderers = this.activeClone.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in cloneRenderers) r.enabled = false;

                    Canvas[] cloneCanvases = this.activeClone.GetComponentsInChildren<Canvas>(true);
                    foreach (Canvas c in cloneCanvases) c.enabled = false;
                }

                EntranceTeleport[] facilityExits = FindObjectsOfType<EntranceTeleport>();
                foreach (EntranceTeleport exit in facilityExits)
                {
                    InteractTrigger trigger = exit.GetComponent<InteractTrigger>();
                    if (trigger != null) trigger.interactable = false;
                }
            }

            if (this.cursingPlayer != null)
            {
                VecnaVFXHelper.TogglePlayerThirdPersonModel(this, this.cursingPlayer, false);
            }
        }

        private void ToggleGhostVisuals(bool isVisible)
        {
            this.isVecnaVisible = isVisible;
            this.EnableEnemyMesh(isVisible, true);

            ScanNodeProperties scanNode = this.gameObject.GetComponentInChildren<ScanNodeProperties>(true);
            if (scanNode != null)
            {
                scanNode.gameObject.SetActive(isVisible);
            }

            foreach (Transform child in this.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("MapDot"))
                {
                    child.gameObject.SetActive(isVisible);
                }
            }
        }

        [ClientRpc]
        public void PlayDoorAnimationClientRpc()
        {
            StartCoroutine(DoorAnimationRoutine());
        }

        private IEnumerator DoorAnimationRoutine()
        {
            if (this.creatureAnimator != null)
            {
                this.creatureAnimator.SetTrigger(AnimBlastDoor);
            }

            yield return wait1Point5Seconds;

            if (this.creatureAnimator != null)
            {
                this.creatureAnimator.SetTrigger(AnimBlastDoorDone);
            }
        }
        public void ResetHaunt(bool repelledByMusic, bool playerKilled = false)
        {
            this.portalManager?.DestroyEscapePortal();
            if (playerKilled) Debug.Log("VECNA: Player killed successfully. Sequence complete.");
            else if (repelledByMusic) Debug.Log("VECNA: Haunt broken by Boombox/Stun!");
            else Debug.Log("VECNA: Chase timer expired naturally. Haunt ending.");

            if (IsServer) SyncHauntEndClientRpc(repelledByMusic, playerKilled);
            else RequestHauntEndServerRpc(repelledByMusic, playerKilled);
        }

        [ClientRpc]
        public void SyncHauntEndClientRpc(bool repelledByMusic, bool playerKilled)
        {
            if (IsServer) this.currentPhase.Value = VecnaPhase.Cooldown;
            this.currentLocalPhase = VecnaPhase.Cooldown;
            this.boomboxRescueTimer = 0f;
            this.isPortalOpen = false;
            this.portalManager?.DestroyEscapePortal();

            if (!this.cursingLocalPlayer && this.spectatorInUpsideDown)
            {
                this.spectatorInUpsideDown = false;
                this.audioTools.StopChaseMusic();
                VecnaVFXHelper.ToggleTeammatesForVictim(this, true);
            }

            if (this.cursingPlayer != null)
            {
                this.cursingPlayer.inAnimationWithEnemy = null;
                this.cursingPlayer.inSpecialInteractAnimation = false;

                if (!this.cursingPlayer.isPlayerDead)
                {
                    this.cursingPlayer.disableLookInput = false;
                    this.cursingPlayer.disableMoveInput = false;
                    this.cursingPlayer.disableInteract = false;

                    Rigidbody victimRb = this.cursingPlayer.GetComponent<Rigidbody>();
                    if (victimRb != null) victimRb.isKinematic = true;
                    this.cursingPlayer.thisController.enabled = true;
                }

                if (UpsideDownPlayers.Contains(this.cursingPlayer)) UpsideDownPlayers.Remove(this.cursingPlayer);
            }

            if (!playerKilled && this.cursingLocalPlayer)
            {
                this.audioTools.PlayEscapeVoiceLine();
            }

            if (this.allAINodes != null && this.allAINodes.Length > 0)
            {
                Transform farNode = this.allAINodes[0].transform;
                if (this.cursingPlayer != null)
                {
                    Transform farthest = base.ChooseFarthestNodeFromPosition(this.cursingPlayer.transform.position);
                    if (farthest != null) farNode = farthest;
                }

                if (this.agent != null && this.agent.isActiveAndEnabled)
                {
                    this.agent.speed = 0f;
                    this.agent.Warp(farNode.position);
                }
            }

            this.ToggleGhostVisuals(false);
            this.audioTools.StopChaseMusic();

            this.clocksSpawned = 0;
            if (this.currentClock != null)
            {
                Destroy(this.currentClock);
                this.currentClock = null;
            }

            if (this.cursingLocalPlayer && this.cursingPlayer != null)
            {
                if (this.activeClone != null)
                {
                    Vector3 exitPosition = this.activeClone.transform.position + (Vector3.up * 1.5f);

                    Destroy(this.activeClone);
                    this.activeClone = null;

                    this.cursingPlayer.fallValue = 0f;
                    this.cursingPlayer.fallValueUncapped = 0f;

                    this.cursingPlayer.thisController.enabled = false;
                    this.cursingPlayer.TeleportPlayer(exitPosition);

                    if (!this.cursingPlayer.isPlayerDead)
                    {
                        this.cursingPlayer.thisController.enabled = true;
                    }

                    if (!playerKilled && HUDManager.Instance != null)
                    {
                        HUDManager.Instance.DisplayTip("SURVIVED", "You have regained control of your body.", isWarning: false);
                    }
                }
                VecnaVFXHelper.ToggleTeammatesForVictim(this, true);
            }

            if (this.activeClone != null)
            {
                Destroy(this.activeClone);
                this.activeClone = null;
            }
            if (this.cinematicDirector.activeFakeBody != null)
            {
                Destroy(this.cinematicDirector.activeFakeBody);
                this.cinematicDirector.activeFakeBody = null;
            }

            this.environmentTools.RestoreLights();

            if (this.cursingPlayer != null)
            {
                VecnaVFXHelper.TogglePlayerThirdPersonModel(this, this.cursingPlayer, true);

                if (this.cursingLocalPlayer)
                {
                    EntranceTeleport[] facilityExits = FindObjectsOfType<EntranceTeleport>();
                    foreach (EntranceTeleport exit in facilityExits)
                    {
                        InteractTrigger trigger = exit.GetComponent<InteractTrigger>();
                        if (trigger != null) trigger.interactable = true;
                    }
                    this.cursingPlayer.voiceMuffledByEnemy = false;

                    foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
                    {
                        if (enemy != this && !enemy.isEnemyDead)
                        {
                            foreach (AudioSource source in enemy.GetComponentsInChildren<AudioSource>(true))
                            {
                                source.mute = false;
                            }
                        }
                    }
                }
            }

            if (IsServer)
            {
                this.hauntCooldownTimer = repelledByMusic ? 60f : 15f;
            }

            if (repelledByMusic || playerKilled)
            {
                this.cursingPlayer = null;
            }

            if (this.cursingLocalPlayer)
            {
                foreach (BoomboxItem boombox in FindObjectsOfType<BoomboxItem>())
                {
                    if (boombox.boomboxAudio != null) boombox.boomboxAudio.mute = false;
                }
            }
        }

        [ClientRpc]
        public void SyncCinematicKillClientRpc(int victimPlayerId, Vector3 stopPos, Vector3 lookDir)
        {
            if (IsServer) this.currentPhase.Value = VecnaPhase.ExecutingKill;
            this.currentLocalPhase = VecnaPhase.ExecutingKill;

            PlayerControllerB dyingPlayer = StartOfRound.Instance.allPlayerScripts[victimPlayerId];
            bool isVictim = (GameNetworkManager.Instance.localPlayerController == dyingPlayer);

            if (isVictim)
            {
                Vector3 dirToVecna = (this.transform.position - dyingPlayer.transform.position);
                dirToVecna.y = 0f;
                if (dirToVecna == Vector3.zero) dirToVecna = this.transform.forward;
                dirToVecna.Normalize();

                stopPos = dyingPlayer.transform.position + (dirToVecna * 2.5f);
                lookDir = -dirToVecna;
            }

            if (this.agent != null && this.agent.isActiveAndEnabled && this.agent.isOnNavMesh)
            {
                this.moveTowardsDestination = false;
                this.agent.speed = 0f;
                this.agent.velocity = Vector3.zero; 
                this.agent.ResetPath();
                this.agent.Warp(stopPos);
            }
            this.transform.position = stopPos;
            this.serverPosition = stopPos;
            this.transform.rotation = Quaternion.LookRotation(lookDir);

            if (isVictim)
            {
                this.audioTools.StopBreathing();
                this.audioTools.StopChaseMusic();

                if (this.vecnafpexecution != null)
                {
                    StartCoroutine(PlayDelayedDeathProofSnap(dyingPlayer, this.vecnafpexecution, 4.0f));
                }

                this.audioTools.PlayExecutionVoiceLine();
                this.audioTools.PlayClockChime();
                this.audioTools.PlayTelekinesisSound();
            }

            if (isVictim || IsServer)
            {
                if (this.creatureAnimator != null) this.creatureAnimator.SetTrigger(AnimSwingAttack);
            }

            if (dyingPlayer != null) dyingPlayer.voiceMuffledByEnemy = false;

            if (dyingPlayer != null && !isVictim)
            {
                VecnaVFXHelper.TogglePlayerThirdPersonModel(this, dyingPlayer, false);

                StartCoroutine(TeammateLevitationWatchRoutine(dyingPlayer));
            }
            if (isVictim)
            {
                StartCoroutine(LocalLevitationKillRoutine(dyingPlayer));
            }

            if (dyingPlayer != null && !isVictim)
            {
                StartCoroutine(DelayedCloneSnapRoutine(dyingPlayer));
            }
        }

        public void TriggerCinematicKill(PlayerControllerB targetPlayer)
        {
            if (this.currentPhase.Value == VecnaPhase.ExecutingKill) return;

            if (this.agent != null && this.agent.isActiveAndEnabled && this.agent.isOnNavMesh)
            {
                this.agent.speed = 0f;
                this.agent.ResetPath();
            }
            this.currentLocalPhase = VecnaPhase.ExecutingKill;
            if (IsServer) this.currentPhase.Value = VecnaPhase.ExecutingKill;

            Vector3 dirToVecna = (this.transform.position - targetPlayer.transform.position);
            dirToVecna.y = 0f;
            if (dirToVecna == Vector3.zero) dirToVecna = this.transform.forward;
            dirToVecna.Normalize();

            Vector3 stopPos = targetPlayer.transform.position + (dirToVecna * 2.5f);
            Vector3 lookDir = -dirToVecna;

            int playerId = (int)targetPlayer.playerClientId;

            StartCoroutine(this.cinematicDirector.ExecuteCinematicKill());

            if (IsServer) SyncCinematicKillClientRpc(playerId, stopPos, lookDir);
            else RequestCinematicKillServerRpc(playerId, stopPos, lookDir);
        }

        private IEnumerator PlayDelayedDeathProofSnap(PlayerControllerB victim, AudioClip snapClip, float delayTime)
        {
            yield return new WaitForSeconds(delayTime);

            if (snapClip != null && victim != null)
            {
                GameObject audioSpawner = new GameObject("VecnaSnapAudio");
                audioSpawner.transform.position = victim.gameplayCamera.transform.position;

                AudioSource snapSource = audioSpawner.AddComponent<AudioSource>();
                snapSource.clip = snapClip;
                snapSource.spatialBlend = 0f;
                snapSource.volume = 1f;

                if (SoundManager.Instance != null && SoundManager.Instance.diageticMixer != null)
                {
                    snapSource.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("Master")[0];
                }

                snapSource.Play();

                Destroy(audioSpawner, snapClip.length + 0.5f);
            }
        }
        private IEnumerator TeammateLevitationWatchRoutine(PlayerControllerB victim)
        {
            float timer = 0f;
            while (timer < 6.35f)
            {
                if (this.currentLocalPhase != VecnaPhase.ExecutingKill) break;
                timer += Time.deltaTime;
                yield return null;
            }

            if (victim != null)
            {
                VecnaVFXHelper.TogglePlayerThirdPersonModel(this, victim, true);
            }
        }

        private IEnumerator DelayedCloneSnapRoutine(PlayerControllerB dyingPlayer)
        {
            float timer = 0f;
            while (timer < 1.0f)
            {
                if (this.currentLocalPhase != VecnaPhase.ExecutingKill) yield break;
                timer += Time.deltaTime;
                yield return null;
            }

            if (this.activeCloneAnim != null)
            {
                this.activeCloneAnim.enabled = true;
                this.activeCloneAnim.SetTrigger(AnimHasDied);
                if (!this.cursingLocalPlayer && this.liftTelekinesisClip != null)
                {
                    AudioSource cloneAudio = this.activeClone.GetComponent<AudioSource>();
                    if (cloneAudio != null)
                    {
                        cloneAudio.PlayOneShot(this.liftTelekinesisClip, 1f);
                    }
                    else
                    {
                        AudioSource.PlayClipAtPoint(this.liftTelekinesisClip, this.activeClone.transform.position, 1f);
                    }
                }
            }

            float timeToRightElbow = 3.2f;
            float timeToLeftElbow = 0.3f;
            float timeToRightKnee = 1.0f;
            float timeToLeftKnee = 0.2f;

            float[] snapDelays = { timeToRightElbow, timeToLeftElbow, timeToRightKnee, timeToLeftKnee };

            Transform rightElbow = null, leftElbow = null, leftKnee = null, rightKnee = null, cloneHead = null;

            if (this.activeClone != null)
            {
                foreach (Transform t in this.activeClone.GetComponentsInChildren<Transform>())
                {
                    string cleanName = t.name.ToLower().Replace(".", "").Replace("_", "");

                    if (cleanName.Contains("armrlower")) rightElbow = t;
                    else if (cleanName.Contains("armllower")) leftElbow = t;
                    else if (cleanName.Contains("shinl") || cleanName.Contains("calfl")) leftKnee = t;
                    else if (cleanName.Contains("shinr") || cleanName.Contains("calfr")) rightKnee = t;
                    else if (cleanName.Contains("spine004") || cleanName.Contains("head")) cloneHead = t;
                }
            }

            Transform[] snapBones = { leftElbow, rightElbow, rightKnee, leftKnee};

            for (int i = 0; i < 4; i++)
            {
                float audioTimer = 0f;
                while (audioTimer < snapDelays[i])
                {
                    if (this.currentLocalPhase != VecnaPhase.ExecutingKill) yield break;
                    audioTimer += Time.deltaTime;
                    yield return null;
                }
                Vector3 snapPos = this.activeClone != null ? this.activeClone.transform.position : dyingPlayer.transform.position;
                AudioSource cloneAudio = this.activeClone != null ? this.activeClone.GetComponent<AudioSource>() : null;
                this.audioTools.PlayBoneSnap(snapPos, cloneAudio);

                Vector3 splashPos = snapBones[i] != null
                    ? snapBones[i].position
                    : (this.activeClone != null ? this.activeClone.transform.position + Vector3.up * 1.5f : dyingPlayer.transform.position);

                VecnaVFXHelper.CreateSmallBloodSplash(splashPos);

                if (i >= 2)
                {
                    Vector3 headPos = cloneHead != null ? cloneHead.position : (this.activeClone != null ? this.activeClone.transform.position + Vector3.up * 1.6f : dyingPlayer.transform.position + Vector3.up * 1.6f);
                    VecnaVFXHelper.CreateSmallBloodSplash(headPos);
                }
            }
        }

        private IEnumerator LocalLevitationKillRoutine(PlayerControllerB victim)
        {
            victim.inAnimationWithEnemy = this;
            victim.disableLookInput = true;
            victim.disableMoveInput = true;
            victim.disableInteract = true;
            victim.thisController.enabled = false;

            float liftDuration = 3.0f;
            float elapsed = 0f;

            Vector3 startPos = victim.transform.position;

            Vector3 targetPos = startPos + new Vector3(0, 1.5f, 0);

            if (Physics.Raycast(startPos, Vector3.up, out RaycastHit hit, 2.6f, StartOfRound.Instance.collidersAndRoomMask))
            {
                targetPos = startPos + new Vector3(0, Mathf.Max(0, hit.distance - 0.5f), 0);
            }
            Vector3 dirToVecna = (this.transform.position - victim.transform.position);
            dirToVecna.y = 0f;
            dirToVecna.Normalize();

            Quaternion startBodyRot = victim.transform.rotation;
            Quaternion targetBodyRot = Quaternion.LookRotation(dirToVecna);

            Quaternion startCamRot = victim.gameplayCamera.transform.rotation;
            Vector3 originalCamRot = victim.gameplayCamera.transform.localEulerAngles;

            while (elapsed < liftDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / liftDuration);

                if (!victim.playerCollider.enabled) victim.playerCollider.enabled = true;

                victim.transform.position = Vector3.Lerp(startPos, targetPos, t);
                victim.transform.rotation = Quaternion.Slerp(startBodyRot, targetBodyRot, t);

                Vector3 vecnaFace = this.transform.position + (Vector3.up * 2.4f);
                Vector3 dirFromCam = vecnaFace - victim.gameplayCamera.transform.position;

                victim.gameplayCamera.transform.rotation = Quaternion.Slerp(startCamRot, Quaternion.LookRotation(dirFromCam), t);

                victim.fallValue = 0f;
                victim.fallValueUncapped = 0f;
                yield return null;
            }

            float holdDuration = 2.7f;
            float holdTimer = 0f;
            while (holdTimer < holdDuration)
            {
                if (!victim.playerCollider.enabled) victim.playerCollider.enabled = true;

                victim.fallValue = 0f;
                victim.fallValueUncapped = 0f;
                victim.transform.position = targetPos;

                holdTimer += Time.deltaTime;
                yield return null;
            }

            Vector3 currentCamRotEulers = victim.gameplayCamera.transform.localEulerAngles;
            victim.gameplayCamera.transform.localEulerAngles = new Vector3(
                currentCamRotEulers.x - 45f,
                currentCamRotEulers.y + 60f,
                70f
            );

            yield return wait0Point21Seconds;

            Vector3 finalDeathPos = victim.transform.position;

            if (this.activeClone != null)
            {
                finalDeathPos = this.activeClone.transform.position + (Vector3.up * 0.1f);
                victim.thisController.enabled = true;

                Destroy(this.activeClone);
                this.activeClone = null;

                victim.TeleportPlayer(finalDeathPos);

                yield return new WaitForSeconds(0.8f);
            }
            else
            {
                victim.thisController.enabled = true;
                yield return new WaitForSeconds(0.4f);
            }

            VecnaVFXHelper.TogglePlayerThirdPersonModel(this, victim, true);

            victim.thisController.enabled = false;
            victim.transform.position = finalDeathPos;

            victim.gameplayCamera.transform.localEulerAngles = originalCamRot;
            victim.KillPlayer(Vector3.zero, spawnBody: true, CauseOfDeath.Strangulation);

            victim.inAnimationWithEnemy = null;
            victim.disableLookInput = false;
            victim.disableMoveInput = false;
            victim.disableInteract = false;

            if (!victim.isPlayerDead)
            {
                victim.thisController.enabled = true;
                victim.fallValue = 0f;
                victim.fallValueUncapped = 0f;
            }

            if (this.cursingLocalPlayer) VecnaVFXHelper.ToggleTeammatesForVictim(this, true);
        }
    }
}
