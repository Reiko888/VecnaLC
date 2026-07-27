using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vecna
{
    public static class VecnaVFXHelper
    {
        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == child) return "";
            string path = child.name;
            Transform parent = child.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static void SetAllChildrenLayer(Transform trans, int layer)
        {
            if (trans == null) return;
            trans.gameObject.layer = layer;
            foreach (Transform child in trans)
            {
                SetAllChildrenLayer(child, layer);
            }
        }

        public static void DressCloneLikePlayer(GameObject clone, PlayerControllerB victim)
        {
            if (clone == null || victim == null) return;

            try
            {
                SkinnedMeshRenderer cloneBaseRenderer = clone.GetComponentInChildren<SkinnedMeshRenderer>();
                if (cloneBaseRenderer != null && victim.thisPlayerModel != null)
                {
                    cloneBaseRenderer.sharedMesh = victim.thisPlayerModel.sharedMesh;
                    cloneBaseRenderer.sharedMaterials = victim.thisPlayerModel.sharedMaterials;
                    cloneBaseRenderer.enabled = victim.thisPlayerModel.enabled;
                }

                List<GameObject> clonedObjects = new List<GameObject>();

                ApplyMoreCompanyCosmetics(clone, victim, clonedObjects);

                ApplyModelReplacement(clone, victim, clonedObjects);

                foreach (GameObject clonedObj in clonedObjects)
                {
                    if (clonedObj == null) continue;
                    SetAllChildrenLayer(clonedObj.transform, 0);
                    foreach (Renderer r in clonedObj.GetComponentsInChildren<Renderer>(true))
                    {
                        r.enabled = true;
                        r.forceRenderingOff = false;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                        r.receiveShadows = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"VECNA: Exception in DressCloneLikePlayer: {ex}");
            }
        }


        //compat
        private static void ApplyMoreCompanyCosmetics(GameObject clone, PlayerControllerB victim, List<GameObject> clonedObjects)
        {
            try
            {
                var moreCompanyAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "MoreCompany");
                if (moreCompanyAssembly == null) return;

                Transform cloneMetarig = clone.transform.Find("metarig");
                if (cloneMetarig == null)
                {
                    foreach (Transform t in clone.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "metarig")
                        {
                            cloneMetarig = t;
                            break;
                        }
                    }
                }
                if (cloneMetarig == null) cloneMetarig = clone.transform;

                var patchesType = moreCompanyAssembly.GetType("MoreCompany.CosmeticPatches");
                if (patchesType != null)
                {
                    var cloneMethod = patchesType.GetMethod("CloneCosmeticsToNonPlayer", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (cloneMethod != null)
                    {
                        object parentTypeVal = System.Enum.ToObject(moreCompanyAssembly.GetType("MoreCompany.Cosmetics.ParentType"), 3);
                        cloneMethod.Invoke(null, new object[] { parentTypeVal, cloneMetarig, (int)victim.playerClientId, false });
                        //Debug.Log($"VECNA: MoreCompany cosmetics applied to clone via reflection for player {victim.playerUsername}");

                        Component cosmeticApplication = cloneMetarig.GetComponent("MoreCompany.Cosmetics.CosmeticApplication")
                                                     ?? cloneMetarig.GetComponent("CosmeticApplication");
                        if (cosmeticApplication != null)
                        {
                            var spawnedCosmeticsField = cosmeticApplication.GetType().GetField("spawnedCosmetics", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (spawnedCosmeticsField != null)
                            {
                                var list = spawnedCosmeticsField.GetValue(cosmeticApplication) as System.Collections.IEnumerable;
                                if (list != null)
                                {
                                    foreach (object item in list)
                                    {
                                        if (item is Component compItem && compItem != null)
                                        {
                                            clonedObjects.Add(compItem.gameObject);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"VECNA: Error applying MoreCompany cosmetics to clone: {ex}");
            }
        }

        private static bool IsSubclassOfTypeName(System.Type type, string typeName)
        {
            while (type != null)
            {
                if (type.Name == typeName || type.FullName == typeName)
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        private static bool IsDescendantOf(Transform parent, Transform child)
        {
            Transform p = child;
            while (p != null)
            {
                if (p == parent) return true;
                p = p.parent;
            }
            return false;
        }
        //compat
        private static void ApplyModelReplacement(GameObject clone, PlayerControllerB victim, List<GameObject> clonedObjects)
        {
            try
            {
                var mrAssembly = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "ModelReplacementAPI");
                if (mrAssembly == null) return;

                MonoBehaviour bodyReplacement = null;
                foreach (var comp in victim.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (comp != null && IsSubclassOfTypeName(comp.GetType(), "BodyReplacementBase"))
                    {
                        bodyReplacement = comp;
                        break;
                    }
                }

                if (bodyReplacement == null) return;

                GameObject replacementModel = null;
                foreach (var f in bodyReplacement.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    if (f.FieldType == typeof(GameObject) && (f.Name.Contains("replacement") || f.Name.Contains("model") || f.Name.Contains("Replacement") || f.Name.Contains("Model")))
                    {
                        replacementModel = f.GetValue(bodyReplacement) as GameObject;
                        if (replacementModel != null) break;
                    }
                }

                if (replacementModel == null)
                {
                    foreach (var p in bodyReplacement.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                    {
                        if (p.PropertyType == typeof(GameObject) && (p.Name.Contains("replacement") || p.Name.Contains("model") || p.Name.Contains("Replacement") || p.Name.Contains("Model")))
                        {
                            replacementModel = p.GetValue(bodyReplacement) as GameObject;
                            if (replacementModel != null) break;
                        }
                    }
                }

                if (replacementModel != null)
                {
                    GameObject clonedReplacement = GameObject.Instantiate(replacementModel, clone.transform);
                    clonedReplacement.transform.localPosition = replacementModel.transform.localPosition;
                    clonedReplacement.transform.localRotation = replacementModel.transform.localRotation;
                    clonedReplacement.transform.localScale = replacementModel.transform.localScale;

                    clonedObjects.Add(clonedReplacement);

                    foreach (var netObj in clonedReplacement.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true)) UnityEngine.Object.DestroyImmediate(netObj);
                    foreach (var coll in clonedReplacement.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(coll);

                    var updaterType = mrAssembly.GetType("ModelReplacement.AvatarBodyUpdater.AvatarUpdater") 
                                   ?? mrAssembly.GetType("ModelReplacement.AvatarUpdater");
                    if (updaterType != null)
                    {
                        var binder = clone.AddComponent<CloneModelBinder>();
                        binder.Initialize(clone, clonedReplacement, updaterType);
                    }

                    foreach (SkinnedMeshRenderer smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (smr != null && !smr.transform.IsChildOf(clonedReplacement.transform))
                        {
                            smr.enabled = false;
                        }
                    }
                    foreach (MeshRenderer mr in clone.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (mr != null && !mr.transform.IsChildOf(clonedReplacement.transform))
                        {
                            mr.enabled = false;
                        }
                    }
                    
                    //Debug.Log("VECNA: ModelReplacementAPI custom model cloned and mapped via reflection using AvatarUpdater!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"VECNA: Error applying ModelReplacementAPI to clone: {ex}");
            }
        }

        public static void TogglePlayerThirdPersonModel(VecnaAI vecnaAI, PlayerControllerB player, bool isVisible)
        {
            if (player == null) return;

            PlayerControllerB localClient = GameNetworkManager.Instance.localPlayerController;
            bool isLocalPlayer = (localClient == player) ||
                                 (localClient.isPlayerDead && localClient.spectatedPlayerScript == player);

            if (isVisible)
            {
                player.thisPlayerModel.enabled = true;
                if (!isLocalPlayer)
                {
                    player.thisPlayerModelLOD1.enabled = true;
                    player.thisPlayerModelLOD2.enabled = true;
                    foreach (AudioSource audio in player.gameObject.GetComponentsInChildren<AudioSource>(true))
                    {
                        audio.mute = false;
                    }
                }

                if (player.usernameCanvas != null) player.usernameCanvas.gameObject.SetActive(!isLocalPlayer);
                if (player.usernameBillboardText != null) player.usernameBillboardText.enabled = !isLocalPlayer;
            }
            else
            {
                player.thisPlayerModel.enabled = false;
                player.thisPlayerModelLOD1.enabled = false;
                player.thisPlayerModelLOD2.enabled = false;
                foreach (AudioSource audio in player.gameObject.GetComponentsInChildren<AudioSource>(true))
                {
                    audio.mute = true;
                }

                if (player.usernameCanvas != null) player.usernameCanvas.gameObject.SetActive(false);
                if (player.usernameBillboardText != null) player.usernameBillboardText.enabled = false;
            }
        }

        public static void ToggleCursedDimensionVFX(VecnaAI vecnaAI, bool isVisible)
        {
            if (vecnaAI == null) return;

            GameObject playerObj = GameNetworkManager.Instance.localPlayerController.gameObject;
            Camera mainCam = GameNetworkManager.Instance.localPlayerController.gameplayCamera;

            bool isLocalPlayerVictim = (vecnaAI.cursingPlayer == GameNetworkManager.Instance.localPlayerController);

            if (!isVisible)
            {
                if (vecnaAI.storedCameraMask == -1)
                {
                    vecnaAI.storedCameraMask = mainCam.cullingMask;
                    vecnaAI.storedCamera = mainCam;
                }

                if (isLocalPlayerVictim)
                {
                    mainCam.cullingMask |= (1 << VecnaAI.UPSIDE_DOWN_LAYER);
                }

                foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                {
                    if (p != null && p.isPlayerControlled)
                    {
                        bool shouldHide = false;
                        if (isLocalPlayerVictim && p != vecnaAI.cursingPlayer) shouldHide = true;
                        if (!isLocalPlayerVictim && p == vecnaAI.cursingPlayer) shouldHide = true;

                        if (shouldHide)
                        {
                            foreach (AudioSource audio in p.gameObject.GetComponentsInChildren<AudioSource>(true)) audio.mute = true;
                            if (p.usernameCanvas != null) p.usernameCanvas.gameObject.SetActive(false);
                            if (p.usernameBillboardText != null) p.usernameBillboardText.enabled = false;

                            if (isLocalPlayerVictim && vecnaAI.cursingPlayer.thisController != null && p.thisController != null)
                            {
                                Physics.IgnoreCollision(vecnaAI.cursingPlayer.thisController, p.thisController, true);
                                Physics.IgnoreCollision(vecnaAI.cursingPlayer.playerCollider, p.playerCollider, true);
                            }
                        }
                    }
                }
            }
            else
            {
                if (vecnaAI.storedCameraMask != -1 && mainCam != null)
                {
                    mainCam.cullingMask = vecnaAI.storedCameraMask;
                    vecnaAI.storedCameraMask = -1;
                    vecnaAI.storedCamera = null;
                }

                foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                {
                    if (p != null && p.isPlayerControlled)
                    {
                        foreach (AudioSource audio in p.gameObject.GetComponentsInChildren<AudioSource>(true))
                        {
                            audio.mute = false;
                        }

                        if (p.usernameCanvas != null) p.usernameCanvas.gameObject.SetActive(true);
                        if (p.usernameBillboardText != null) p.usernameBillboardText.enabled = true;

                        if (vecnaAI.cursingPlayer != null && vecnaAI.cursingPlayer.thisController != null && p.thisController != null)
                        {
                            Physics.IgnoreCollision(vecnaAI.cursingPlayer.thisController, p.thisController, false);
                            Physics.IgnoreCollision(vecnaAI.cursingPlayer.playerCollider, p.playerCollider, false);
                        }
                    }
                }
            }
        }
    }

    public class CloneModelBinder : MonoBehaviour
    {
        private object avatarUpdaterInstance;
        private System.Reflection.MethodInfo updateMethod;

        public void Initialize(GameObject clone, GameObject replacementModel, System.Type avatarUpdaterType)
        {
            try
            {
                avatarUpdaterInstance = System.Activator.CreateInstance(avatarUpdaterType);
                
                var assignMethod = avatarUpdaterType.GetMethod("AssignModelReplacement", new System.Type[] { typeof(GameObject), typeof(GameObject) });
                if (assignMethod != null)
                {
                    assignMethod.Invoke(avatarUpdaterInstance, new object[] { clone, replacementModel });
                }

                updateMethod = avatarUpdaterType.GetMethod("Update", System.Type.EmptyTypes);
                //Debug.Log("VECNA: CloneModelBinder successfully initialized AvatarUpdater via reflection!");
            }
            catch (System.Exception ex)
            {
                //Debug.LogError($"VECNA: Error initializing AvatarUpdater in CloneModelBinder: {ex}");
            }
        }

        void LateUpdate()
        {
            try
            {
                if (avatarUpdaterInstance != null && updateMethod != null)
                {
                    updateMethod.Invoke(avatarUpdaterInstance, null);
                }
            }
            catch (System.Exception ex)
            {
                updateMethod = null;
                //Debug.LogError($"VECNA: Error updating AvatarUpdater: {ex}");
            }
        }
    }
}