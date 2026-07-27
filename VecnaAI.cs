using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;

namespace Vecna;
public enum VecnaPhase
{
    Cooldown,
    ClockStalking,
    ClockSpotted,
    HauntChase,
    HuntEveryone,
}

public class VecnaAI : EnemyAI
{
    private static readonly int AnimBlastDoor = Animator.StringToHash("blastDoor");
    private static readonly int AnimBlastDoorDone = Animator.StringToHash("blastDoorDone");
    public static VecnaLairPortal activeEntrancePortal;
    public static VecnaLairPortal activeExitPortal;
    public static bool isPlayerInLair = false;
    public GameObject atticPrefab;
    public GameObject lairEntrancePortalPrefab;
    public GameObject dormantVecnaClonePrefab;

	public bool isHuntingEveryone => currentLocalPhase == VecnaPhase.HuntEveryone;

    public bool isInLair = false;
    public UnityEngine.GameObject portalSpawnNode;
    private EntranceTeleport[] allTeleports;
    [Header("Audio")]
    public AudioSource vecnaSnapAudioSource;
	public AudioClip playerSnapClip;
	public AudioSource breathingAudioSource;
	public AudioSource footstepsAudio;
	public AudioClip[] breathingClips;
	public AudioClip[] executionVoiceLines;
	public AudioClip[] clockSpotTaunts;
	public AudioClip[] clockTickingClips;
	public AudioClip[] escapeVoiceLines;
	public AudioClip finalChimeClip;

	[Space(5f)]
	public AudioClip clockChime1;
	public AudioClip clockChime2;
	public AudioClip clockChime3;
	public AudioClip doorTelekinesisClip;
	public AudioClip liftTelekinesisClip;
	public AudioClip vecnafpexecution;
	public AudioClip vehicleLiftVoiceLine;

    [Header("Vecna Audio Expansion")]
    public AudioClip pullingPlayerSFX;
    public AudioClip[] outOfLOSVoiceLines;
    public AudioClip stunnedVoiceClip;
    public AudioClip[] pullTauntVoiceLines;
    private float outOfLOSTimer = 0f;

    [Header("Variables")]

    public bool canKill = false;
	public float SFXVolumeLerpTo = 1f;
	public AudioSource chimechase;
	public float spawnTimer = 0f;
	public float unspottedTimer = 0f;
	public BoomboxItem[] cachedBoomboxes;
	public GameObject activeClone = null;
	public Animator activeCloneAnim = null;

	private BoomboxItem rescuingBoombox;
	public const int UPSIDE_DOWN_LAYER = 25;
	public int storedCameraMask = -1;
	public UnityEngine.Camera storedCamera = null;
	public GameObject ClonePrefab;
	public float boomboxRescueTimer = 0f;
	public bool isPortalOpen = false;
	public GameObject currentClock;
	public GameObject ClockPrefab;
	public float stareTimer = 0f;
	public float clockTimer = 0f;
	public PlayerControllerB cursingPlayer;
	public bool cursingLocalPlayer;
	//to enable and disable
	public List<SkinnedMeshRenderer> victimBodyRenderers = new List<SkinnedMeshRenderer>();
	public List<SkinnedMeshRenderer> nonVictimBodyRenderers = new List<SkinnedMeshRenderer>();
	public List<MeshRenderer> victimDefaultMeshRenderers = new List<MeshRenderer>();
	public List<MeshRenderer> nonVictimDefaultMeshRenderers = new List<MeshRenderer>();
	public List<Component> victimDecalProjectors = new List<Component>();
	public List<Component> nonVictimDecalProjectors = new List<Component>();

	private System.Random vecnaCurseRandom;
	private bool initializedRandomSeed;
	    public float hauntCooldownTimer = 0f;
    public bool cloneWasTeleportedToShip = false;
    
    [Header("Door Prying")]
    public HangarShipDoor shipDoor;
    public bool isPryingDoor = false;
    private float pryingDoorAnimTime = 0f;
    public float pryOpenDoorAnimLength = 3f;
    public AudioClip shipAlarm;
    public AudioClip breakAndEnter;
    private bool hasTriggeredThrowAnim = false;

	public VecnaPortalManager portalManager;
	public GameObject portalPrefab;
	public GameObject[] outsideNodes;
	public GameObject[] insideNodes;
	public float chaseTimer = 0f;

	[Header("Telekinesis Abilities")]
	public AudioClip telekinesisWindupSFX;
	public AudioClip telePushExecuteSFX;

    [Header("VFX")]
    public ParticleSystem teleDoorParticle;
    public ParticleSystem telePushParticle;     // Plays once when player is pushed/thrown
    public ParticleSystem telePullParticle;     // Loops during pull animation, stops on player arrival
    public UnityEngine.VFX.VisualEffect auraVisualEffect;         // Toggled active/inactive with Vecna's visibility
    public ParticleSystem teleBlastParticle;    // Plays once during a close-range push/throw blast

    [Header("Animator Swapping")]
    public RuntimeAnimatorController vecnaPullLocalAnimator;
    public RuntimeAnimatorController vecnaPullRemoteAnimator;

    private static readonly Dictionary<ulong, RuntimeAnimatorController> _SAVED_ANIMATORS = new Dictionary<ulong, RuntimeAnimatorController>();
    private AnimatorStateInfo _savedState;
    private float _savedNormalizedTime;
    private bool _savedCrouching;
    private bool _savedWalking;
    private bool _savedJumping;
    private bool _savedSprinting;

	private float telekinesisChargeTimer = 0f;
	private float telekinesisCooldown = 0f;
	private bool isCastingTelekinesis = false;
	private bool isPullingPlayer = false;
	private VecnaPhase? queuedPhase = null;
	private Vector3 serverPortalPos;
	public Vector3 serverVictimClonePos = Vector3.zero;
	public bool isTeleportingVictimFromVecna = false;
	private bool hasTauntedForCurrentClock = false;
	private bool lastSpectatingState = false;
	public static HashSet<GrabbableObject> levelSpawnedScrap = new HashSet<GrabbableObject>();
	private List<GrabbableObject> cachedGrabbableObjects = new List<GrabbableObject>();
	private DoorLock[] cachedDoors;
	private float doorSlamTimer = 0f;
	private float slowScanTimer = 0f;
	private float flickerTimer = 0f;
	private Dictionary<Light, float> originalLightIntensities = new Dictionary<Light, float>();
	private Dictionary<DoorLock, float> telekinesisCooldowns = new Dictionary<DoorLock, float>();
	public GameObject activeFakeBody = null;
    public VecnaLairTrigger lairTrigger;
    public bool IsPlayerInLair(PlayerControllerB player)
    {
        if (player == null) return false;
        if (lairTrigger != null)
        {
            lairTrigger.playersInLair.RemoveAll(p => p == null || p.isPlayerDead || !p.isPlayerControlled || !(p.transform.position.x > 1000f && p.transform.position.z > 1000f));
            return lairTrigger.playersInLair.Contains(player);
        }
        return player.transform.position.x > 1000f && player.transform.position.z > 1000f;
    }

    private bool enemyMeshEnabled;
	private int timesClockSpawned;
	private bool hasStartedChase;
	private Vector3 victimStartingPosition;
	public Vector3 localVictimClonePos;
	private bool serverSpawnedClone;
    public float clockLifetimeTimer = 0f;
    private float blastCooldownTimer;
    private float serverTelekinesisCooldownTimer;
    private bool waitingForPhaseChange = false;

    [Header("Config variables")]
    private float boomboxRescueRadius;
    private float clockSpawnInterval;
    private float hauntChaseSpeed;
    private float huntEveryoneChaseSpeed;
    private float hauntChaseTime;
    private float blastCooldown;
    private float telekinCooldown;
    private int telekinesisBaseDmg;
    private int blastBaseDmg;
    private int vecnaHP;
    private float telePushKnockback;
    private float teleThrowHeight;
    private bool isLightFlickerOn;


    [HideInInspector]
	public VecnaPhase currentLocalPhase => (VecnaPhase)base.currentBehaviourStateIndex;

    public override void Awake()
    {
        base.Awake();
        portalManager = new VecnaPortalManager(this);
        cachedDoors = UnityEngine.Object.FindObjectsOfType<DoorLock>();
        ActiveInstances.Add(this);
    }

