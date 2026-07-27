using UnityEngine;
using GameNetcodeStuff;
using Unity.Netcode;

namespace Vecna
{
    public class VecnaLairPortal : MonoBehaviour
    {
        public bool isEntrance; // true if Facility -> Lair, false if Lair -> Facility yess
        public Transform teleportDestination;
        public AudioSource portalAudio;
        public AudioClip teleportClip;

        void Start()
        {
            if (isEntrance)
            {
                VecnaAI.activeEntrancePortal = this;
                Debug.Log("VecnaLairPortal: Registered activeEntrancePortal");
            }
            else
            {
                VecnaAI.activeExitPortal = this;
                Debug.Log("VecnaLairPortal: Registered activeExitPortal");
            }
        }

        // To be called by the InteractTrigger unity event
        public void TeleportLocalPlayer()
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            if (player != null)
            {
                TeleportPlayer(player);
            }
        }

        // To be called if the event specifically passes PlayerControllerB
        public void TeleportPlayer(PlayerControllerB player)
        {
            if (player == null) return;

            // Determine if the player is currently in the Lair using the LairDetect trigger list
            bool playerInLair = false;
            foreach (var vecna in VecnaAI.ActiveInstances)
            {
                if (vecna != null)
                {
                    playerInLair = vecna.IsPlayerInLair(player);
                    break;
                }
            }
            
            // Coordinate fallback if Vecna instance is not ready/active
            if (VecnaAI.ActiveInstances.Count == 0 || VecnaAI.ActiveInstances[0] == null)
            {
                playerInLair = player.transform.position.x > 1000f && player.transform.position.z > 1000f;
            }

            Transform dest = playerInLair ? 
                (VecnaAI.activeEntrancePortal != null ? VecnaAI.activeEntrancePortal.transform : null) :
                (VecnaAI.activeExitPortal != null ? VecnaAI.activeExitPortal.transform : null);

            if (dest == null)
            {
                VecnaLairPortal[] portals = FindObjectsOfType<VecnaLairPortal>(true);
                //Debug.Log($"VecnaLairPortal: Destination portal is null. Fallback searching through {portals.Length} portals...");
                foreach (var p in portals)
                {
                    if (p != null)
                    {
                        bool portalInLair = p.transform.position.x > 1000f && p.transform.position.z > 1000f;
                        //Debug.Log($"VecnaLairPortal: Found portal '{p.gameObject.name}' at position {p.transform.position}. portalInLair: {portalInLair}, isEntrance: {p.isEntrance}");
                        
                        if (portalInLair != playerInLair)
                        {
                            dest = p.transform;
                            break;
                        }
                    }
                }

                if (dest == null)
                {
                    foreach (var p in portals)
                    {
                        if (p != null && p.isEntrance == playerInLair)
                        {
                            dest = p.transform;
                            //Debug.Log($"VecnaLairPortal: Coordinate check failed, fell back to isEntrance flag. Using portal '{p.gameObject.name}'");
                            break;
                        }
                    }
                }
            }

            if (dest == null)
            {
                //Debug.LogError($"VecnaLairPortal: TeleportPlayer failed because destination portal is null! playerInLair: {playerInLair}");
                return;
            }

            bool goingToFacility = !(dest.position.x > 1000f && dest.position.z > 1000f);

            //Debug.Log($"VecnaLairPortal: Teleporting player {player.playerUsername} to {dest.position}. Setting isInsideFactory to {goingToFacility}");

            if (portalAudio != null && teleportClip != null)
            {
                portalAudio.PlayOneShot(teleportClip);
            }

            player.TeleportPlayer(dest.position);

            // Update factory state robustly based on destination coordinatee
            player.isInsideFactory = goingToFacility;

            if (!goingToFacility)
            {
                VecnaAI.isPlayerInLair = true;
            }
            else
            {
                // Check if any other player is still in the Lair
                bool anyoneLeft = false;
                foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                {
                    if (p != player && !p.isPlayerDead && !p.isInsideFactory && p.transform.position.x > 1000f && p.transform.position.z > 1000f)
                    {
                        anyoneLeft = true;
                        break;
                    }
                }
                if (!anyoneLeft)
                {
                    VecnaAI.isPlayerInLair = false;
                }
            }

            // Update local culling masks so they can see the Lair
            if (player == GameNetworkManager.Instance.localPlayerController)
            {
                Camera mainCam = player.gameplayCamera;
                if (mainCam != null)
                {
                    if (!goingToFacility)
                    {
                        mainCam.cullingMask |= (1 << VecnaAI.UPSIDE_DOWN_LAYER);
                    }
                    else
                    {
                        mainCam.cullingMask &= ~(1 << VecnaAI.UPSIDE_DOWN_LAYER);
                    }
                }
            }
        }

        // For Vecna to teleport through
        public void TeleportVecna(VecnaAI vecna)
        {
            if (vecna == null) return;
            
            bool vecnaInLair = vecna.transform.position.x > 1000f && vecna.transform.position.z > 1000f;
            Transform dest = vecnaInLair ? 
                (VecnaAI.activeEntrancePortal != null ? VecnaAI.activeEntrancePortal.transform : null) :
                (VecnaAI.activeExitPortal != null ? VecnaAI.activeExitPortal.transform : null);

            if (dest == null)
            {
                VecnaLairPortal[] portals = FindObjectsOfType<VecnaLairPortal>(true);
                foreach (var p in portals)
                {
                    if (p != null)
                    {
                        bool portalInLair = p.transform.position.x > 1000f && p.transform.position.z > 1000f;
                        if (portalInLair != vecnaInLair)
                        {
                            dest = p.transform;
                            break;
                        }
                    }
                }

                if (dest == null)
                {
                    foreach (var p in portals)
                    {
                        if (p != null && p.isEntrance == vecnaInLair)
                        {
                            dest = p.transform;
                            break;
                        }
                    }
                }
            }

            if (dest == null) return;

            Vector3 destPosition = dest.position;
            bool goingToLair = destPosition.x > 1000f && destPosition.z > 1000f;

            //Debug.Log($"VecnaLairPortal: Teleporting Vecna to {destPosition}.");
            if (vecna.agent != null && vecna.agent.isActiveAndEnabled && vecna.agent.isOnNavMesh)
            {
                vecna.agent.Warp(destPosition);
            }
            
            if (vecna.IsServer)
            {
                vecna.serverPosition = destPosition;
                vecna.transform.position = destPosition;
                vecna.isInLair = goingToLair;
                vecna.isOutside = goingToLair;
                vecna.TeleportVecnaClientRpc(destPosition, goingToLair);
            }
            else
            {
                vecna.isInLair = goingToLair;
                vecna.isOutside = goingToLair;
                vecna.TeleportVecnaThroughPortalServerRpc(destPosition, goingToLair);
            }
        }
    }
}