    public override void Start()
    {
        base.Start();
        levelSpawnedScrap.Clear();
        updatePositionThreshold = 0.5f;
        hauntChaseSpeed = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Vecna chase speed in haunt").Value;
        boomboxRescueRadius = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Boombox detection radius").Value;
        clockSpawnInterval = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Interval between clock spawns").Value;
        huntEveryoneChaseSpeed = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Chase speed whem hunting everyone").Value;
        hauntChaseTime = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Haunt chase duration").Value;
        blastCooldown = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Blast cooldown").Value;
        telekinCooldown = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Telekinesis cooldown").Value;
        telekinesisBaseDmg = VecnaContentHandler.Instance.vecnaAssets.GetConfig<int>("Telekinesis base damage").Value;
        blastBaseDmg = VecnaContentHandler.Instance.vecnaAssets.GetConfig<int>("Telekinesis blast base damage").Value;
        vecnaHP = VecnaContentHandler.Instance.vecnaAssets.GetConfig<int>("Vecna HP").Value;
        telePushKnockback = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Telekinesis knockback strength").Value;
        teleThrowHeight = VecnaContentHandler.Instance.vecnaAssets.GetConfig<float>("Telekinesis throw height").Value;
        isLightFlickerOn = VecnaContentHandler.Instance.vecnaAssets.GetConfig<bool>("Haunt light flickering").Value;

        this.enemyHP = vecnaHP;
        shipDoor = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();

        if (!RoundManager.Instance.hasInitializedLevelRandomSeed)
        {
            RoundManager.Instance.InitializeRandomNumberGenerators();
        }
        outsideNodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
        insideNodes = GameObject.FindGameObjectsWithTag("AINode");
        if (this.IsServer)
        {
            SpawnLairAndPortal();
        }
        ChoosePlayerToCurse();
        EnableEnemyMesh(enable: false, overrideDoNotSet: true, tamperWithMeshes: true);
        enemyMeshEnabled = false;
        StopAllSFX();
        Debug.Log((object)"!!!VECNA CURSE TAKEN HOLD!!!");
        ScanNodeProperties componentInChildren = this.GetComponentInChildren<ScanNodeProperties>(true);

        GameObject detect = GameObject.Find("LairDetect");
        if (detect == null)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name == "LairDetect" && t.gameObject.scene.isLoaded)
                {
                    detect = t.gameObject;
                    break;
                }
            }
        }
        if (detect != null)
        {
            lairTrigger = detect.GetComponent<VecnaLairTrigger>();
            if (lairTrigger == null) lairTrigger = detect.AddComponent<VecnaLairTrigger>();
        }
    }

    public void ChangePhaseSafely(VecnaPhase newPhase)
	{
        waitingForPhaseChange = true;
		if (inSpecialAnimation)
		{
			queuedPhase = newPhase;
		}
		else if (this.IsServer)
		{
			SyncPhaseSafelyClientRpc((int)newPhase, timesClockSpawned);
            if (newPhase == VecnaPhase.HauntChase)
            {
                if (cursingPlayer != null)
                {
                    victimStartingPosition = cursingPlayer.transform.position;
                    serverVictimClonePos = cursingPlayer.transform.position;
                }
                Vector3 spawnPos;
                bool targetIsOutside = cursingPlayer != null && !cursingPlayer.isInsideFactory;
                if (TryFindingChaseSpawnPosition(out spawnPos))
                {
                    SyncHauntChaseStartClientRpc(spawnPos, targetIsOutside);
                }
                else
                {
                    Vector3 fallbackPos = cursingPlayer != null ? cursingPlayer.transform.position : transform.position;
                    SyncHauntChaseStartClientRpc(fallbackPos, targetIsOutside);
                }
            }
		}
		else
		{
			ChangePhaseServerRpc((int)newPhase, timesClockSpawned);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	public void ChangePhaseServerRpc(int newPhaseIndex, int currentTimesClockSpawned)
	{
		this.timesClockSpawned = currentTimesClockSpawned;
        if ((VecnaPhase)newPhaseIndex == VecnaPhase.Cooldown) { hauntCooldownTimer = clockSpawnInterval; }
		ChangePhaseSafely((VecnaPhase)newPhaseIndex);
	}

	[ClientRpc]
	private void SyncPhaseSafelyClientRpc(int newPhaseIndex, int currentTimesClockSpawned)
	{
		VecnaPhase oldPhase = currentLocalPhase;
		VecnaPhase newPhase = (VecnaPhase)newPhaseIndex;

		this.timesClockSpawned = currentTimesClockSpawned;
		base.currentBehaviourStateIndex = newPhaseIndex;

        if (newPhase == VecnaPhase.Cooldown) { hauntCooldownTimer = clockSpawnInterval; }

		if (oldPhase != newPhase)
		{
			OnLocalPhaseChanged(oldPhase, newPhase);
		}
        
        waitingForPhaseChange = false;
	}

	[ClientRpc]
	public void SyncVictimClientRpc(int victimPlayerId)
	{
		if (victimPlayerId < 0)
		{
			cursingPlayer = null;
			cursingLocalPlayer = false;
			return;
		}
		cursingPlayer = StartOfRound.Instance.allPlayerScripts[victimPlayerId];
		cursingLocalPlayer = (GameNetworkManager.Instance.localPlayerController == cursingPlayer);
		StartCoroutine(NosebleedRoutine(cursingPlayer));
		Debug.Log($"VECNA: Network synced! The victim is {cursingPlayer.playerUsername}. Local: {cursingLocalPlayer}");
        
        PopulateCullingListsDynamically();
	}

    private void PopulateCullingListsDynamically()
    {
        victimBodyRenderers.Clear();
        nonVictimBodyRenderers.Clear();
        victimDefaultMeshRenderers.Clear();
        nonVictimDefaultMeshRenderers.Clear();
        victimDecalProjectors.Clear();
        nonVictimDecalProjectors.Clear();

        if (cursingPlayer != null)
        {
            CullModelReplacement(cursingPlayer, true);
            foreach (SkinnedMeshRenderer smr in cursingPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr != null && smr.gameObject.name != "ScavengerModelArmsOnly" && !smr.gameObject.name.Contains("CloneNametag"))
                {
                    bool isItem = false;
                    for (int i = 0; i < cursingPlayer.ItemSlots.Length; i++)
                    {
                        if (cursingPlayer.ItemSlots[i] != null && smr.transform.IsChildOf(cursingPlayer.ItemSlots[i].transform))
                        {
                            isItem = true; break;
                        }
                    }
                    if (!isItem) victimBodyRenderers.Add(smr);
                }
            }
            foreach (MeshRenderer mr in cursingPlayer.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr != null && mr.gameObject.layer == 0 && !mr.gameObject.name.Contains("CloneNametag"))
                {
                    bool isItem = false;
                    for (int i = 0; i < cursingPlayer.ItemSlots.Length; i++)
                    {
                        if (cursingPlayer.ItemSlots[i] != null && mr.transform.IsChildOf(cursingPlayer.ItemSlots[i].transform))
                        {
                            isItem = true; break;
                        }
                    }
                    if (!isItem) victimDefaultMeshRenderers.Add(mr);
                }
            }
            foreach (Component dp in cursingPlayer.GetComponentsInChildren<Component>(true))
            {
                if (dp != null && dp.GetType().FullName == "UnityEngine.Rendering.HighDefinition.DecalProjector")
                {
                    victimDecalProjectors.Add(dp);
                }
            }
        }

        foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
        {
            if (p != null && p != cursingPlayer && p.isPlayerControlled)
            {
                CullModelReplacement(p, false);
                foreach (SkinnedMeshRenderer smr in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr != null && smr.gameObject.name != "ScavengerModelArmsOnly" && !smr.gameObject.name.Contains("CloneNametag"))
                    {
                        bool isItem = false;
                        for (int i = 0; i < p.ItemSlots.Length; i++)
                        {
                            if (p.ItemSlots[i] != null && smr.transform.IsChildOf(p.ItemSlots[i].transform))
                            {
                                isItem = true; break;
                            }
                        }
                        if (!isItem) nonVictimBodyRenderers.Add(smr);
                    }
                }
                foreach (MeshRenderer mr in p.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr != null && mr.gameObject.layer == 0 && !mr.gameObject.name.Contains("CloneNametag"))
                    {
                        bool isItem = false;
                        for (int i = 0; i < p.ItemSlots.Length; i++)
                        {
                            if (p.ItemSlots[i] != null && mr.transform.IsChildOf(p.ItemSlots[i].transform))
                            {
                                isItem = true; break;
                            }
                        }
                        if (!isItem) nonVictimDefaultMeshRenderers.Add(mr);
                    }
                }
                foreach (Component dp in p.GetComponentsInChildren<Component>(true))
                {
                    if (dp != null && dp.GetType().FullName == "UnityEngine.Rendering.HighDefinition.DecalProjector")
                    {
                        nonVictimDecalProjectors.Add(dp);
                    }
                }
            }
        }
    }

    private void CullModelReplacement(PlayerControllerB p, bool isCursingPlayer)
    {
        UnityEngine.Component bodyReplacement = null;
        foreach (var comp in p.GetComponents<UnityEngine.Component>())
        {
            if (comp != null && comp.GetType().Name == "BodyReplacementBase")
            {
                bodyReplacement = comp;
                break;
            }
        }
        if (bodyReplacement == null) return;

        try
        {
            var type = bodyReplacement.GetType();
            var modelFields = new string[] { "replacementModel", "replacementViewModel", "replacementModelShadow" };
            foreach (var fieldName in modelFields)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null) continue;

                var modelObj = field.GetValue(bodyReplacement) as UnityEngine.GameObject;
                if (modelObj == null) continue;

                foreach (UnityEngine.Renderer r in modelObj.GetComponentsInChildren<UnityEngine.Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.gameObject.name.Contains("CloneNametag")) continue;

                    if (r is UnityEngine.SkinnedMeshRenderer smr)
                    {
                        if (isCursingPlayer) victimBodyRenderers.Add(smr);
                        else nonVictimBodyRenderers.Add(smr);
                    }
                    else if (r is UnityEngine.MeshRenderer mr)
                    {
                        if (isCursingPlayer) victimDefaultMeshRenderers.Add(mr);
                        else nonVictimDefaultMeshRenderers.Add(mr);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"VECNA: Error culling model replacement for {p.playerUsername}: {ex}");
        }
    }

	private Vector3 CalculatePortalPosition()
	{
		GameObject[] nodesToCheck = cursingPlayer.isInsideFactory ? insideNodes : outsideNodes;

		if (nodesToCheck != null)
		{
			foreach (GameObject node in nodesToCheck)
			{
				if (node == null) continue;
				float dist = Vector3.Distance(cursingPlayer.transform.position, node.transform.position);
				if (dist > 10f && dist < 35f)
				{
					return node.transform.position + (Vector3.up * 1.5f);
				}
			}
		}
		return cursingPlayer.transform.position + (cursingPlayer.transform.forward * 15f) + (Vector3.up * 1.5f);
	}

    private void SetDecalEnabled(Component decal, bool enabled)
    {
        if (decal == null) return;
        try
        {
            var prop = decal.GetType().GetProperty("enabled");
            if (prop != null)
            {
                prop.SetValue(decal, enabled);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"VECNA: Failed to set DecalProjector enabled to {enabled}: {ex}");
        }
    }

	private void OnLocalPhaseChanged(VecnaPhase oldPhase, VecnaPhase newPhase)
	{
		Debug.Log($"VECNA: Local Phase Changed from {oldPhase} to {newPhase}");
		
		if (newPhase == VecnaPhase.HauntChase)
		{
            if (!IsVictimOrSpectatingVictim())
            {
                SpawnPlayerClone();
            }
			if (cursingPlayer != null)
			{
				VecnaVFXHelper.TogglePlayerThirdPersonModel(this, cursingPlayer, false);
			}

            if (IsVictimOrSpectatingVictim())
            {
                SetMapObjectsVisibility(false);
            }
            else
            {
                // Local player is teammate: hide the victim (and their held gear)
                if (cursingPlayer != null)
                {
                    HauntVisibilityRegistry.Hide(cursingPlayer.gameObject, "VecnaHaunt");
                    if (cursingPlayer.ItemSlots != null)
                    {
                        foreach (var item in cursingPlayer.ItemSlots)
                        {
                            if (item != null)
                            {
                                HauntVisibilityRegistry.Hide(item.gameObject, "VecnaHaunt");
                            }
                        }
                    }
                }
            }
		}
		else
		{
            if (oldPhase == VecnaPhase.HauntChase)
            {
                if (this.cursingLocalPlayer)
                {
                    EntranceTeleport[] facilityExits = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
                    foreach (EntranceTeleport exit in facilityExits)
                    {
                        InteractTrigger trigger = exit.GetComponent<InteractTrigger>();
                        if (trigger != null) trigger.interactable = true;
                    }
                }

                if (newPhase == VecnaPhase.HuntEveryone && this.IsServer && cursingPlayer != null && activeClone != null && !cursingPlayer.isPlayerDead)
                {
                    TeleportSurvivingVictimClientRpc((int)cursingPlayer.playerClientId, activeClone.transform.position, cloneWasTeleportedToShip);
                }

                // Restore all objects
                SetMapObjectsVisibility(true);
            }

            if (newPhase == VecnaPhase.Cooldown) {
                StopAllSFX();
            }
            if (newPhase == VecnaPhase.HuntEveryone)
            {
                if (currentClock != null)
                {
                    UnityEngine.Object.Destroy(currentClock);
                    currentClock = null;
                }
            }

			DestroyActiveClone();
			if (portalManager != null)
			{
				portalManager.DestroyEscapePortal();
			}
			
			if (cursingPlayer != null)
			{
				VecnaVFXHelper.TogglePlayerThirdPersonModel(this, cursingPlayer, true);
			}

			if (newPhase != VecnaPhase.HuntEveryone)
			{
				enemyMeshEnabled = false;
				EnableEnemyMesh(enable: false, overrideDoNotSet: true, tamperWithMeshes: true);
			}
            else
            {
                if (this.enemyType != null)
                {
                    this.enemyType.canDie = true;
                    this.enemyType.canBeStunned = true;
                }
            }
		}
	}

	private void SetMapObjectsVisibility(bool visible)
	{
		foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
		{
			if (p != null)
			{
				if (visible)
				{
					HauntVisibilityRegistry.Restore(p.gameObject, "VecnaHaunt");
					if (p.ItemSlots != null)
					{
						foreach (var item in p.ItemSlots)
						{
							if (item != null) HauntVisibilityRegistry.Restore(item.gameObject, "VecnaHaunt");
						}
					}
				}
				else if (p.isPlayerControlled && p != cursingPlayer)
				{
					HauntVisibilityRegistry.Hide(p.gameObject, "VecnaHaunt");
					if (p.ItemSlots != null)
					{
						foreach (var item in p.ItemSlots)
						{
							if (item != null) HauntVisibilityRegistry.Hide(item.gameObject, "VecnaHaunt");
						}
					}
				}
			}
		}

		HashSet<GameObject> victimInventory = new HashSet<GameObject>();
		if (cursingPlayer != null && cursingPlayer.ItemSlots != null)
		{
			foreach (GrabbableObject item in cursingPlayer.ItemSlots)
			{
				if (item != null)
				{
					victimInventory.Add(item.gameObject);
				}
			}
		}

		foreach (GrabbableObject item in cachedGrabbableObjects)
		{
			if (item != null && !victimInventory.Contains(item.gameObject))
			{
				item.EnableItemMeshes(visible);
			}
		}

		if (RoundManager.Instance != null && RoundManager.Instance.SpawnedEnemies != null)
		{
			foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
			{
				if (enemy != null && enemy != this)
				{
					if (visible) HauntVisibilityRegistry.Restore(enemy.gameObject, "VecnaHaunt");
					else if (!enemy.isEnemyDead) HauntVisibilityRegistry.Hide(enemy.gameObject, "VecnaHaunt");
				}
			}
		}
	}

	private void UpdateCachedGrabbables()
	{
		cachedGrabbableObjects.Clear();

		if (StartOfRound.Instance != null && StartOfRound.Instance.propsContainer != null)
		{
			foreach (Transform child in StartOfRound.Instance.propsContainer)
			{
				if (child != null && child.CompareTag("PhysicsProp"))
				{
					GrabbableObject go = child.GetComponent<GrabbableObject>();
					if (go != null) cachedGrabbableObjects.Add(go);
				}
			}
		}

		if (RoundManager.Instance != null && RoundManager.Instance.mapPropsContainer != null)
		{
			foreach (Transform child in RoundManager.Instance.mapPropsContainer.transform)
			{
				if (child != null && child.CompareTag("PhysicsProp"))
				{
					GrabbableObject go = child.GetComponent<GrabbableObject>();
					if (go != null) cachedGrabbableObjects.Add(go);
				}
			}
		}

		GameObject hangarShip = GameObject.Find("HangarShip");
		if (hangarShip != null)
		{
			GrabbableObject[] shipItems = hangarShip.GetComponentsInChildren<GrabbableObject>(true);
			foreach (GrabbableObject go in shipItems)
			{
				if (go != null) cachedGrabbableObjects.Add(go);
			}
		}
		foreach (GrabbableObject scrap in levelSpawnedScrap)
		{
			if (scrap != null) cachedGrabbableObjects.Add(scrap);
		}
	}

	private void SpawnLairAndPortal()
    {
        VecnaLairPortal exitPortal = null;
        if (atticPrefab != null)
        {
            Vector3 lairPosition = new Vector3(1500f, -200f, 1500f);
            GameObject lairInstance = Instantiate(atticPrefab, lairPosition, Quaternion.identity);
            NetworkObject netObj = lairInstance.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
            else Debug.LogWarning("VECNA: atticPrefab is missing a NetworkObject component!");
            
            exitPortal = lairInstance.GetComponentInChildren<VecnaLairPortal>();
            activeExitPortal = exitPortal;
        }

        VecnaLairPortal entrancePortal = null;
        if (lairEntrancePortalPrefab != null && insideNodes != null && insideNodes.Length > 0)
        {
            GameObject randomNode = insideNodes[UnityEngine.Random.Range(0, insideNodes.Length)];
            if (randomNode != null)
            {
                portalSpawnNode = randomNode;
                Vector3 portalPos = randomNode.transform.position;
                RaycastHit hit;
                if (Physics.Raycast(randomNode.transform.position + Vector3.up * 1.5f, Vector3.down, out hit, 10f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    portalPos = hit.point + Vector3.up * 0.15f;
                }
                GameObject entranceInstance = Instantiate(lairEntrancePortalPrefab, portalPos, Quaternion.identity);
                NetworkObject netObj = entranceInstance.GetComponent<NetworkObject>();
                if (netObj != null) netObj.Spawn();
                else Debug.LogWarning("VECNA: lairEntrancePortalPrefab is missing a NetworkObject component!");
                
                entrancePortal = entranceInstance.GetComponent<VecnaLairPortal>();
                activeEntrancePortal = entrancePortal;
            }
        }

        if (exitPortal != null && entrancePortal != null)
        {
            entrancePortal.teleportDestination = exitPortal.transform;
            exitPortal.teleportDestination = entrancePortal.transform;
        }

        foreach (Collider vc in GetComponentsInChildren<Collider>(true))
        {
            if (vc != null)
            {
                if (exitPortal != null)
                {
                    foreach (Collider pc in exitPortal.GetComponentsInChildren<Collider>(true))
                    {
                        Physics.IgnoreCollision(vc, pc, true);
                    }
                }
                if (entrancePortal != null)
                {
                    foreach (Collider pc in entrancePortal.GetComponentsInChildren<Collider>(true))
                    {
                        Physics.IgnoreCollision(vc, pc, true);
                    }
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
		if (currentLocalPhase == VecnaPhase.HauntChase || currentLocalPhase == VecnaPhase.ClockStalking)
		{
			ResetHaunt(repelledByMusic: true);
		}
	}

	private void OnVictimDied(PlayerControllerB deadPlayer)
	{
		if (this.IsServer && !(cursingPlayer != deadPlayer))
		{
			if (currentLocalPhase != VecnaPhase.Cooldown)
			{
				ResetHaunt(repelledByMusic: false);
			}
			else if (currentLocalPhase == VecnaPhase.Cooldown && hauntCooldownTimer <= 0f)
			{
				Debug.Log((object)"Target missing/dead. Choosing new victim!");
				ChoosePlayerToCurse();
				ChangePhaseSafely(VecnaPhase.ClockStalking);
			}
		}
	}

	private void OnVictimDisconnected(PlayerControllerB disconnectedPlayer)
	{
		OnVictimDied(disconnectedPlayer);
	}

    [ClientRpc]
    public void TeleportSurvivingVictimClientRpc(int playerId, Vector3 clonePos, bool cloneWasTeleportedToShip)
    {
        PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[playerId];
        if (victim == GameNetworkManager.Instance.localPlayerController)
        {
            isTeleportingVictimFromVecna = true;
            victim.thisController.enabled = false;
            victim.TeleportPlayer(clonePos);
            if (cloneWasTeleportedToShip && victim.isInsideFactory)
            {
                victim.isInsideFactory = false;
            }
            victim.thisController.enabled = true;
            isTeleportingVictimFromVecna = false;
        }
    }

	public void ResetHaunt(bool repelledByMusic, bool playerKilled = false)
	{
		if (isHuntingEveryone && !StartOfRound.Instance.shipIsLeaving) return;

        if (cursingPlayer != null && GameNetworkManager.Instance.localPlayerController == cursingPlayer)
        {
            EntranceTeleport[] facilityExits = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
            foreach (EntranceTeleport exit in facilityExits)
            {
                InteractTrigger trigger = exit.GetComponent<InteractTrigger>();
                if (trigger != null) trigger.interactable = true;
            }
        }

        if (!playerKilled && cursingPlayer != null && GameNetworkManager.Instance.localPlayerController == cursingPlayer)
        {
            PlayEscapeVoiceLineToVictim();
        }

        enemyMeshEnabled = false;
        EnableEnemyMesh(enable: false, overrideDoNotSet: true, tamperWithMeshes: true);


		if (this.IsServer)
		{
            if (cursingPlayer != null)
            {
                Vector3 targetPos = (serverVictimClonePos != Vector3.zero) ? serverVictimClonePos : (activeClone != null ? activeClone.transform.position : (localVictimClonePos != Vector3.zero ? localVictimClonePos : victimStartingPosition));
                TeleportSurvivingVictimClientRpc((int)cursingPlayer.playerClientId, targetPos, cloneWasTeleportedToShip);
            }

			DestroyActiveClone();
            isPortalOpen = false;
            serverVictimClonePos = Vector3.zero;
		}

        hasStartedChase = false;
        stareTimer = 0f;
        chaseTimer = 0f;
        timesClockSpawned = 0;
        hasTauntedForCurrentClock = false;
        
        movingTowardsTargetPlayer = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = 0f;
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.ResetPath();
        }
        hauntCooldownTimer = clockSpawnInterval;

        if (portalManager != null)
        {
            portalManager.DestroyEscapePortal();
        }
		
		serverSpawnedClone = false;
        boomboxRescueTimer = 0f;
		
        hauntCooldownTimer = clockSpawnInterval;
        if (playerKilled)
        {
            Debug.Log("VECNA: Player killed successfully. Sequence complete.");
        }

        isInLair = false;
        isOutside = false;
        if (this.IsServer)
        {
            SyncLairStateClientRpc(false);
            SyncOutsideStateClientRpc(false);
        }

		StopAllSFX();
		ChangePhaseSafely(VecnaPhase.Cooldown);
        cursingPlayer = null;
        cursingLocalPlayer = false;
	}

	private void PerformSlowEnvironmentScan()
	{
		slowScanTimer += Time.deltaTime;
		if (slowScanTimer >= 2f)
		{
			slowScanTimer = 0f;
			cachedBoomboxes = UnityEngine.Object.FindObjectsOfType<BoomboxItem>();
			UpdateCachedGrabbables();
		}
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (currentClock != null)
		{
			UnityEngine.Object.Destroy(currentClock);
		}
		DestroyActiveClone();
		if (activeFakeBody != null)
		{
			UnityEngine.Object.Destroy(activeFakeBody);
			activeFakeBody = null;
		}
		StopAllSFX();
		Debug.Log((object)"Destroyed. Round wiped and memory cleared.");
	}

	public void ChoosePlayerToCurse()
	{
        timesClockSpawned = 0;
		clockTimer = 0f;
		if (!initializedRandomSeed)
		{
			vecnaCurseRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 158);
			initializedRandomSeed = true;
		}
		float num = 0f;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int numPlayers = StartOfRound.Instance.allPlayerScripts.Length;
		for (int i = 0; i < numPlayers; i++)
		{
			PlayerControllerB val = StartOfRound.Instance.allPlayerScripts[i];
			if (!(val == null))
			{
				if (StartOfRound.Instance.gameStats != null && StartOfRound.Instance.gameStats.allPlayerStats != null && i < StartOfRound.Instance.gameStats.allPlayerStats.Length && StartOfRound.Instance.gameStats.allPlayerStats[i].turnAmount > num3)
				{
					num3 = StartOfRound.Instance.gameStats.allPlayerStats[i].turnAmount;
					num4 = i;
				}
				if (val.insanityLevel > num)
				{
					num = val.insanityLevel;
					num2 = i;
				}
			}
		}
		int[] array = new int[numPlayers];
		for (int j = 0; j < numPlayers; j++)
		{
			PlayerControllerB val2 = StartOfRound.Instance.allPlayerScripts[j];
			if (val2 == null || !val2.isPlayerControlled || val2.isPlayerDead)
			{
				array[j] = 0;
				continue;
			}
			array[j] += 80;
			if (num2 == j && num > 1f)
			{
				array[j] += 50;
			}
			if (num4 == j)
			{
				array[j] += 30;
			}
			if (!val2.hasBeenCriticallyInjured)
			{
				array[j] += 10;
			}
			if (val2.currentlyHeldObjectServer != null && val2.currentlyHeldObjectServer.scrapValue > 150)
			{
				array[j] += 30;
			}
		}
		int randomWeightedIndex = RoundManager.Instance.GetRandomWeightedIndex(array, vecnaCurseRandom);
		cursingPlayer = StartOfRound.Instance.allPlayerScripts[randomWeightedIndex];
		if (cursingPlayer == null)
		{
			cursingPlayer = GameNetworkManager.Instance.localPlayerController;
		}
		if (cursingPlayer != null)
		{
			this.ChangeOwnershipOfEnemy(cursingPlayer.actualClientId);
			cursingLocalPlayer = GameNetworkManager.Instance.localPlayerController == cursingPlayer;
			if (this.IsServer)
			{
				SyncVictimClientRpc((int)cursingPlayer.playerClientId);
			}
		}
	}

	private IEnumerator NosebleedRoutine(PlayerControllerB victim)
	{
		yield return new UnityEngine.WaitForSeconds(5f);
		if (victim != null && !victim.isPlayerDead)
		{
			victim.bloodDropTimer = -1f;
			victim.DropBlood(Vector3.down, true, false);
			yield return new UnityEngine.WaitForSeconds(1.5f);
			if (victim != null && !victim.isPlayerDead)
			{
				victim.bloodDropTimer = -1f;
				victim.DropBlood(Vector3.down, true, false);
			}
			yield return new UnityEngine.WaitForSeconds(2f);
			if (victim != null && !victim.isPlayerDead)
			{
				victim.bloodDropTimer = -1f;
				victim.DropBlood(Vector3.down, true, false);
			}
		}
	}

	public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
	{
		if (currentLocalPhase != VecnaPhase.HuntEveryone) return;
		base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (isEnemyDead) return;
        this.enemyHP -= force;
        if (this.enemyHP <= 0)
        {
            KillEnemyOnOwnerClient(overrideDestroy: false);
        }
	}

	public override void SetEnemyStunned(bool setToStunned, float setToStunTime = 1f, PlayerControllerB setStunnedByPlayer = null)
	{
		if (currentLocalPhase != VecnaPhase.HuntEveryone) return;
		base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
		if (setToStunned)
		{
            isCastingTelekinesis = false;
            isPullingPlayer = false;
            if (creatureVoice != null && creatureVoice.loop)
            {
                creatureVoice.Stop();
                creatureVoice.loop = false;
            }
            if (creatureVoice != null && stunnedVoiceClip != null)
            {
                creatureVoice.PlayOneShot(stunnedVoiceClip, 1f);
            }
            if (creatureAnimator != null)
            {
                creatureAnimator.SetTrigger("isStunned");
                creatureAnimator.SetBool("isPulling", false);
            }
		}
	}

	public override void KillEnemy(bool destroy = false)
	{
		if (currentLocalPhase != VecnaPhase.HuntEveryone) return;
		base.KillEnemy(destroy);
        if (creatureVoice != null && dieSFX != null)
        {
            creatureVoice.PlayOneShot(dieSFX, 1f);
        }
        
        if (creatureAnimator != null)
        {
            creatureAnimator.SetTrigger("vecnaDie");
        }
        if (auraVisualEffect != null)
        {
            auraVisualEffect.Stop();
        }
        StopAllSFX();
	}

    private void StartPullingPlayer()
    {
        isPullingPlayer = true;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.speed = 0f;
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.ResetPath();
        }
        if (telePullParticle != null)
        {
            telePullParticle.Play();
        }
    }

    private void StopPullingPlayer()
    {
        isPullingPlayer = false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        if (telePullParticle != null)
        {
            telePullParticle.Stop();
        }
    }

	public override void DoAIInterval()
    {
        base.DoAIInterval();
        
        if (isEnemyDead || StartOfRound.Instance.allPlayersDead) return;

        //Debug.Log($"[VECNA AI UPDATE] State: {currentBehaviourStateIndex}, Phase: {currentLocalPhase}, Target: {(targetPlayer != null ? targetPlayer.playerUsername : "none")}, Position: {transform.position}, isInLair: {isInLair}, isOutside: {isOutside}");

        if (isPullingPlayer || inSpecialAnimation || stunNormalizedTimer > 0f)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.speed = 0f;
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        if (isInLair)
        {
            if (lairTrigger != null)
            {
                string playerNames = string.Join(", ", lairTrigger.playersInLair.ConvertAll(p => p.playerUsername));
                //Debug.Log($"[VECNA PORTAL SYSTEM] Lair Status: {lairTrigger.playersInLair.Count} player(s) in Lair. List: [{playerNames}]");
            }
            if (this.IsOwner)
            {
                bool anyoneInLair = false;
                if (lairTrigger != null)
                {
                    anyoneInLair = lairTrigger.playersInLair.Count > 0;
                }

                if (!anyoneInLair && activeEntrancePortal != null)
                {
                    Vector3 position = portalSpawnNode != null ? portalSpawnNode.transform.position : activeEntrancePortal.transform.position;
                    //Debug.Log($"[VECNA PORTAL SYSTEM] Lair Escape Triggered! No players in Lair list. Warping instantly to entrance portal node: {position}");
                    
                    if (agent.isActiveAndEnabled)
                    {
                        agent.ResetPath();
                    }
                    movingTowardsTargetPlayer = false;
                    agent.enabled = false;
                    transform.position = position;
                    serverPosition = position;
                    agent.enabled = true;
                    bool warpSuccess = agent.Warp(position);
                    //Debug.Log($"[VECNA PORTAL SYSTEM] Warp success: {warpSuccess}, New position: {transform.position}");
                    
                    isOutside = false;
                    isInLair = false;
                    TeleportVecnaThroughPortalServerRpc(position, false);
                }
            }

            if (this.IsOwner && isInLair)
            {
                PlayerControllerB lairTarget = null;
                float lairTargetDist = 9999f;
                
                if (lairTrigger != null)
                {
                    foreach (PlayerControllerB p in lairTrigger.playersInLair)
                    {
                        float d = Vector3.Distance(transform.position, p.transform.position);
                        if (d < lairTargetDist)
                        {
                            lairTargetDist = d;
                            lairTarget = p;
                        }
                    }
                }

                if (lairTarget != null)
                {
                    targetPlayer = lairTarget;
                    SetMovingTowardsTargetPlayer(lairTarget);
                }
            }
        }

        if ((currentLocalPhase == VecnaPhase.HauntChase || isHuntingEveryone) && IsOwner && !this.isInLair)
        {
            if (isHuntingEveryone)
            {
                if (BreakIntoShip()) return;

                PlayerControllerB closestVisible = null;
                float closestDist = 9999f;
                foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                {
                    if (p == null || p.isPlayerDead || !p.isPlayerControlled) continue;
                    float d = Vector3.Distance(transform.position, p.transform.position);
                    if (d < closestDist && d < 100f && !Physics.Linecast(transform.position + Vector3.up, p.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                    {
                        closestVisible = p;
                        closestDist = d;
                    }
                }
                if (closestVisible != null)
                {
                    targetPlayer = closestVisible;
                    cursingPlayer = closestVisible;
                }
                else
                {
                    base.TargetClosestPlayer(1.5f, false, 70f, false, false, true);
                    if (targetPlayer == null)
                    {
                        PlayerControllerB absoluteClosest = null;
                        float absoluteClosestDist = 9999f;
                        foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                        {
                            if (p == null || p.isPlayerDead || !p.isPlayerControlled) continue;
                            float d = Vector3.Distance(transform.position, p.transform.position);
                            if (d < absoluteClosestDist)
                            {
                                absoluteClosestDist = d;
                                absoluteClosest = p;
                            }
                        }
                        targetPlayer = absoluteClosest;
                    }
                    if (targetPlayer != null) cursingPlayer = targetPlayer;
                }

                if (targetPlayer != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    bool targetInLair = IsPlayerInLair(targetPlayer);
                    
                    if (!this.isInLair && targetInLair)
                    {
                        VecnaLairPortal targetPortal = activeEntrancePortal;
                        if (targetPortal != null)
                        {
                            SetDestinationToPosition(targetPortal.transform.position, checkForPath: true);
                            if (Vector3.Distance(transform.position, targetPortal.transform.position) < 3f)
                                targetPortal.TeleportVecna(this);
                        }
                    }
                    else if (!isInLair && this.isOutside != !targetPlayer.isInsideFactory)
                    {
                        EntranceTeleport chaserDoor = GetClosestDoorToVecna();
                        if (chaserDoor != null)
                        {
                            SetDestinationToPosition(chaserDoor.transform.position, checkForPath: false);
                            if (Vector3.Distance(transform.position, chaserDoor.transform.position) < 4f)
                            {
                                EntranceTeleport exitDoor = GetCorrespondingDoor(chaserDoor);
                                if (exitDoor != null)
                                {
                                    bool playerIsOutside = !targetPlayer.isInsideFactory;
                                    //Debug.Log($"[VECNA PORTAL SYSTEM] Teleporting through entrance/exit door to {exitDoor.entrancePoint.position}. Target isOutside: {playerIsOutside}. Current Vecna position: {transform.position}");
                                    TeleportEnemyClientRpc(exitDoor.entrancePoint.position, playerIsOutside);
                                }
                            }
                        }
                    }
                    else
                    {
                        SetDestinationToPosition(targetPlayer.transform.position, checkForPath: true);
                    }
                }
            }
            else
            {
                PlayerControllerB target = cursingPlayer;
                if (target != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    bool playerIsOutside = !target.isInsideFactory;
                    bool targetInLair = IsPlayerInLair(target);

                    if (!this.isInLair && targetInLair)
                    {
                        VecnaLairPortal targetPortal = activeEntrancePortal;
                        if (targetPortal != null)
                        {
                            SetDestinationToPosition(targetPortal.transform.position, checkForPath: true);
                            if (Vector3.Distance(transform.position, targetPortal.transform.position) < 3f)
                                targetPortal.TeleportVecna(this);
                        }
                    }
                    else if (!isInLair && this.isOutside != playerIsOutside)
                    {
                        EntranceTeleport chaserDoor = GetClosestDoorToVecna();
                        if (chaserDoor != null)
                        {
                            SetDestinationToPosition(chaserDoor.transform.position, checkForPath: false);
                            if (Vector3.Distance(transform.position, chaserDoor.transform.position) < 4f)
                            {
                                EntranceTeleport exitDoor = GetCorrespondingDoor(chaserDoor);
                                if (exitDoor != null)
                                {
                                    //Debug.Log($"[VECNA PORTAL SYSTEM] Teleporting through entrance/exit door to {exitDoor.entrancePoint.position}. Target isOutside: {playerIsOutside}. Current Vecna position: {transform.position}");
                                    TeleportEnemyClientRpc(exitDoor.entrancePoint.position, playerIsOutside);
                                }
                            }
                        }
                    }
                    else
                    {
                        SetDestinationToPosition(target.transform.position, checkForPath: true);
                    }
                }
            }
        }
    }

	public override void Update()
	{
        if (isPryingDoor && inSpecialAnimation)
        {
            if (shipDoor != null)
            {
                transform.position = Vector3.Lerp(transform.position, shipDoor.outsideDoorPoint.position, 7f * Time.deltaTime);
                transform.rotation = Quaternion.Lerp(transform.rotation, shipDoor.outsideDoorPoint.rotation, 7f * Time.deltaTime);
                pryingDoorAnimTime = Mathf.Min(pryingDoorAnimTime + Time.deltaTime / pryOpenDoorAnimLength, 1f);
                
                shipDoor.shipDoorsAnimator.SetFloat("pryOpenDoor", pryingDoorAnimTime);
                
                if (pryingDoorAnimTime >= 0.5f && !hasTriggeredThrowAnim)
                {
                    if (creatureAnimator != null)
                    {
                        creatureAnimator.SetTrigger("teleThrow");
                    }
                    hasTriggeredThrowAnim = true;
                }
                
                BreakIntoShip();
                return;
            }
        }

        if (this.IsServer && serverTelekinesisCooldownTimer > 0f)
        {
            serverTelekinesisCooldownTimer -= Time.deltaTime;
        }
        if (queuedPhase.HasValue && !inSpecialAnimation)
        {
            ChangePhaseSafely(queuedPhase.Value);
            queuedPhase = null;
        }
        base.Update();
		PerformSlowEnvironmentScan();
		UpdateGlobalVisuals();
		HandleBreathing();

        if (isHuntingEveryone && this.IsOwner)
        {
            if (blastCooldownTimer > 0f)
            {
                blastCooldownTimer -= Time.deltaTime;
            }
            else if (!isCastingTelekinesis && !inSpecialAnimation)
            {
                bool playerWithinRange = false;
                foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (player != null && !player.isPlayerDead && player.isPlayerControlled)
                    {
                        if (Vector3.Distance(transform.position, player.transform.position) < 2.5f)
                        {
                            playerWithinRange = true;
                            break;
                        }
                    }
                }
                if (playerWithinRange)
                {
                    blastCooldownTimer = blastCooldown;
                    RequestBlastServerRpc();
                }
            }
        }
		if (currentBehaviourStateIndex != 4)
		{
			if (!base.IsOwner && !isHuntingEveryone)
			{
				if (enemyMeshEnabled)
				{
					enemyMeshEnabled = false;
					EnableEnemyMesh(enable: false, overrideDoNotSet: true);
				}
			}
			else if (cursingPlayer != null && GameNetworkManager.Instance.localPlayerController != cursingPlayer && !isHuntingEveryone)
			{
				ChangeOwnershipOfEnemy(cursingPlayer.actualClientId);
			}
		}

        if (isInLair && lairTrigger != null)
        {
            Collider col = lairTrigger.GetComponent<Collider>();
            if (col != null && !col.bounds.Contains(transform.position))
            {
                //Debug.Log($"[VECNA PORTAL SYSTEM] Vecna left the LairDetect trigger bounds! Position: {transform.position}. Setting isInLair = false, isOutside = false.");
                isInLair = false;
                isOutside = false;
                if (this.IsServer)
                {
                    SyncLairStateClientRpc(false);
                    SyncOutsideStateClientRpc(false);
                    SyncPositionToClients();
                }
            }
        }

        if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
        {
            return;
        }

        if (this.IsServer)
        {
            if (currentBehaviourStateIndex == 3 || currentBehaviourStateIndex == 4)
            {
                bool outOfLOS = false;
                if (currentBehaviourStateIndex == 3 && cursingPlayer != null)
                {
                    outOfLOS = !cursingPlayer.HasLineOfSightToPosition(transform.position, 60f, 60, 2f);
                }
                else if (currentBehaviourStateIndex == 4)
                {
                    bool anyoneHasLOS = false;
                    foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                    {
                        if (p != null && p.isPlayerControlled && !p.isPlayerDead)
                        {
                            if (p.HasLineOfSightToPosition(transform.position, 60f, 60, 2f))
                            {
                                anyoneHasLOS = true;
                                break;
                            }
                        }
                    }
                    outOfLOS = !anyoneHasLOS;
                }

                if (outOfLOS)
                {
                    outOfLOSTimer += Time.deltaTime;
                    if (outOfLOSTimer >= 20f)
                    {
                        outOfLOSTimer = 0f;
                        if (outOfLOSVoiceLines != null && outOfLOSVoiceLines.Length > 0)
                        {
                            int randIndex = UnityEngine.Random.Range(0, outOfLOSVoiceLines.Length);
                            PlayOutOfLOSVoiceLineClientRpc(randIndex);
                        }
                    }
                }
                else
                {
                    outOfLOSTimer = 0f;
                }
            }
            else
            {
                outOfLOSTimer = 0f;
            }
        }
        if (currentLocalPhase != VecnaPhase.HauntChase)
        {
            hasStartedChase = false;
            serverSpawnedClone = false;
            flickerTimer = 0f;
        }
        else
        {
            flickerTimer += Time.deltaTime;
            if (isLightFlickerOn && flickerTimer >= 1.5f)
            {
                flickerTimer = 0f;
                Vector3 clonePos = (activeClone != null) ? activeClone.transform.position : localVictimClonePos;
                StartCoroutine(FlickerPoweredLightsNearClone(clonePos, flickerFlashlights: true, disableFlashlights: true));
            }
        }

		BoomboxCheck();
		if (portalManager != null)
		{
			portalManager.UpdatePortalRotation();
		}
		
		doorSlamTimer += Time.deltaTime;
		if (doorSlamTimer > 1.5f)
		{
			doorSlamTimer = 0f;
			SlamNearbyDoorsCheck();
		}

		if (this.IsServer && currentBehaviourStateIndex >= 1 && currentBehaviourStateIndex < 4 && !inSpecialAnimation)
		{
			if (cursingPlayer == null || cursingPlayer.isPlayerDead || !cursingPlayer.isPlayerControlled)
			{
				Debug.Log("VECNA: Target died/DC'd mid-hunt! Resetting to Cooldown...");
				ResetHaunt(repelledByMusic: false, playerKilled: false);
				return;
			}
		}

		if (footstepsAudio != null)
		{
			if (currentBehaviourStateIndex == 3)
			{
				footstepsAudio.volume = IsVictimOrSpectatingVictim() ? 1f : 0f;
				if (breathingAudioSource != null) breathingAudioSource.volume = IsVictimOrSpectatingVictim() ? 1f : 0f;
			}
			else if (currentBehaviourStateIndex == 4)
			{
				footstepsAudio.volume = 1f;
				if (breathingAudioSource != null) breathingAudioSource.volume = 1f;
			}
			else
			{
				footstepsAudio.volume = 0f;
				if (breathingAudioSource != null) breathingAudioSource.volume = 0f;
			}
		}

        {
			switch (base.currentBehaviourStateIndex)
			{
			case 0:
				if (this.IsServer)
				{
					if (hauntCooldownTimer > 0f)
					{
						hauntCooldownTimer -= Time.deltaTime;
						break;
					}
					
					if (cursingPlayer == null || cursingPlayer.isPlayerDead || !cursingPlayer.isPlayerControlled)
					{
						Debug.Log("VECNA: Target missing/dead. Choosing new victim!");
						ChoosePlayerToCurse();
					}
					
					if (timesClockSpawned > 0)
					{
						ChangePhaseSafely(VecnaPhase.ClockStalking);
					}
					else
					{
						clockTimer += Time.deltaTime;
						if (clockTimer >= 3f)
						{
							clockTimer = 0f;
							ChangePhaseSafely(VecnaPhase.ClockStalking);
						}
					}
				}
				break;
			case 1:
				if (this.IsOwner)
				{
					if (cursingPlayer == null) return;
					
					if (timesClockSpawned >= 3)
					{
						if (GetPlayerVehicle(cursingPlayer) == null)
						{
							ChangePhaseSafely(VecnaPhase.HauntChase);
						}
						return;
					}

					if (currentClock == null && !waitingForPhaseChange)
					{
						if (IsMusicPlayingNearVictim())
						{
							break;
						}
						TrySpawningClock();
						clockLifetimeTimer = 0f;
						stareTimer = 0f;
					}
					else if (currentClock != null)
					{
						clockLifetimeTimer += Time.deltaTime;
						if (cursingPlayer.HasLineOfSightToPosition(currentClock.transform.position, 60f, 60, 2f))
						{
							if (stareTimer == 0f && !hasTauntedForCurrentClock)
							{
								hasTauntedForCurrentClock = true;
								SpotClock(timesClockSpawned);
							}
							stareTimer += Time.deltaTime;
						}
						else
						{
							if (stareTimer > 2f)
							{
								DisappearClock();
							}
							else if (stareTimer > 0f)
							{
								stareTimer = 0f;
							}
						}

						if (clockLifetimeTimer > 20f && currentClock != null && stareTimer == 0f)
						{
							if (HUDManager.Instance != null)
							{
								HUDManager.Instance.DisplayTip("????", "a clock tolled in the distance", isWarning: true);
							}
							DisappearClock();
						}
					}
				}
				break;
			case 2:
				break;
			case 3: //hauntchase
				if (this.IsOwner && !hasStartedChase)
				{
					hasStartedChase = true;
					InitializeHauntChase();
					base.SetMovingTowardsTargetPlayer(cursingPlayer);
				}
				if (this.IsServer)
				{
					if (!inSpecialAnimation)
					{
						chaseTimer += Time.deltaTime;
						if (chaseTimer >= hauntChaseTime)
						{
							chaseTimer = 0f;
							Debug.Log($"VECNA: HauntChase {hauntChaseTime}s limit reached! Returning to cooldown.");
							ResetHaunt(repelledByMusic: false, playerKilled: false);
						}
					}
				}
				break;
			case 4:
                    enemyMeshEnabled = true;
                    EnableEnemyMesh(enable: true, overrideDoNotSet: true, tamperWithMeshes: true);
                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        if (!isPullingPlayer && !inSpecialAnimation && stunNormalizedTimer <= 0f)
                        {
                            agent.speed = huntEveryoneChaseSpeed;
                            agent.isStopped = false;
                        }
                        else
                        {
                            agent.speed = 0f;
                            agent.velocity = Vector3.zero;
                            agent.isStopped = true;
                        }
                    }

					if (this.IsOwner)
					{
						if (targetPlayer != null && !isCastingTelekinesis && !inSpecialAnimation && stunNormalizedTimer <= 0f)
						{
                            float distToTarget = Vector3.Distance(transform.position, targetPlayer.transform.position);
							bool hasLOS = distToTarget <= 15f && (distToTarget < 3f || !Physics.Linecast(transform.position + Vector3.up * 1.5f, targetPlayer.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault));
							
                            if (telekinesisCooldown > 0f) telekinesisCooldown -= Time.deltaTime;

                            if (hasLOS)
							{
								telekinesisChargeTimer += Time.deltaTime;
                                bool closeRange = distToTarget <= 2.5f;
								if (telekinesisCooldown <= 0f && (closeRange || telekinesisChargeTimer >= 2f))
								{
									telekinesisChargeTimer = 0f;
									telekinesisCooldown = 8f;
                                    if (targetPlayer.health < 30)
                                    {
                                        isCastingTelekinesis = true;
                                        RequestExecutionPullServerRpc((int)targetPlayer.playerClientId);
                                    }
                                    else
                                    {
                                        isCastingTelekinesis = true;
                                        RequestTelekinesisAttackServerRpc((int)targetPlayer.playerClientId, this.isOutside, closeRange);
                                    }
								}
							}
							else
							{
								telekinesisChargeTimer = 0f;
							}
						}
					}



                break;
			}
		}
	}

	public bool TrySpawningClock()
	{
		if (cursingPlayer == null)
		{
			return false;
		}

		VehicleController car = GetPlayerVehicle(cursingPlayer);
		if (car != null)
		{
			Vector3 hoodPos = car.transform.position + (car.transform.forward * 3.8f) + (car.transform.up * -1.5f);
			SpawnVecnaClock(hoodPos, timesClockSpawned);
			if (currentClock != null)
			{
				currentClock.transform.SetParent(car.transform, true);
			}
			RequestClockSpawnFlickerServerRpc();
			stareTimer = 0f;
			clockLifetimeTimer = 0f;
			return true;
		}

		if (insideNodes == null || outsideNodes == null)
		{
			return false;
		}
		GameObject[] array = (cursingPlayer.isInsideFactory ? insideNodes : outsideNodes);
		List<GameObject> list = new List<GameObject>();
		GameObject[] array2 = array;
		foreach (GameObject val2 in array2)
		{
			if (val2 == null)
			{
				continue;
			}
			float num = Vector3.Distance((cursingPlayer).transform.position, val2.transform.position);
			if (num > 3f && num < 15f && !cursingPlayer.HasLineOfSightToPosition(val2.transform.position, 80f, 100, -1f, -1))
			{
				Vector3 val3 = val2.transform.position + Vector3.up * 1f;
				if (!Physics.CheckSphere(val3, 0.5f, StartOfRound.Instance.collidersAndRoomMask))
				{
					list.Add(val2);
				}
			}
		}
		if (list.Count > 0)
		{
			int index = vecnaCurseRandom.Next(list.Count);
			Vector3 position = list[index].transform.position;
			Vector3 val4 = position;
			NavMeshHit val5 = default(NavMeshHit);
			if (NavMesh.SamplePosition(position, out val5, 5f, -1))
			{
				val4 = ((NavMeshHit)(val5)).position;
			}
			SpawnVecnaClock(val4, timesClockSpawned);
			RequestClockSpawnFlickerServerRpc();
			return true;
		}
		return false;
	}

	public void SpawnVecnaClock(Vector3 spawnPos, int timesClockSpawned)
	{
		try
		{
			currentClock = UnityEngine.Object.Instantiate<GameObject>(ClockPrefab, spawnPos, Quaternion.identity);
			if (currentClock == null)
			{
				return;
			}
			hasTauntedForCurrentClock = false;
			
			AudioClip chimeToPlay = null;
			if (timesClockSpawned == 0) chimeToPlay = clockChime1;
			else if (timesClockSpawned == 1) chimeToPlay = clockChime2;
			else if (timesClockSpawned >= 2) chimeToPlay = clockChime3;
			
			if (chimeToPlay != null)
			{
				AudioSource[] clockSources = currentClock.GetComponentsInChildren<AudioSource>();
				if (clockSources != null && clockSources.Length > 0)
				{
					clockSources[0].playOnAwake = false;
					clockSources[0].Stop();
					clockSources[0].PlayOneShot(chimeToPlay, 1f);
				}
				else
				{
					AudioSource.PlayClipAtPoint(chimeToPlay, spawnPos, 1f);
				}
			}

			AudioSource[] componentsInChildren = currentClock.GetComponentsInChildren<AudioSource>(true);
			if (componentsInChildren != null && componentsInChildren.Length > 0)
			{
				AudioSource tickSource = componentsInChildren.Length > 1 ? componentsInChildren[1] : componentsInChildren[0];
				if (tickSource != null && clockTickingClips != null && clockTickingClips.Length > 0)
				{
					tickSource.clip = clockTickingClips[UnityEngine.Mathf.Clamp(timesClockSpawned, 0, clockTickingClips.Length - 1)];
					tickSource.Play();
				}
			}
            Vector3 val3 = Vector3.forward;
			Vector3 val4 = Vector3.forward;
			if (cursingPlayer != null)
			{
				val4 = (cursingPlayer).transform.position - currentClock.transform.position;
				val4.y = 0f;
				if (val4 != Vector3.zero)
				{
					val4 = val4.normalized;
				}
				else
				{
					val4 = Vector3.forward;
				}
			}
			if (cursingPlayer != null && !cursingPlayer.isInsideFactory)
			{
				val3 = val4;
			}
			else
			{
				float num = -1f;
				int collidersAndRoomMask = StartOfRound.Instance.collidersAndRoomMask;
				Vector3 rayStart = spawnPos + Vector3.up * 0.5f;
				
				NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(8, Allocator.TempJob);
				NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(8, Allocator.TempJob);

				for (int i = 0; i < 8; i++)
				{
					Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
					commands[i] = new RaycastCommand(rayStart, dir, new QueryParameters(collidersAndRoomMask, false, QueryTriggerInteraction.Ignore, false), 20f);
				}

				JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));
				handle.Complete();

				for (int i = 0; i < 8; i++)
				{
					Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
					float distance = 20f;
					if (results[i].collider != null)
					{
						distance = results[i].distance;
					}
					
					float dot = Vector3.Dot(dir, val4);
					float score = distance * (dot + 1.5f);
					if (distance < 1.5f) score *= 0.1f;
					
					if (score > num)
					{
						num = score;
						val3 = dir;
					}
				}

				commands.Dispose();
				results.Dispose();
			}
			if (val3 != Vector3.zero)
			{
				currentClock.transform.rotation = Quaternion.LookRotation(val3);
				currentClock.transform.Rotate(0f, 90f, 0f, (Space)1);
			}
			if (!cursingLocalPlayer)
			{
				Renderer[] componentsInChildren2 = currentClock.GetComponentsInChildren<Renderer>(true);
				Renderer[] array2 = componentsInChildren2;
				foreach (Renderer val18 in array2)
				{
					val18.enabled = false;
				}
				AudioSource[] array3 = componentsInChildren;
				foreach (AudioSource val19 in array3)
				{
					val19.volume = 0f;
				}
				Light[] componentsInChildren3 = currentClock.GetComponentsInChildren<Light>(true);
				Light[] array4 = componentsInChildren3;
				foreach (Light val20 in array4)
				{
					(val20).enabled = false;
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("Safely caught visual setup error in Clock Spawn: " + ex.Message));
		}
	}

	public void SpotClock(int currentClockCount)
	{
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (localPlayer != null)
        {
            localPlayer.JumpToFearLevel(0.8f, true);
            this.CancelInvoke("PlayDelayedChime");
            if (currentClockCount == 0 && finalChimeClip != null)
            {
                this.Invoke("PlayDelayedChime", 10f);
            }
            if (currentClockCount == 1)
            {
                //PlayClockSpotTaunt();
            }
            if (currentClockCount == 2)
            {
                PlayClockSpotTaunt();
            }
        }
    }

    public void PlayClockSpotTaunt()
    {
        if (clockSpotTaunts != null && clockSpotTaunts.Length > 0 && HUDManager.Instance != null && HUDManager.Instance.UIAudio != null)
        {
            HUDManager.Instance.UIAudio.PlayOneShot(clockSpotTaunts[UnityEngine.Random.Range(0, clockSpotTaunts.Length)], 1f);
        }
    }

    public void PlayEscapeVoiceLineToVictim()
    {
        if (escapeVoiceLines != null && escapeVoiceLines.Length > 0 && HUDManager.Instance != null && HUDManager.Instance.UIAudio != null)
        {
            HUDManager.Instance.UIAudio.PlayOneShot(escapeVoiceLines[UnityEngine.Random.Range(0, escapeVoiceLines.Length)], 1f);
        }
    }

    [ClientRpc]
    public void PlayOutOfLOSVoiceLineClientRpc(int clipIndex)
    {
        if (outOfLOSVoiceLines != null && clipIndex >= 0 && clipIndex < outOfLOSVoiceLines.Length && creatureVoice != null)
        {
            creatureVoice.PlayOneShot(outOfLOSVoiceLines[clipIndex], 1f);
        }
    }

    private void DisappearClock()
    {
        if (currentClock != null)
        {
            UnityEngine.Object.Destroy(currentClock);
            currentClock = null;
        }

        timesClockSpawned++;
        stareTimer = 0f;
        if (timesClockSpawned >= 3)
        {
            if (GetPlayerVehicle(cursingPlayer) == null)
            {
                ChangePhaseSafely(VecnaPhase.HauntChase);
            }
        }
        else
        {
            hauntCooldownTimer = clockSpawnInterval; // reset cooldown for next clock
            ChangePhaseSafely(VecnaPhase.Cooldown);
        }
    }

    private bool TryFindingChaseSpawnPosition(out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;
        if (cursingPlayer == null) return false;

        GameObject[] nodes = cursingPlayer.isInsideFactory ? insideNodes : outsideNodes;
        if (nodes == null || nodes.Length == 0) return false;

        List<Vector3> validPositions = new List<Vector3>();

        foreach (GameObject node in nodes)
        {
            if (node == null) continue;

            float dist = Vector3.Distance(cursingPlayer.transform.position, node.transform.position);
            if (dist >= 15f && dist <= 30f)
            {
                bool inPlayerFOV = cursingPlayer.HasLineOfSightToPosition(node.transform.position, 60f, 40);
                if (!inPlayerFOV)
                {
                    validPositions.Add(node.transform.position);
                }
            }
        }

        if (validPositions.Count == 0)
        {
            foreach (GameObject node in nodes)
            {
                if (node == null) continue;

                float dist = Vector3.Distance(cursingPlayer.transform.position, node.transform.position);
                if (dist >= 10f && dist <= 45f)
                {
                    bool inPlayerFOV = cursingPlayer.HasLineOfSightToPosition(node.transform.position, 60f, 50);
                    if (!inPlayerFOV)
                    {
                        validPositions.Add(node.transform.position);
                    }
                }
            }
        }

        if (validPositions.Count > 0)
        {
            Vector3 targetPos = validPositions[UnityEngine.Random.Range(0, validPositions.Count)];
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 5f, -1))
            {
                spawnPos = hit.position;
                return true;
            }
        }

        spawnPos = cursingPlayer.transform.position - cursingPlayer.transform.forward * 15f;
        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(spawnPos, out fallbackHit, 10f, -1))
        {
            spawnPos = fallbackHit.position;
            return true;
        }

        return false;
    }

    private void InitializeHauntChase()
    {
        if (this.IsOwner)
        {
            Vector3 spawnPos;
            if (TryFindingChaseSpawnPosition(out spawnPos))
            {
                if (agent != null)
                {
                    agent.enabled = false;
                    transform.position = spawnPos;
                    base.serverPosition = spawnPos;
                    agent.enabled = true;
                    agent.Warp(spawnPos);
                    agent.speed = hauntChaseSpeed;
                }
                else
                {
                    transform.position = spawnPos;
                    base.serverPosition = spawnPos;
                }
                SyncPositionToClients();
            }

            EnableEnemyMesh(enable: true, overrideDoNotSet: true, tamperWithMeshes: true);
            enemyMeshEnabled = true;
            SFXVolumeLerpTo = 1f;
            StartAllSFX();
        }
    }

    private void UpdateGlobalVisuals()
    {
        if (isEnemyDead)
        {
            if (!enemyMeshEnabled)
            {
                EnableEnemyMesh(enable: true, overrideDoNotSet: true, tamperWithMeshes: true);
                enemyMeshEnabled = true;
            }
            return;
        }

        if (!isHuntingEveryone && (currentLocalPhase == VecnaPhase.Cooldown || currentLocalPhase == VecnaPhase.ClockStalking || currentLocalPhase == VecnaPhase.ClockSpotted))
        {
            return;
        }

        bool shouldSeeVecna = isHuntingEveryone || (currentLocalPhase == VecnaPhase.HauntChase && IsVictimOrSpectatingVictim());

        if (enemyMeshEnabled != shouldSeeVecna)
        {
            EnableEnemyMesh(shouldSeeVecna, overrideDoNotSet: true, tamperWithMeshes: true);
            enemyMeshEnabled = shouldSeeVecna;
        }
    }
	private void SpawnPlayerClone()
	{
		DestroyActiveClone();
		if (ClonePrefab == null || cursingPlayer == null) return;

		Vector3 victimPos = cursingPlayer.transform.position;
		Quaternion victimRot = cursingPlayer.transform.rotation;

		activeClone = UnityEngine.Object.Instantiate(ClonePrefab, victimPos, victimRot);
		activeClone.transform.localScale = Vector3.one;

		foreach (Collider val9 in activeClone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(val9);
		foreach (CharacterController val10 in activeClone.GetComponentsInChildren<CharacterController>(true)) UnityEngine.Object.Destroy(val10);

		activeCloneAnim = activeClone.GetComponentInChildren<Animator>();
		if (activeCloneAnim != null)
		{
			activeCloneAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			activeCloneAnim.enabled = true;
		}

		foreach (MeshRenderer val11 in activeClone.GetComponentsInChildren<MeshRenderer>())
		{
			if (!val11.gameObject.name.Contains("CloneNametag"))
			{
				if (val11.transform.localScale.sqrMagnitude > 10f)
				{
					val11.transform.localScale = new Vector3(1f, 1f, 1f);
				}
			}
		}

		VecnaVFXHelper.DressCloneLikePlayer(activeClone, cursingPlayer);

		if (cursingPlayer.usernameCanvas != null)
		{
			Transform val12 = null;
			foreach (Transform val13 in activeClone.GetComponentsInChildren<Transform>())
			{
				string text3 = val13.name.ToLower().Replace("_", ".");
				if (text3.Contains("spine.004") && !text3.Contains("end"))
				{
					val12 = val13;
					break;
				}
			}
			if (val12 == null) val12 = activeClone.transform;
			
			GameObject val14 = UnityEngine.Object.Instantiate(cursingPlayer.usernameCanvas.gameObject);
			val14.name = "CloneNametag";
			val14.transform.SetParent(val12);
			Vector3 lossyScale = cursingPlayer.usernameCanvas.transform.lossyScale;
			Vector3 lossyScale2 = val12.lossyScale;
			val14.transform.localScale = new Vector3((lossyScale2.x > 0f) ? (lossyScale.x / lossyScale2.x) : 0f, (lossyScale2.y > 0f) ? (lossyScale.y / lossyScale2.y) : 0f, (lossyScale2.z > 0f) ? (lossyScale.z / lossyScale2.z) : 0f);
			val14.transform.position = val12.position + new Vector3(0f, 0.6f, 0f);
			val14.transform.rotation = activeClone.transform.rotation;
			
			Canvas component = val14.GetComponent<Canvas>();
			if (component != null) component.enabled = true;
			
			CanvasGroup component2 = val14.GetComponent<CanvasGroup>();
			if (component2 != null) component2.alpha = 1f;
			
			foreach (UnityEngine.MonoBehaviour val15 in val14.GetComponentsInChildren<UnityEngine.MonoBehaviour>())
			{
				if (!(val15 is TMPro.TextMeshProUGUI) && val15.GetType().Name != "PlayerNameBillboard")
				{
					UnityEngine.Object.DestroyImmediate(val15);
				}
			}
			
			TMPro.TextMeshProUGUI componentInChildren = val14.GetComponentInChildren<TMPro.TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.text = cursingPlayer.playerUsername;
				componentInChildren.enabled = true;
			}
			val14.SetActive(true);
		}
	}

    public override void OnCollideWithPlayer(Collider other)
	{
        base.OnCollideWithPlayer(other);
        PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other, inKillAnimation: inSpecialAnimation, overrideIsInsideFactoryCheck: true);
        
        if (playerControllerB == null) return;

        if (currentBehaviourStateIndex == 3)
		{
			if (!cursingLocalPlayer) return;
            if (playerControllerB == cursingPlayer && !inSpecialAnimation)
            {
                RequestHauntKillServerRpc((int)playerControllerB.playerClientId);
            }
		}
		else if (currentBehaviourStateIndex == 4)
		{
            if (!inSpecialAnimation && !isPullingPlayer && playerControllerB.health < 30)
            {
                if (this.IsServer)
                {
                    SyncVisibleKillClientRpc((int)playerControllerB.playerClientId);
                }
                else if (playerControllerB == GameNetworkManager.Instance.localPlayerController)
                {
                    RequestVisibleKillServerRpc((int)playerControllerB.playerClientId);
                }
            }
		}
	}

    [ServerRpc(RequireOwnership = false)]
    public void RequestHauntKillServerRpc(int playerId)
    {
        SyncHauntKillClientRpc(playerId);
    }

    [ClientRpc]
    public void SyncHauntKillClientRpc(int playerId)
    {
        if (inSpecialAnimation) return;
        inSpecialAnimation = true;

        if (executionVoiceLines != null && executionVoiceLines.Length > 0 && creatureVoice != null)
        {
            creatureVoice.PlayOneShot(executionVoiceLines[UnityEngine.Random.Range(0, executionVoiceLines.Length)], 1f);
        }

        PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[playerId];
        
        Vector3 directionFromPlayer = (transform.position - victim.transform.position).normalized;
        directionFromPlayer.y = 0f;
        if (directionFromPlayer == Vector3.zero) directionFromPlayer = transform.forward;
        
        Vector3 stopPos = victim.transform.position + directionFromPlayer * 2.5f;
        
        if (this.agent != null && this.agent.isActiveAndEnabled && this.agent.isOnNavMesh)
        {
            this.movingTowardsTargetPlayer = false;
            this.agent.speed = 0f;
            this.agent.velocity = Vector3.zero;
            this.agent.ResetPath();
            this.agent.Warp(stopPos);
        }
        
        transform.position = stopPos;
        serverPosition = stopPos;
        transform.LookAt(victim.transform.position);

        if (creatureAnimator != null)
        {
            creatureAnimator.SetTrigger("swingAttack");
        }

        if (victim != null)
        {
            if (victim == GameNetworkManager.Instance.localPlayerController)
            {
                StartCoroutine(HauntKillRoutine());
            }
            else
            {
                StartCoroutine(OtherClientHauntKillRoutine(victim));
            }
        }
    }

    private IEnumerator OtherClientHauntKillRoutine(PlayerControllerB victim)
    {
        if (vecnafpexecution != null && vecnaSnapAudioSource != null)
        {
            vecnaSnapAudioSource.PlayOneShot(vecnafpexecution);
        }

        float timer = 0f;
        Vector3 startPos = victim.transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.8f;

        while (timer < 2.5f)
        {
            timer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, timer / 2.5f);
            victim.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        if (activeClone != null)
        {
            foreach (Animator anim in activeClone.GetComponentsInChildren<Animator>(true))
            {
                anim.SetTrigger("isSnapped");
                anim.SetBool("isSnapped", true);
            }
        }

        timer = 0f;
        while (timer < 3.2f)
        {
            timer += Time.deltaTime;
            victim.transform.position = endPos;
            yield return null;
        }

        if (playerSnapClip != null && vecnaSnapAudioSource != null)
        {
            vecnaSnapAudioSource.PlayOneShot(playerSnapClip);
        }

        yield return new WaitForSeconds(0.65f);

        inSpecialAnimation = false;
    }

    private IEnumerator HauntKillRoutine()
    {
        PlayerControllerB victim = cursingPlayer;
        if (victim == null) yield break;

        victim.inSpecialInteractAnimation = true;
        victim.inAnimationWithEnemy = this;
        victim.disableLookInput = true;
        victim.disableMoveInput = true;
        victim.disableInteract = true;
        if (victim.thisController != null) victim.thisController.enabled = false;
        victim.fallValue = 0f;
        victim.fallValueUncapped = 0f;

        if (vecnafpexecution != null && vecnaSnapAudioSource != null)
        {
            vecnaSnapAudioSource.PlayOneShot(vecnafpexecution);
        }

        float liftDuration = 2.5f;
        float elapsed = 0f;
        Vector3 startPos = victim.transform.position;
        Vector3 targetPos = startPos + Vector3.up * 0.8f;
        
        RaycastHit hit = default;
        if (Physics.Raycast(startPos, Vector3.up, out hit, 2.6f, StartOfRound.Instance.collidersAndRoomMask))
        {
            targetPos = startPos + Vector3.up * Mathf.Max(0f, hit.distance - 0.5f);
        }

        Vector3 dirToVecna = transform.position - victim.transform.position;
        dirToVecna.y = 0f;
        if (dirToVecna != Vector3.zero) dirToVecna.Normalize();

        Quaternion startBodyRot = victim.transform.rotation;
        Quaternion targetBodyRot = (dirToVecna != Vector3.zero) ? Quaternion.LookRotation(dirToVecna) : startBodyRot;
        Quaternion startCamRot = victim.gameplayCamera.transform.rotation;
        Vector3 originalCamRot = victim.gameplayCamera.transform.localEulerAngles;

        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / liftDuration);
            if (!victim.playerCollider.enabled) victim.playerCollider.enabled = true;

            victim.transform.position = Vector3.Lerp(startPos, targetPos, t);
            victim.transform.rotation = Quaternion.Slerp(startBodyRot, targetBodyRot, t);

            Vector3 vecnaFace = transform.position + Vector3.up * 2.4f;
            Vector3 dirFromCam = vecnaFace - victim.gameplayCamera.transform.position;
            if (dirFromCam != Vector3.zero)
            {
                victim.gameplayCamera.transform.rotation = Quaternion.Slerp(startCamRot, Quaternion.LookRotation(dirFromCam), t);
            }

            victim.fallValue = 0f;
            victim.fallValueUncapped = 0f;
            yield return null;
        }

        if (activeClone != null)
        {
            foreach (Animator anim in activeClone.GetComponentsInChildren<Animator>(true))
            {
                anim.SetTrigger("isSnapped");
                anim.SetBool("isSnapped", true);
            }
        }

        float holdDuration = 3.2f;
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

        if (playerSnapClip != null && vecnaSnapAudioSource != null)
        {
            vecnaSnapAudioSource.PlayOneShot(playerSnapClip);
        }

        Vector3 currentCamRotEulers = victim.gameplayCamera.transform.localEulerAngles;
        victim.gameplayCamera.transform.localEulerAngles = new Vector3(currentCamRotEulers.x - 45f, currentCamRotEulers.y + 60f, 70f);
        yield return new WaitForSeconds(0.21f);

        Vector3 finalDeathPos = (this.IsServer && serverVictimClonePos != Vector3.zero) 
            ? serverVictimClonePos + Vector3.up * 0.1f
            : (activeClone != null ? activeClone.transform.position + Vector3.up * 0.1f
            : (localVictimClonePos != Vector3.zero ? localVictimClonePos + Vector3.up * 0.1f
            : victim.transform.position));

        if (activeClone != null)
        {
            if (victim.thisController != null) victim.thisController.enabled = true;
            DestroyActiveClone();
            victim.TeleportPlayer(finalDeathPos, false, 0f, false, true);
            yield return new WaitForSeconds(0.8f);
        }
        else
        {
            if (victim.thisController != null) victim.thisController.enabled = true;
            yield return new WaitForSeconds(0.8f);
        }

        VecnaVFXHelper.TogglePlayerThirdPersonModel(this, victim, true);
        if (victim.thisController != null) victim.thisController.enabled = false;
        victim.transform.position = finalDeathPos;
        victim.gameplayCamera.transform.localEulerAngles = originalCamRot;

        victim.inSpecialInteractAnimation = false;
        victim.inAnimationWithEnemy = null;
        victim.disableLookInput = false;
        victim.disableMoveInput = false;
        victim.disableInteract = false;

        victim.KillPlayer(Vector3.zero, spawnBody: true, CauseOfDeath.Strangulation, 0, Vector3.zero);

        inSpecialAnimation = false;
        if (this.IsServer)
        {
            ResetHaunt(repelledByMusic: false, playerKilled: true);
        }
    }

    [ClientRpc]
    public void SyncVisibleKillClientRpc(int playerId)
    {
        inSpecialAnimation = true;

        if (executionVoiceLines != null && executionVoiceLines.Length > 0 && creatureVoice != null)
        {
            creatureVoice.PlayOneShot(executionVoiceLines[UnityEngine.Random.Range(0, executionVoiceLines.Length)], 1f);
        }

        PlayerControllerB victim = StartOfRound.Instance.allPlayerScripts[playerId];
        
        Vector3 directionFromPlayer = (transform.position - victim.transform.position).normalized;
        directionFromPlayer.y = 0f;
        if (directionFromPlayer == Vector3.zero) directionFromPlayer = transform.forward;
        
        Vector3 stopPos = victim.transform.position + victim.transform.forward * 2.5f;
        
        if (this.agent != null && this.agent.isActiveAndEnabled && this.agent.isOnNavMesh)
        {
            this.movingTowardsTargetPlayer = false;
            this.agent.speed = 0f;
            this.agent.velocity = Vector3.zero;
            this.agent.isStopped = true;
            this.agent.ResetPath();
            this.agent.Warp(stopPos);
        }
        
        transform.position = stopPos;
        serverPosition = stopPos;
        transform.LookAt(victim.transform.position);

        if (creatureAnimator != null)
        {
            creatureAnimator.SetTrigger("swingAttack");
        }

        StartCoroutine(VisibleKillRoutine(victim));
    }

    private IEnumerator VisibleKillRoutine(PlayerControllerB victim)
    {
        if (victim != null)
        {
            EnableVecnaPullAnimator(victim);
        }
        victim.inSpecialInteractAnimation = true;
        victim.inAnimationWithEnemy = this;
        victim.disableLookInput = true;
        victim.disableMoveInput = true;
        victim.disableInteract = true;
        if (victim.thisController != null) victim.thisController.enabled = false;
        victim.fallValue = 0f;
        victim.fallValueUncapped = 0f;

        if (vecnafpexecution != null && vecnaSnapAudioSource != null)
        {
            vecnaSnapAudioSource.PlayOneShot(vecnafpexecution);
        }

        float timer = 0f;
        Vector3 startPos = victim.transform.position;
        Vector3 endPos = startPos + Vector3.up * 1f;

        while (timer < 5.5f)
        {
            if (stunNormalizedTimer > 0f || isEnemyDead || Vector3.Distance(transform.position, victim.transform.position) > 50f)
            {
                if (victim.thisController != null) victim.thisController.enabled = true;
                victim.disableLookInput = false;
                victim.disableMoveInput = false;
                victim.disableInteract = false;
                if (victim.playerBodyAnimator != null)
                {
                    victim.playerBodyAnimator.SetBool("startExecuting", false);
                }
                if (victim != null)
                {
                    DisableVecnaPullAnimator(victim);
                    if (victim == GameNetworkManager.Instance.localPlayerController) victim.disableMoveInput = false;
                    victim.inSpecialInteractAnimation = false;
                    victim.inAnimationWithEnemy = null;
                }
                inSpecialAnimation = false;
                yield break;
            }

            timer += Time.deltaTime;
            victim.transform.position = Vector3.Lerp(startPos, endPos, timer / 5.5f);
            yield return null;
        }

        if (playerSnapClip != null && vecnaSnapAudioSource != null)
        {
            vecnaSnapAudioSource.PlayOneShot(playerSnapClip);
        }

        yield return new WaitForSeconds(0.5f);

        if (victim.thisController != null) victim.thisController.enabled = true;
        victim.disableLookInput = false;
        victim.disableMoveInput = false;
        victim.disableInteract = false;

        if (activeClone != null)
        {
            victim.TeleportPlayer(activeClone.transform.position);
        }

        if (victim == GameNetworkManager.Instance.localPlayerController)
        {
            victim.DamagePlayer(100, hasDamageSFX: true, callRPC: true, CauseOfDeath.Strangulation, 0, false, default);
        }

        if (victim.playerBodyAnimator != null)
        {
            victim.playerBodyAnimator.SetBool("startExecuting", false);
        }
        
        if (victim != null)
        {
            DisableVecnaPullAnimator(victim);
            if (victim == GameNetworkManager.Instance.localPlayerController) victim.disableMoveInput = false;
            victim.inSpecialInteractAnimation = false;
            victim.inAnimationWithEnemy = null;
        }
        
        inSpecialAnimation = false;

        if (this.IsServer)
        {
            ResetHaunt(repelledByMusic: false, playerKilled: true);
        }
    }

    [ClientRpc]
    public void ExecutionPullClientRpc(int playerId)
    {
        PlayerControllerB target = StartOfRound.Instance.allPlayerScripts[playerId];
        if (target == null) return;

        if (pullTauntVoiceLines != null && pullTauntVoiceLines.Length > 0 && creatureVoice != null)
        {
            creatureVoice.PlayOneShot(pullTauntVoiceLines[UnityEngine.Random.Range(0, pullTauntVoiceLines.Length)], 1f);
        }

        StartCoroutine(ExecutionPullRoutine(target));
    }

    private IEnumerator ExecutionPullRoutine(PlayerControllerB target)
    {
        inSpecialAnimation = true;
        isCastingTelekinesis = true;
        StartPullingPlayer();

        if (target == null || target.isPlayerDead)
        {
            StopPullingPlayer();
            isCastingTelekinesis = false;
            inSpecialAnimation = false;
            yield break;
        }

        target.inSpecialInteractAnimation = true;
        target.inAnimationWithEnemy = this;

        bool startsClose = Vector3.Distance(transform.position, target.transform.position) <= 3f;
        bool pullSucceeded = startsClose;

        if (!startsClose)
        {
            if (telekinesisWindupSFX != null && creatureVoice != null)
            {
                creatureVoice.clip = telekinesisWindupSFX;
                creatureVoice.Play();
            }
            
            if (creatureAnimator != null)
            {
                creatureAnimator.SetBool("isPulling", true);
            }
            
            EnableVecnaPullAnimator(target);
            
            if (target == GameNetworkManager.Instance.localPlayerController)
            {
                target.disableMoveInput = true;
            }

            yield return new WaitForSeconds(1.5f);
            
            float pullTimer = 0f;
            if (pullingPlayerSFX != null && creatureVoice != null)
            {
                creatureVoice.clip = pullingPlayerSFX;
                creatureVoice.loop = true;
                creatureVoice.pitch = 1f;
                creatureVoice.Play();
            }

            while (pullTimer < 8f && target != null && !target.isPlayerDead)
            {
                if (stunNormalizedTimer > 0f || isEnemyDead || Vector3.Distance(transform.position, target.transform.position) > 50f)
                {
                    pullSucceeded = false;
                    break;
                }

                if (Vector3.Distance(transform.position, target.transform.position) <= 3f)
                {
                    pullSucceeded = true;
                    break;
                }

                if (target == GameNetworkManager.Instance.localPlayerController)
                {
                    target.transform.position = Vector3.MoveTowards(target.transform.position, transform.position, 1.25f * Time.deltaTime);
                    Vector3 lookPos = transform.position;
                    lookPos.y = target.transform.position.y;
                    target.transform.LookAt(lookPos);
                }
                pullTimer += Time.deltaTime;

                if (creatureVoice != null && pullingPlayerSFX != null && creatureVoice.clip == pullingPlayerSFX)
                {
                    creatureVoice.pitch = Mathf.Lerp(1f, 2f, pullTimer / 8f);
                }

                yield return null;
            }

            if (creatureVoice != null && pullingPlayerSFX != null && creatureVoice.clip == pullingPlayerSFX)
            {
                creatureVoice.Stop();
                creatureVoice.loop = false;
                creatureVoice.pitch = 1f;
            }
        }
        else
        {
            EnableVecnaPullAnimator(target);
            if (target != null && target.playerBodyAnimator != null)
            {
                target.playerBodyAnimator.SetBool("isBeingPulled", false);
            }
            if (target == GameNetworkManager.Instance.localPlayerController)
            {
                target.disableMoveInput = true;
            }
        }
        
        if (target != null && !target.isPlayerDead && pullSucceeded)
        {
            if (target == GameNetworkManager.Instance.localPlayerController)
            {
                target.externalForces = Vector3.zero;
            }
            
            if (creatureAnimator != null) 
            {
                creatureAnimator.SetBool("isPulling", false);
            }
            
            if (target.playerBodyAnimator != null)
            {
                if (!startsClose)
                {
                    target.playerBodyAnimator.SetBool("isBeingPulled", false);
                }
                target.playerBodyAnimator.SetBool("startExecuting", true);
            }
            
            if (target == GameNetworkManager.Instance.localPlayerController)
            {
                Vector3 lookPos = transform.position;
                lookPos.y = target.transform.position.y;
                target.transform.LookAt(lookPos);
            }
            
            if (this.IsServer)
            {
                inSpecialAnimation = true;
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.speed = 0f;
                    agent.velocity = Vector3.zero;
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                SyncVisibleKillClientRpc((int)target.playerClientId);
            }
        }
        else
        {
            if (creatureAnimator != null) creatureAnimator.SetBool("isPulling", false);
        }
        
        if (!pullSucceeded)
        {
            if (target != null) DisableVecnaPullAnimator(target);
            if (target != null && target == GameNetworkManager.Instance.localPlayerController) target.disableMoveInput = false;

            if (target != null)
            {
                target.inSpecialInteractAnimation = false;
                target.inAnimationWithEnemy = null;
            }
            inSpecialAnimation = false;
        }

        if (creatureVoice != null && pullingPlayerSFX != null && creatureVoice.clip == pullingPlayerSFX)
        {
            creatureVoice.Stop();
            creatureVoice.loop = false;
            creatureVoice.pitch = 1f;
        }

        StopPullingPlayer();
        isCastingTelekinesis = false;
    }

    [ClientRpc]
    public void TelekinesisAttackClientRpc(int targetPlayerId, bool isOutside, bool isCloseRange)
    {
        if (inSpecialAnimation) return;
        PlayerControllerB target = StartOfRound.Instance.allPlayerScripts[targetPlayerId];
        if (target != null)
        {
            StartCoroutine(TelekinesisAttackRoutine(target, isOutside, isCloseRange));
        }
    }

    private IEnumerator TelekinesisAttackRoutine(PlayerControllerB target, bool isOutside, bool isCloseRange)
    {
        isCastingTelekinesis = true;

        AudioSource local2DAudio = null;
        if (target == GameNetworkManager.Instance.localPlayerController)
        {
            GameObject sfxObj = new GameObject("VecnaTelekinesis2DLocalSFX");
            local2DAudio = sfxObj.AddComponent<AudioSource>();
            local2DAudio.spatialBlend = 0f;
            local2DAudio.clip = telekinesisWindupSFX;
            local2DAudio.volume = 1f;
            local2DAudio.pitch = 1f;
            local2DAudio.Play();
        }
        else if (telekinesisWindupSFX != null && creatureVoice != null)
        {
            creatureVoice.clip = telekinesisWindupSFX;
            creatureVoice.volume = 1f;
            creatureVoice.pitch = 1f;
            creatureVoice.Play();
        }

        float windupTimer = 0f;
        float currentPitch = 1f;
        while (windupTimer < 1.5f)
        {
            windupTimer += Time.deltaTime;

            if (target == null || target.isPlayerDead)
            {
                Debug.Log("VECNA: Telekinesis cancelled mid-windup, target lost or dead.");
                if (local2DAudio != null) UnityEngine.Object.Destroy(local2DAudio.gameObject);
                if (creatureVoice != null) { creatureVoice.Stop(); creatureVoice.pitch = 1f; }
                isCastingTelekinesis = false;
                yield break;
            }

            float distToTarget = Vector3.Distance(transform.position, target.transform.position);
            bool hasLOS = distToTarget < 3f || !Physics.Linecast(transform.position + Vector3.up * 1.5f, target.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault);

            if (hasLOS)
            {
                currentPitch = Mathf.MoveTowards(currentPitch, 1.8f, Time.deltaTime * 0.53f);
            }
            else
            {
                currentPitch = Mathf.MoveTowards(currentPitch, 0.9f, Time.deltaTime * 3.0f);
            }

            if (local2DAudio != null) local2DAudio.pitch = currentPitch;
            else if (creatureVoice != null) creatureVoice.pitch = currentPitch;

            if (currentPitch <= 1.0f && !hasLOS)
            {
                Debug.Log("VECNA: Telekinesis cancelled mid-windup, line of Sight broken.");
                if (local2DAudio != null) UnityEngine.Object.Destroy(local2DAudio.gameObject);
                if (creatureVoice != null) { creatureVoice.Stop(); creatureVoice.pitch = 1f; }
                isCastingTelekinesis = false;
                yield break;
            }

            yield return null;
        }

        if (local2DAudio != null) UnityEngine.Object.Destroy(local2DAudio.gameObject);
        if (creatureVoice != null) creatureVoice.pitch = 1f;

        if (creatureAnimator != null) creatureAnimator.SetTrigger((isOutside && !isInLair) ? "teleThrow" : "telePush");
        if (telePushExecuteSFX != null && creatureVoice != null)
        {
            creatureVoice.clip = telePushExecuteSFX;
            creatureVoice.Play();
        }

        if (isCloseRange)
        {
            if (teleBlastParticle != null)
            {
                teleBlastParticle.Play();
            }
        }
        else
        {
            if (telePushParticle != null)
            {
                telePushParticle.Play();
            }
        }

        if (target == GameNetworkManager.Instance.localPlayerController)
        {
            if (isOutside && !isInLair)
            {
                Vector3 pushDirection = (target.transform.position - transform.position);
                pushDirection.y = 0f;
                pushDirection = pushDirection.normalized;
                pushDirection.y = 1.0f;
                pushDirection = pushDirection.normalized;
                target.externalForceAutoFade += pushDirection * (telePushKnockback * 0.67f);
                target.DamagePlayer(telekinesisBaseDmg, hasDamageSFX: true, callRPC: true, CauseOfDeath.Gravity, 0, false, default);
            }
            else
            {
                Vector3 pushDirection = (target.transform.position - transform.position).normalized;
                pushDirection.y = 0.2f;
                target.externalForceAutoFade += pushDirection * telePushKnockback;
                StartCoroutine(HitWallCheckRoutine(target, pushDirection));
            }
        }

        yield return new WaitForSeconds(1f);
        if (!isOutside || isInLair)
        {
            Transform distortionPowerT = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "DistortionPower")
                {
                    distortionPowerT = t;
                    break;
                }
            }
            if (distortionPowerT != null)
            {
                distortionPowerT.gameObject.SetActive(true);
                ParticleSystem ps = distortionPowerT.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Play();
                }
            }
        }
        isCastingTelekinesis = false;
    }

    private IEnumerator HitWallCheckRoutine(PlayerControllerB player, Vector3 pushDir)
    {
        yield return new WaitForSeconds(0.1f);
        if (player == null) yield break;
        float checkTime = 0f;
        Vector3 lastPos = player.transform.position;
        bool dealtDamage = false;

        while (checkTime < 0.4f && player != null && player.externalForceAutoFade.magnitude > 10f)
        {
            yield return new WaitForSeconds(0.1f);
            checkTime += 0.1f;

            float distMoved = Vector3.Distance(lastPos, player.transform.position);
            if (distMoved < 0.5f)
            {
                player.DamagePlayer(blastBaseDmg, true, true, CauseOfDeath.Bludgeoning, 0, false, pushDir);
                dealtDamage = true;
                player.hinderedMultiplier = 0.5f;
                yield return new WaitForSeconds(1f);
                player.hinderedMultiplier = 1f;
                break;
            }
            lastPos = player.transform.position;
        }

        if (!dealtDamage && player != null && !player.isPlayerDead)
        {
            player.DamagePlayer(telekinesisBaseDmg, true, true, CauseOfDeath.Bludgeoning, 0, false, pushDir);
        }
    }

	private void SlamNearbyDoorsCheck()
	{
        if (!this.IsServer) return;
		if (cachedDoors == null) return;

		foreach (DoorLock door in cachedDoors)
		{
			if (door == null) continue;

			if (Vector3.Distance(door.transform.position, transform.position) < 3f)
			{
				if (!door.isDoorOpened && (!telekinesisCooldowns.ContainsKey(door) || (Time.time - telekinesisCooldowns[door] >= 5f)))
				{
					if (door.isLocked)
					{
						door.UnlockDoorServerRpc();
					}
					AnimatedObjectTrigger component = door.GetComponent<AnimatedObjectTrigger>();
					if (component != null)
					{
						component.TriggerAnimationNonPlayer(true, true, false);
					}
					PlayDoorAnimationClientRpc();
					door.OpenDoorAsEnemyServerRpc();
					
					NetworkObject doorNetObj = door.GetComponent<NetworkObject>();
					if (doorNetObj != null)
					{
						SlamDoorSoundClientRpc(doorNetObj);
					}
					telekinesisCooldowns[door] = Time.time;
				}
			}
		}
	}

	[ClientRpc]
	public void SlamDoorSoundClientRpc(NetworkObjectReference doorRef)
	{
		if (doorRef.TryGet(out NetworkObject netObj))
		{
			DoorLock door = netObj.GetComponent<DoorLock>();
			if (door != null && doorTelekinesisClip != null)
			{
				AudioSource.PlayClipAtPoint(doorTelekinesisClip, door.transform.position, 1f);
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

        yield return new WaitForSeconds(1.5f);

        if (this.creatureAnimator != null)
        {
            this.creatureAnimator.SetTrigger(AnimBlastDoorDone);
        }
    }

    private void BoomboxCheck()
    {
        if (!this.IsOwner) return;
        if (currentLocalPhase != VecnaPhase.HauntChase || cursingPlayer == null) return;
        if (cachedBoomboxes == null) return;

        bool boomboxPlayingNearClone = false;
        rescuingBoombox = null;

        foreach (BoomboxItem boombox in cachedBoomboxes)
        {
            if (boombox != null && boombox.isPlayingMusic)
            {
                float distToClone = activeClone != null ? Vector3.Distance(activeClone.transform.position, boombox.transform.position) : 
                                   (localVictimClonePos != Vector3.zero ? Vector3.Distance(localVictimClonePos, boombox.transform.position) : 999f);
                float distToPlayer = cursingPlayer != null ? Vector3.Distance(cursingPlayer.transform.position, boombox.transform.position) : 999f;

                if (distToClone < boomboxRescueRadius || distToPlayer < boomboxRescueRadius)
                {
                    boomboxPlayingNearClone = true;
                    rescuingBoombox = boombox;
                    break;
                }
            }
        }

        if (boomboxPlayingNearClone && rescuingBoombox != null)
        {
            if (!isPortalOpen)
            {
                isPortalOpen = true;
                serverPortalPos = CalculatePortalPosition();
                if (portalManager != null)
                {
                    portalManager.TogglePortal(true, rescuingBoombox, serverPortalPos);
                }
            }
            
            float distToPortal = Vector3.Distance(cursingPlayer.transform.position, serverPortalPos);
            if (distToPortal < 3.0f)
            {
                //Debug.Log("VECNA: Victim reached the portal locally and escaped! Notifying Server.");
                NotifyVictimEscapeServerRpc();
            }
        }
        else
        {
            if (isPortalOpen)
            {
                isPortalOpen = false;
                if (portalManager != null)
                {
                    portalManager.TogglePortal(false, null, Vector3.zero);
                }
            }
        }
    }

    [ClientRpc]
    public void OpenEscapePortalClientRpc(NetworkObjectReference boomboxRef, Vector3 portalPos)
    {
        if (portalManager == null) return;
        
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

        if (cursingPlayer == GameNetworkManager.Instance.localPlayerController)
        {
            portalManager.TogglePortal(true, boombox, portalPos);
        }
    }

    [ClientRpc]
    public void CloseEscapePortalClientRpc()
    {
        if (portalManager != null)
        {
            portalManager.TogglePortal(false, null, Vector3.zero);
        }
    }
	
    public static System.Collections.Generic.List<VecnaAI> ActiveInstances = new System.Collections.Generic.List<VecnaAI>();


    public static bool IsPlayerInUpsideDown(PlayerControllerB player)
    {
        if (player == null) return false;

        foreach (VecnaAI vecna in ActiveInstances)
        {
            if (vecna != null && vecna.currentLocalPhase == VecnaPhase.HauntChase && vecna.cursingPlayer == player)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsVictimOrSpectatingVictim()
    {
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (cursingPlayer == null || localPlayer == null) return false;
        if (localPlayer == cursingPlayer) return true;
        if (localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript == cursingPlayer) return true;
        return false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void TeleportVecnaThroughPortalServerRpc(Vector3 position, bool inLair)
    {
        //Debug.Log($"VECNA: TeleportVecnaThroughPortalServerRpc called on server. Position: {position}, inLair: {inLair}");
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            bool warpSuccess = agent.Warp(position);
            //Debug.Log($"VECNA: ServerRpc agent.Warp result: {warpSuccess}");
        }
        serverPosition = position;
        transform.position = position;
        isInLair = inLair;
        isOutside = inLair;
        TeleportVecnaClientRpc(position, inLair);
    }

    [ClientRpc]
    public void TeleportVecnaClientRpc(Vector3 position, bool inLair)
    {
        serverPosition = position;
        transform.position = position;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(position);
        }
        isInLair = inLair;
        isOutside = inLair;

        if (!inLair)
        {
            StartCoroutine(ExitLairCutsceneRoutine(position));
        }
    }

    private System.Collections.IEnumerator ExitLairCutsceneRoutine(Vector3 exitPosition)
    {
        inSpecialAnimation = true;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(exitPosition);
        }
        transform.position = exitPosition;
        serverPosition = exitPosition;

        if (creatureAnimator != null)
        {
            creatureAnimator.SetTrigger("exitLair");
        }

        yield return new UnityEngine.WaitForSeconds(1f);

        VecnaLairPortal portal = activeEntrancePortal != null ? activeEntrancePortal : activeExitPortal;
        if (portal != null)
        {
            Transform emergeTransform = portal.transform.Find("Emerge");
            if (emergeTransform != null)
            {
                foreach (ParticleSystem ps in emergeTransform.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Play();
                }
            }
        }

        yield return new UnityEngine.WaitForSeconds(4f);
        inSpecialAnimation = false;
    }


    [ServerRpc(RequireOwnership = false)]
    public void TriggerCloneWakeUpServerRpc()
    {
        TriggerCloneWakeUpClientRpc();
    }

    [ClientRpc]
    public void TriggerCloneWakeUpClientRpc()
    {
        DormantVecnaClone clone = UnityEngine.Object.FindObjectOfType<DormantVecnaClone>();
        if (clone != null)
        {
            clone.DetachCloneLocally();
            clone.StartWakeUpRoutine();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void NotifyVictimEscapeServerRpc()
    {
        Debug.Log("VECNA: NotifyVictimEscapeServerRpc called on server.");
        ResetHaunt(repelledByMusic: false, playerKilled: false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestExecutionPullServerRpc(int playerId)
    {
        if (serverTelekinesisCooldownTimer > 0f) return;
        serverTelekinesisCooldownTimer = telekinCooldown;
        SyncAttackCooldownClientRpc(telekinCooldown);
        ExecutionPullClientRpc(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTelekinesisAttackServerRpc(int targetPlayerId, bool isOutside, bool isCloseRange)
    {
        if (serverTelekinesisCooldownTimer > 0f) return;
        serverTelekinesisCooldownTimer = telekinCooldown;
        SyncAttackCooldownClientRpc(telekinCooldown);
        TelekinesisAttackClientRpc(targetPlayerId, isOutside, isCloseRange);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestVisibleKillServerRpc(int playerId)
    {
        SyncVisibleKillClientRpc(playerId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestBlastServerRpc()
    {
        TriggerBlastClientRpc();
    }



    [ClientRpc]
    public void SyncAttackCooldownClientRpc(float cooldownTime)
    {
        telekinesisCooldown = cooldownTime;
    }

    [ClientRpc]
    public void TriggerBlastClientRpc()
    {
        if (isEnemyDead) return;
        if (teleBlastParticle != null)
        {
            teleBlastParticle.Play();
        }

        if (creatureAnimator != null)
        {
            creatureAnimator.SetTrigger("teleBlast");
        }

        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        if (localPlayer != null && !localPlayer.isPlayerDead && localPlayer.isPlayerControlled)
        {
            float dist = Vector3.Distance(transform.position, localPlayer.transform.position);
            if (dist <= 5f)
            {
                Vector3 pushDirection = (localPlayer.transform.position - transform.position).normalized;
                pushDirection.y = 0.2f;
                localPlayer.externalForceAutoFade += pushDirection * telePushKnockback;
                StartCoroutine(HitWallCheckRoutine(localPlayer, pushDirection));
            }
        }
    }

    public void WakeUpInLair(Vector3 spawnPos)
    {
        //Debug.Log($"VECNA: WakeUpInLair called. SpawnPos: {spawnPos}. IsServer: {this.IsServer}");
        if (this.IsServer)
        {
            if (agent != null) agent.enabled = false;
            
            serverPosition = spawnPos;
            transform.position = spawnPos;
            
            if (agent != null)
            {
                agent.enabled = true;
                bool warpSuccess = agent.Warp(spawnPos);
                //Debug.Log($"VECNA: WakeUpInLair agent.Warp result: {warpSuccess}");
            }
            
            isInLair = true;
            isOutside = true;
            ChangePhaseSafely(VecnaPhase.HuntEveryone);
            
            targetPlayer = null;
            float closestDist = 20f;
            foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
            {
                if (p == null || p.isPlayerDead || !p.isPlayerControlled) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d < closestDist)
                {
                    closestDist = d;
                    targetPlayer = p;
                }
            }

            if (targetPlayer == null)
            {
                Vector3 destPosition = portalSpawnNode != null ? portalSpawnNode.transform.position : (activeEntrancePortal != null ? activeEntrancePortal.transform.position : transform.position);
                //Debug.Log($"VECNA: Target player is null! Teleporting to entrance portal saved node: {destPosition}");
                if (agent != null) agent.Warp(destPosition);
                serverPosition = destPosition;
                transform.position = destPosition;
                isInLair = false;
                isOutside = false;
                base.TargetClosestPlayer();
            }

            SyncLairStateClientRpc(isInLair);
            SyncOutsideStateClientRpc(isOutside);
            SyncPositionToClients();
            WakeUpInLairClientRpc(serverPosition);
        }
    }

    [ClientRpc]
    private void WakeUpInLairClientRpc(Vector3 spawnPos)
    {
        //Debug.Log($"VECNA: WakeUpInLairClientRpc - syncing position. isInLair: {isInLair}, isOutside: {isOutside}");

        serverPosition = spawnPos;
        transform.position = spawnPos;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Warp(spawnPos);
            agent.speed = hauntChaseSpeed;
            movingTowardsTargetPlayer = true;
            base.TargetClosestPlayer();
            //Debug.Log($"VECNA: Lair targeting ready. targetPlayer: {(targetPlayer != null ? targetPlayer.playerUsername : "null")}");
        }
        else
        {
            //Debug.LogWarning("VECNA: Agent not ready in WakeUpInLairClientRpc - skipping local target check (this is normal for clients).");
        }
    }

    [ClientRpc]
    public void SyncLairStateClientRpc(bool inLair)
    {
        this.isInLair = inLair;
        if (inLair) this.isOutside = true;
        //Debug.Log($"[VECNA PORTAL SYSTEM] SyncLairStateClientRpc received. Setting isInLair = {inLair}, isOutside = {this.isOutside}");
    }

    [ClientRpc]
    public void SyncOutsideStateClientRpc(bool outside)
    {
        this.isOutside = outside;
        //Debug.Log($"[VECNA PORTAL SYSTEM] SyncOutsideStateClientRpc received. isOutside set to: {outside}");
    }

    [ClientRpc]
    public void SyncHauntChaseStartClientRpc(Vector3 spawnPos, bool isOutsideState)
    {
        //Debug.Log($"[VECNA PORTAL SYSTEM] SyncHauntChaseStartClientRpc received. Spawning at: {spawnPos}, isOutsideState: {isOutsideState}");

        this.isOutside = isOutsideState;
        this.isInLair = false;

        hasStartedChase = true;

        if (cursingPlayer != null)
        {
            localVictimClonePos = cursingPlayer.transform.position;
        }
        cloneWasTeleportedToShip = false;

        if (this.cursingLocalPlayer)
        {
            EntranceTeleport[] facilityExits = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
            foreach (EntranceTeleport exit in facilityExits)
            {
                InteractTrigger trigger = exit.GetComponent<InteractTrigger>();
                if (trigger != null) trigger.interactable = false;
            }
        }

        if (agent != null)
        {
            agent.enabled = false;
            transform.position = spawnPos;
            base.serverPosition = spawnPos;
            agent.enabled = true;
            agent.Warp(spawnPos);
            agent.speed = hauntChaseSpeed;
        }
        else
        {
            transform.position = spawnPos;
            base.serverPosition = spawnPos;
        }

        bool shouldSeeVecna = isHuntingEveryone || IsVictimOrSpectatingVictim();
        if (shouldSeeVecna)
        {
            EnableEnemyMesh(enable: true, overrideDoNotSet: true, tamperWithMeshes: true);
            enemyMeshEnabled = true;
            SFXVolumeLerpTo = 1f;
            StartAllSFX();
        }
        else
        {
            EnableEnemyMesh(enable: false, overrideDoNotSet: true, tamperWithMeshes: true);
            enemyMeshEnabled = false;
            SFXVolumeLerpTo = 0f;
        }
    }

    [ClientRpc]
    public void TeleportEnemyClientRpc(Vector3 pos, bool setOutside)
    {
        if (agent != null)
        {
            agent.enabled = false;
            transform.position = pos;
            base.serverPosition = pos;
            agent.enabled = true;
            agent.Warp(pos);
        }
        else
        {
            transform.position = pos;
            base.serverPosition = pos;
        }
        this.isOutside = setOutside;
        this.isInLair = false;
    }

    private EntranceTeleport GetClosestDoorToVecna()
    {
        if (allTeleports == null || allTeleports.Length == 0)
        {
            allTeleports = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
        }
        EntranceTeleport closestDoor = null;
        float minDist = float.MaxValue;
        foreach (EntranceTeleport door in allTeleports)
        {
            if (door != null && door.isEntranceToBuilding == this.isOutside)
            {
                float dist = Vector3.Distance(transform.position, door.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestDoor = door;
                }
            }
        }
        return closestDoor;
    }

    private EntranceTeleport GetCorrespondingDoor(EntranceTeleport chaserDoor)
    {
        if (chaserDoor == null) return null;
        if (allTeleports == null || allTeleports.Length == 0)
        {
            allTeleports = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>();
        }
        foreach (EntranceTeleport door in allTeleports)
        {
            if (door != null && door.entranceId == chaserDoor.entranceId && door.isEntranceToBuilding != chaserDoor.isEntranceToBuilding)
            {
                return door;
            }
        }
        return null;
    }

    private void DestroyActiveClone()
    {
        if (activeClone != null)
        {
            UnityEngine.Object.Destroy(activeClone);
            activeClone = null;
        }
    }

    public override void EnableEnemyMesh(bool enable, bool overrideDoNotSet = false, bool tamperWithMeshes = false)
    {
        bool shouldSeeVecna = isEnemyDead || isHuntingEveryone || (currentLocalPhase == VecnaPhase.HauntChase && IsVictimOrSpectatingVictim());
        enable = shouldSeeVecna;

        base.EnableEnemyMesh(enable, overrideDoNotSet, tamperWithMeshes);
        
        if (enable)
        {
            if (skinnedMeshRenderers != null)
            {
                foreach (var r in skinnedMeshRenderers)
                {
                    if (r != null)
                    {
                        r.enabled = true;
                        r.updateWhenOffscreen = true; 
                    }
                }
            }
            if (meshRenderers != null)
            {
                foreach (var r in meshRenderers) { if (r != null) r.enabled = true; }
            }
        }

        if (auraVisualEffect != null)
        {
            auraVisualEffect.enabled = enable;
        }

        foreach (AudioSource source in GetComponentsInChildren<AudioSource>(true))
        {
            if (source != null)
            {
                source.mute = !enable;
            }
        }
    }

	public void StopAllSFX()
	{
        if (chimechase != null)
        {
            chimechase.Stop();
        }
        if (breathingAudioSource != null)
        {
            breathingAudioSource.Stop();
        }
        if (footstepsAudio != null)
        {
            footstepsAudio.Stop();
        }
    }

	public void StartAllSFX()
	{
		if (!isHuntingEveryone && !IsVictimOrSpectatingVictim()) return;

		if (chimechase != null)
		{
			chimechase.Play();
		}
		if (breathingAudioSource != null && breathingClips != null && breathingClips.Length > 0)
		{
			breathingAudioSource.clip = breathingClips[UnityEngine.Random.Range(0, breathingClips.Length)];
			breathingAudioSource.Play();
		}
		if (footstepsAudio != null)
		{
			footstepsAudio.Play();
		}
	}

    private void EnableVecnaPullAnimator(PlayerControllerB player)
    {
        if (player == null)
        {
            //Debug.LogWarning("[VecnaPullDebug] EnableVecnaPullAnimator failed: player is null.");
            return;
        }

        //Debug.Log($"[VecnaPullDebug] Vecna animators in Inspector - Local: {vecnaPullLocalAnimator != null}, Remote: {vecnaPullRemoteAnimator != null}");

        if (!_SAVED_ANIMATORS.ContainsKey(player.playerClientId))
        {
            //Debug.Log($"[VecnaPullDebug] Saving original animator for client {player.playerClientId}: {(player.playerBodyAnimator.runtimeAnimatorController != null ? player.playerBodyAnimator.runtimeAnimatorController.name : "null")}");
            _SAVED_ANIMATORS[player.playerClientId] = player.playerBodyAnimator.runtimeAnimatorController;
        }

        SaveAnimatorState(player.playerBodyAnimator);

        if (player == GameNetworkManager.Instance.localPlayerController)
        {
            //Debug.Log("[VecnaPullDebug] Applying LOCAL pull animator.");
            player.playerBodyAnimator.runtimeAnimatorController = vecnaPullLocalAnimator;
        }
        else
        {
            //Debug.Log("[VecnaPullDebug] Applying REMOTE pull animator.");
            player.playerBodyAnimator.runtimeAnimatorController = vecnaPullRemoteAnimator;
        }

        //Debug.Log("[VecnaPullDebug] Rebinding and Updating animator...");
        player.playerBodyAnimator.Rebind();
        player.playerBodyAnimator.Update(0f);
        
        player.playerBodyAnimator.SetBool("isBeingPulled", true);
        
        //Debug.Log("[VecnaPullDebug] EnableVecnaPullAnimator finished successfully. isBeingPulled set to true.");
    }

    private void DisableVecnaPullAnimator(PlayerControllerB player)
    {
        //Debug.Log($"[VecnaPullDebug] DisableVecnaPullAnimator called for client {(player != null ? player.playerClientId.ToString() : "null")}");
        if (player == null)
        {
            //Debug.LogWarning("[VecnaPullDebug] DisableVecnaPullAnimator failed: player is null.");
            return;
        }

        SaveAnimatorState(player.playerBodyAnimator);

        if (_SAVED_ANIMATORS.TryGetValue(player.playerClientId, out var original))
        {
            //Debug.Log($"[VecnaPullDebug] Restoring original animator for client {player.playerClientId}: {(original != null ? original.name : "null")}");
            player.playerBodyAnimator.runtimeAnimatorController = original;
            _SAVED_ANIMATORS.Remove(player.playerClientId);
        }
        else
        {
            //Debug.LogError($"[VecnaPullDebug] FATAL: Could not find original animator in dictionary for client {player.playerClientId}!");
        }

        //Debug.Log("[VecnaPullDebug] Rebinding and Updating original animator...");
        player.playerBodyAnimator.Rebind();
        player.playerBodyAnimator.Update(0f);

        RestoreAnimatorState(player.playerBodyAnimator, false);
        
        player.playerBodyAnimator.SetBool("isBeingPulled", false);
        //Debug.Log("[VecnaPullDebug] DisableVecnaPullAnimator finished successfully. isBeingPulled set to false.");
    }

    private void SaveAnimatorState(Animator anim)
    {
        _savedState = anim.GetCurrentAnimatorStateInfo(0);
        _savedNormalizedTime = _savedState.normalizedTime;

        _savedCrouching = anim.GetBool("crouching");
        _savedWalking = anim.GetBool("Walking");
        _savedJumping = anim.GetBool("Jumping");
        _savedSprinting = anim.GetBool("Sprinting");

        //Debug.Log($"[VecnaPullDebug] Saved State -> Hash: {_savedState.fullPathHash}, Time: {_savedNormalizedTime}, Walk: {_savedWalking}, Crouch: {_savedCrouching}");
    }

    private void RestoreAnimatorState(Animator anim, bool isEquipping)
    {
        //Debug.Log($"[VecnaPullDebug] Restoring Animator State. isEquipping: {isEquipping}");
        anim.Play(_savedState.fullPathHash, 0, _savedNormalizedTime);
        anim.SetBool("crouching", _savedCrouching);
        anim.SetBool("Walking", _savedWalking);
        anim.SetBool("Jumping", _savedJumping);
        anim.SetBool("Sprinting", _savedSprinting);
    }

    public VehicleController GetPlayerVehicle(PlayerControllerB player)
    {
        if (player == null) return null;

        VehicleController parentVehicle = player.GetComponentInParent<VehicleController>();
        if (parentVehicle != null) return parentVehicle;

        VehicleController[] allVehicles = UnityEngine.Object.FindObjectsOfType<VehicleController>();
        for (int i = 0; i < allVehicles.Length; i++)
        {
            VehicleController v = allVehicles[i];
            if (v != null && (v.currentDriver == player || v.currentPassenger == player))
            {
                return v;
            }
        }

        return null;
    }

    public bool IsMusicPlayingNearVictim()
    {
        if (cursingPlayer == null || cachedBoomboxes == null)
        {
            return false;
        }
        float num = boomboxRescueRadius * boomboxRescueRadius;
        for (int i = 0; i < cachedBoomboxes.Length; i++)
        {
            BoomboxItem boombox = cachedBoomboxes[i];
            if (boombox != null && boombox.isPlayingMusic)
            {
                float sqrMagnitude = (boombox.transform.position - cursingPlayer.transform.position).sqrMagnitude;
                if (sqrMagnitude <= num)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void HandleBreathing()
    {
        if (breathingAudioSource == null) return;

        if (breathingAudioSource.outputAudioMixerGroup == null && SoundManager.Instance != null && SoundManager.Instance.diageticMixer != null)
        {
            breathingAudioSource.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("Master")[0];
        }

        bool shouldBreathe = !isEnemyDead && (isHuntingEveryone || (currentLocalPhase == VecnaPhase.HauntChase && IsVictimOrSpectatingVictim()));
        if (shouldBreathe)
        {
            if (breathingClips != null && breathingClips.Length > 0 && !breathingAudioSource.isPlaying)
            {
                breathingAudioSource.clip = breathingClips[UnityEngine.Random.Range(0, breathingClips.Length)];
                breathingAudioSource.Play();
            }
        }
        else
        {
            if (breathingAudioSource.isPlaying)
            {
                breathingAudioSource.Stop();
            }
        }
    }

    [ServerRpc]
    public void RequestClockSpawnFlickerServerRpc()
    {
        FlickerLightsForAllClientsClientRpc();
    }

    [ClientRpc]
    public void FlickerLightsForAllClientsClientRpc()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.FlickerLights(flickerFlashlights: true, disableFlashlights: true);
        }
    }

    private IEnumerator FlickerPoweredLightsNearClone(Vector3 clonePos, bool flickerFlashlights = false, bool disableFlashlights = false)
    {
        if (cursingPlayer == GameNetworkManager.Instance.localPlayerController) yield break;
        if (clonePos == Vector3.zero) yield break;

        List<FlashlightItem> flashlightsNearClone = new List<FlashlightItem>();
        if (flickerFlashlights)
        {
            FlashlightItem[] allFlashlights = UnityEngine.Object.FindObjectsOfType<FlashlightItem>();
            if (allFlashlights != null)
            {
                for (int i = 0; i < allFlashlights.Length; i++)
                {
                    FlashlightItem fl = allFlashlights[i];
                    if (fl != null && fl.playerHeldBy != null && fl.playerHeldBy != cursingPlayer && Vector3.Distance(fl.playerHeldBy.transform.position, clonePos) <= 15f)
                    {
                        flashlightsNearClone.Add(fl);
                    }
                }
            }

            for (int i = 0; i < flashlightsNearClone.Count; i++)
            {
                FlashlightItem fl = flashlightsNearClone[i];
                if (fl != null)
                {
                    if (fl.flashlightAudio != null && fl.flashlightFlicker != null)
                    {
                        fl.flashlightAudio.PlayOneShot(fl.flashlightFlicker);
                        WalkieTalkie.TransmitOneShotAudio(fl.flashlightAudio, fl.flashlightFlicker, 0.8f);
                    }
                    if (disableFlashlights && fl.playerHeldBy != null && fl.playerHeldBy.isInsideFactory)
                    {
                        fl.flashlightInterferenceLevel = 2;
                    }
                }
            }
        }

        List<Animator> lightsNearClone = new List<Animator>();
        if (RoundManager.Instance != null && RoundManager.Instance.allPoweredLightsAnimators != null)
        {
            for (int i = 0; i < RoundManager.Instance.allPoweredLightsAnimators.Count; i++)
            {
                Animator animator = RoundManager.Instance.allPoweredLightsAnimators[i];
                if (animator != null && Vector3.Distance(animator.transform.position, clonePos) <= 15f)
                {
                    lightsNearClone.Add(animator);
                }
            }
        }

        if (lightsNearClone.Count > 0)
        {
            int loopCount = 0;
            int b = 4;
            while (b > 0 && b != 0)
            {
                int limit = lightsNearClone.Count / b;
                for (int j = loopCount; j < limit; j++)
                {
                    if (j < lightsNearClone.Count && lightsNearClone[j] != null)
                    {
                        lightsNearClone[j].SetTrigger("Flicker");
                    }
                    loopCount++;
                }
                yield return new WaitForSeconds(0.05f);
                b--;
            }
        }

        if (!flickerFlashlights)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < flashlightsNearClone.Count; i++)
        {
            FlashlightItem fl = flashlightsNearClone[i];
            if (fl != null)
            {
                fl.flashlightInterferenceLevel = 0;
            }
        }
    }

    private void BeginPryOpenDoor()
    {
        SetPryingDoorClientRpc(true);
    }

    private void FinishPryOpenDoor()
    {
        SetPryingDoorClientRpc(false);
    }

    [ClientRpc]
    public void SetPryingDoorClientRpc(bool state)
    {
        isPryingDoor = state;
        inSpecialAnimation = state;
        if (agent != null) agent.enabled = !state;
        
        if (state)
        {
            creatureAnimator.SetBool("stopWalking", true);
            pryingDoorAnimTime = 0f;
            hasTriggeredThrowAnim = false;
            if (shipDoor != null)
            {
                shipDoor.shipDoorsAnimator.SetBool("PryingOpenDoor", true);
                shipDoor.shipDoorsAnimator.SetFloat("pryOpenDoor", 0f);
            }
            if (teleDoorParticle != null)
            {
                teleDoorParticle.Play();
            }
            if (breakAndEnter != null && creatureVoice != null)
            {
                creatureVoice.PlayOneShot(breakAndEnter);
                WalkieTalkie.TransmitOneShotAudio(creatureVoice, breakAndEnter);
            }
            else if (shipAlarm != null)
            {
                StartOfRound.Instance.speakerAudioSource.PlayOneShot(shipAlarm);
                WalkieTalkie.TransmitOneShotAudio(StartOfRound.Instance.speakerAudioSource, shipAlarm);
            }
            if (Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, transform.position) < 18f)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            }
        }
        else
        {
            if (creatureAnimator != null)
            {
                creatureAnimator.SetBool("stopWalking", false);
            }
            if (shipDoor != null)
            {
                shipDoor.shipDoorsAnimator.SetBool("Closed", false);
                shipDoor.shipDoorsAnimator.SetBool("PryingOpenDoor", false);
                StartOfRound.Instance.SetShipDoorsClosed(false);
                StartOfRound.Instance.SetShipDoorsOverheatLocalClient();
                shipDoor.doorPower = 0f;
            }
        }
    }

    public bool BreakIntoShip()
    {
        if (shipDoor == null) return false;
        
        if (isPryingDoor)
        {
            if (pryingDoorAnimTime >= 1f)
            {
                FinishPryOpenDoor();
            }
            return true;
        }
        
        if (StartOfRound.Instance.hangarDoorsClosed && targetPlayer != null && targetPlayer.isInHangarShipRoom && Vector3.Distance(transform.position, shipDoor.outsideDoorPoint.position) < 4f)
        {
            foreach (VecnaAI otherVecna in VecnaAI.ActiveInstances)
            {
                if (otherVecna != null && otherVecna != this && otherVecna.isPryingDoor)
                {
                    return false;
                }
            }
            BeginPryOpenDoor();
            return true;
        }
        return false;
    }
}