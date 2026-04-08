﻿﻿﻿using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Vecna
{
    public static class VecnaVFXHelper
    {
        private static List<ParticleSystem> mindFlayerDustPool = new List<ParticleSystem>();
        private static List<ParticleSystem> massiveBloodSplashPool = new List<ParticleSystem>();
        private static List<ParticleSystem> smallBloodSplashPool = new List<ParticleSystem>();

        private class PooledParticle : MonoBehaviour
        {
            public float lifeTime = 2f;
            private WaitForSeconds waitInstruction;

            private void OnEnable() 
            { 
                if (waitInstruction == null) waitInstruction = new WaitForSeconds(lifeTime);
                StartCoroutine(DisableAfterTime()); 
            }

            private IEnumerator DisableAfterTime()
            {
                yield return waitInstruction;
                gameObject.SetActive(false);
            }
        }

        public static void PrewarmPools()
        {
            mindFlayerDustPool.RemoveAll(p => p == null);
            massiveBloodSplashPool.RemoveAll(p => p == null);
            smallBloodSplashPool.RemoveAll(p => p == null);

            if (mindFlayerDustPool.Count == 0)
            {
                for (int i = 0; i < 2; i++) mindFlayerDustPool.Add(SetupMindFlayerDust());
                for (int i = 0; i < 3; i++) massiveBloodSplashPool.Add(SetupMassiveBloodSplash());
                for (int i = 0; i < 8; i++) smallBloodSplashPool.Add(SetupSmallBloodSplash());
            }
        }

        private static ParticleSystem GetFromPool(List<ParticleSystem> pool, Func<ParticleSystem> factoryMethod)
        {
            pool.RemoveAll(p => p == null);

            foreach (var ps in pool)
            {
                if (!ps.gameObject.activeInHierarchy) return ps;
            }

            ParticleSystem newPs = factoryMethod();
            pool.Add(newPs);
            return newPs;
        }

        public static void CreateMindFlayerDust(Vector3 pos)
        {
            ParticleSystem ps = GetFromPool(mindFlayerDustPool, SetupMindFlayerDust);
            ps.gameObject.transform.position = pos + Vector3.up * 2f;
            ps.gameObject.SetActive(true);
            ps.Play();
        }

        private static ParticleSystem SetupMindFlayerDust()
        {
            GameObject dustObj = new GameObject("MindFlayerDust_Pooled");
            dustObj.SetActive(false);
            dustObj.AddComponent<PooledParticle>().lifeTime = 6f;

            ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = dustObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));

            var main = ps.main;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.01f, 0.01f, 0.02f, 0.9f), new Color(0.05f, 0.05f, 0.08f, 0.7f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.gravityModifier = -0.05f; 
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 400, 600) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.5f, 4.0f, 1.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 1.5f;
            noise.scrollSpeed = 2.0f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 0.8f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

            return ps;
        }

        public static void CreateMassiveBloodSplash(Vector3 pos, bool isBody = false)
        {
            ParticleSystem ps = GetFromPool(massiveBloodSplashPool, SetupMassiveBloodSplash);
            ps.gameObject.transform.position = pos + (isBody ? Vector3.up * 0.5f : Vector3.up * 1.5f);

            var shape = ps.shape;
            if (isBody)
            {
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.8f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.radius = 0.5f;
                shape.angle = 30f;
            }

            ps.gameObject.SetActive(true);
            ps.Play();
        }

        private static ParticleSystem SetupMassiveBloodSplash()
        {
            GameObject bloodObj = new GameObject("VecnaMassiveBloodSplash_Pooled");
            bloodObj.SetActive(false);
            bloodObj.AddComponent<PooledParticle>().lifeTime = 4f;

            ParticleSystem ps = bloodObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = bloodObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));

            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.cameraVelocityScale = 0f;
            psr.velocityScale = 0.08f;
            psr.lengthScale = 2f;

            var main = ps.main;
            main.duration = 2f;
            main.loop = false;
            main.gravityModifier = 1.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0f, 0f, 1f), new Color(0.08f, 0f, 0f, 1f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 16f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 120, 150) });

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, 0f);

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounceMultiplier = 0.1f;
            collision.dampenMultiplier = 0.8f;
            collision.quality = ParticleSystemCollisionQuality.High;

            return ps;
        }

        public static void CreateSmallBloodSplash(Vector3 pos)
        {
            ParticleSystem ps = GetFromPool(smallBloodSplashPool, SetupSmallBloodSplash);
            ps.gameObject.transform.position = pos;
            ps.gameObject.SetActive(true);
            ps.Play();
        }

        private static ParticleSystem SetupSmallBloodSplash()
        {
            GameObject bloodObj = new GameObject("VecnaBloodSplash_Pooled");
            bloodObj.SetActive(false);
            bloodObj.AddComponent<PooledParticle>().lifeTime = 2f;

            ParticleSystem ps = bloodObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = bloodObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));

            var main = ps.main;

            main.startColor = new Color(0.4f, 0f, 0f, 1f);

            main.duration = 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.gravityModifier = 1.5f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.6f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);

            return ps;
        }

        public static void DressCloneLikePlayer(GameObject clone, PlayerControllerB victim)
        {
            if (clone == null || victim == null) return;

            SkinnedMeshRenderer cloneRenderer = clone.GetComponentInChildren<SkinnedMeshRenderer>();
            if (cloneRenderer != null && victim.thisPlayerModel != null)
            {
                cloneRenderer.material = victim.thisPlayerModel.material;
            }

            Dictionary<string, Transform> cloneBones = new Dictionary<string, Transform>();
            foreach (Transform t in clone.GetComponentsInChildren<Transform>())
            {
                string cleanName = t.name.ToLower().Replace(".", "").Replace("_", "");
                if (cleanName.EndsWith("end")) cleanName = cleanName.Replace("end", "");

                if (!cloneBones.ContainsKey(cleanName)) cloneBones[cleanName] = t;
            }

            HashSet<Transform> clonedCosmeticRoots = new HashSet<Transform>();
            Renderer[] victimRenderers = victim.gameObject.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer r in victimRenderers)
            {
                if (r == victim.thisPlayerModel || r == victim.thisPlayerModelLOD1 || r == victim.thisPlayerModelLOD2 || r == victim.thisPlayerModelArms) continue;

                bool isItem = false;
                for (int i = 0; i < victim.ItemSlots.Length; i++)
                {
                    if (victim.ItemSlots[i] != null && r.transform.IsChildOf(victim.ItemSlots[i].transform))
                    {
                        isItem = true; break;
                    }
                }

                if (!isItem && victim.ItemOnlySlot != null)
                {
                    if (r.transform.IsChildOf(victim.ItemOnlySlot.transform))
                    {
                        isItem = true;
                    }
                }

                if (isItem) continue;

                string objName = r.gameObject.name.ToLower();
                if (objName.Contains("map") || objName.Contains("radar") || objName.Contains("arrow") ||
                    objName.Contains("cube") || objName.Contains("screen") || objName.Contains("sticker") ||
                    objName.Contains("badge") || objName.Contains("shadow") || objName.Contains("canvas") ||
                    objName.Contains("speak") || r.gameObject.layer == 5 || r.gameObject.layer == 14) continue;

                Transform cosmeticRoot = r.transform;
                Transform targetBoneOnClone = null;

                while (cosmeticRoot.parent != null)
                {
                    string searchName = cosmeticRoot.parent.name.ToLower().Replace(".", "").Replace("_", "");
                    if (cloneBones.TryGetValue(searchName, out Transform matchingBone))
                    {
                        targetBoneOnClone = matchingBone;
                        break;
                    }
                    cosmeticRoot = cosmeticRoot.parent;
                }

                if (targetBoneOnClone != null && !clonedCosmeticRoots.Contains(cosmeticRoot))
                {
                    string rootName = cosmeticRoot.name.ToLower();
                    if (rootName.Contains("scavengermodel") || rootName.Contains("metarig") || rootName.Contains("player")) continue;

                    clonedCosmeticRoots.Add(cosmeticRoot);

                    GameObject cosmeticCopy = GameObject.Instantiate(cosmeticRoot.gameObject, targetBoneOnClone);

                    foreach (var netObj in cosmeticCopy.GetComponentsInChildren<NetworkObject>()) UnityEngine.Object.DestroyImmediate(netObj);
                    foreach (var comp in cosmeticCopy.GetComponentsInChildren<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(comp);
                    foreach (var coll in cosmeticCopy.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(coll);

                    cosmeticCopy.SetActive(true);
                    cosmeticCopy.transform.localPosition = cosmeticRoot.localPosition;
                    cosmeticCopy.transform.localRotation = cosmeticRoot.localRotation;

                    Vector3 targetGlobalScale = cosmeticRoot.lossyScale;
                    Vector3 parentGlobalScale = targetBoneOnClone.lossyScale;

                    cosmeticCopy.transform.localScale = new Vector3(
                        parentGlobalScale.x > 0 ? targetGlobalScale.x / parentGlobalScale.x : 0f,
                        parentGlobalScale.y > 0 ? targetGlobalScale.y / parentGlobalScale.y : 0f,
                        parentGlobalScale.z > 0 ? targetGlobalScale.z / parentGlobalScale.z : 0f
                    );

                    foreach (Renderer copyRenderer in cosmeticCopy.GetComponentsInChildren<Renderer>())
                    {
                        copyRenderer.enabled = true;
                        copyRenderer.forceRenderingOff = false;
                        copyRenderer.gameObject.layer = 0;
                    }
                }
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
                if (player.usernameAlpha != null) player.usernameAlpha.alpha = !isLocalPlayer ? 1f : 0f;
            }
            else
            {
                if (!isLocalPlayer)
                {
                    player.thisPlayerModel.enabled = false;
                    player.thisPlayerModelLOD1.enabled = false;
                    player.thisPlayerModelLOD2.enabled = false;
                    foreach (AudioSource audio in player.gameObject.GetComponentsInChildren<AudioSource>(true))
                    {
                        audio.mute = true;
                    }
                }

                if (player.usernameCanvas != null) player.usernameCanvas.gameObject.SetActive(false);
                if (player.usernameBillboardText != null) player.usernameBillboardText.enabled = false;
                if (player.usernameAlpha != null) player.usernameAlpha.alpha = 0f;
            }

            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                GrabbableObject item = player.ItemSlots[i];
                if (item != null)
                {
                    item.EnableItemMeshes(isVisible);

                    if (!isLocalPlayer && !isVisible)
                    {
                        foreach (var r in item.GetComponentsInChildren<Renderer>())
                        {
                            if (r.enabled)
                            {
                                vecnaAI.hiddenCosmetics.Add(r);
                                r.enabled = false;
                                r.forceRenderingOff = true;
                            }
                        }

                        foreach (var light in item.GetComponentsInChildren<Light>())
                        {
                            if (light.enabled && light.intensity > 0f)
                            {
                                if (!vecnaAI.hiddenLights.ContainsKey(light)) vecnaAI.hiddenLights.Add(light, light.intensity);
                                light.enabled = false;
                                light.intensity = 0f;
                            }
                        }
                    }
                }
            }

            if (player.ItemOnlySlot != null)
            {
                GrabbableObject item = player.ItemOnlySlot;
                item.EnableItemMeshes(isVisible);

                if (!isLocalPlayer && !isVisible)
                {
                    foreach (var r in item.GetComponentsInChildren<Renderer>())
                    {
                        if (r.enabled)
                        {
                            vecnaAI.hiddenCosmetics.Add(r);
                            r.enabled = false;
                            r.forceRenderingOff = true;
                        }
                    }

                    foreach (var light in item.GetComponentsInChildren<Light>())
                    {
                        if (light.enabled && light.intensity > 0f)
                        {
                            if (!vecnaAI.hiddenLights.ContainsKey(light)) vecnaAI.hiddenLights.Add(light, light.intensity);
                            light.enabled = false;
                            light.intensity = 0f;
                        }
                    }
                }
            }

            if (!isVisible)
            {
                if (!isLocalPlayer)
                {
                    foreach (var renderer in player.gameObject.GetComponentsInChildren<Renderer>())
                    {
                        if (renderer == player.thisPlayerModel || renderer == player.thisPlayerModelLOD1 || renderer == player.thisPlayerModelLOD2) continue;
                        if (player.thisPlayerModelArms != null && renderer.gameObject == player.thisPlayerModelArms.gameObject) continue;
                        if (renderer.gameObject.name.ToLower().Contains("mapdot")) continue;

                        bool isItem = false;
                        for (int i = 0; i < player.ItemSlots.Length; i++)
                        {
                            if (player.ItemSlots[i] != null && renderer.transform.IsChildOf(player.ItemSlots[i].transform))
                            {
                                isItem = true; break;
                            }
                        }
                        if (isItem) continue;

                        if (renderer.enabled)
                        {
                            vecnaAI.hiddenCosmetics.Add(renderer);
                            renderer.enabled = false;
                            renderer.forceRenderingOff = true;
                        }
                    }

                    foreach (var light in player.gameObject.GetComponentsInChildren<Light>())
                    {
                        if (light.gameObject.name.ToLower().Contains("map") || light.gameObject.name.ToLower().Contains("nightvision")) continue;

                        if (light.enabled && light.intensity > 0f)
                        {
                            if (!vecnaAI.hiddenLights.ContainsKey(light)) vecnaAI.hiddenLights.Add(light, light.intensity);
                            light.enabled = false;
                            light.intensity = 0f;
                        }
                    }
                }
            }
            else
            {
                foreach (var renderer in vecnaAI.hiddenCosmetics)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                        renderer.forceRenderingOff = false;
                    }
                }
                vecnaAI.hiddenCosmetics.Clear();

                foreach (var kvp in vecnaAI.hiddenLights)
                {
                    if (kvp.Key != null)
                    {
                        kvp.Key.enabled = true;
                        kvp.Key.intensity = kvp.Value;
                    }
                }
                vecnaAI.hiddenLights.Clear();
            }

            
        }

        public static void ToggleTeammatesForVictim(VecnaAI vecnaAI, bool isVisible)
        {
            if (vecnaAI.cursingPlayer == null) return;
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            Camera mainCam = localPlayer.gameplayCamera;

            if (localPlayer.isPlayerDead && StartOfRound.Instance.spectateCamera != null)
            {
                mainCam = StartOfRound.Instance.spectateCamera;
            }

            if (!isVisible)
            {
                if (vecnaAI.storedCameraMask == -1)
                {
                    vecnaAI.storedCameraMask = mainCam.cullingMask;
                    vecnaAI.storedCamera = mainCam;
                }

                mainCam.cullingMask &= ~(1 << VecnaAI.PORTAL_ONLY_LAYER);
                mainCam.cullingMask |= (1 << VecnaAI.UPSIDE_DOWN_LAYER);

                vecnaAI.hiddenTeammateLayers.Clear();

                foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                {
                    if (p != null && p != vecnaAI.cursingPlayer && p.isPlayerControlled)
                    {
                        foreach (AudioSource audio in p.gameObject.GetComponentsInChildren<AudioSource>(true)) audio.mute = true;
                        if (p.usernameCanvas != null) p.usernameCanvas.gameObject.SetActive(false);
                        if (p.usernameBillboardText != null) p.usernameBillboardText.enabled = false;

                        if (vecnaAI.cursingPlayer.thisController != null && p.thisController != null)
                        {
                            Physics.IgnoreCollision(vecnaAI.cursingPlayer.thisController, p.thisController, true);
                            Physics.IgnoreCollision(vecnaAI.cursingPlayer.playerCollider, p.playerCollider, true);
                        }

                        foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r.GetComponent<Collider>() != null) continue;

                            string rName = r.gameObject.name.ToLower();
                            if (rName.Contains("map") || rName.Contains("radar") || rName.Contains("arrow"))
                            {
                                r.enabled = false;
                                continue;
                            }

                            if (!vecnaAI.hiddenTeammateLayers.ContainsKey(r)) vecnaAI.hiddenTeammateLayers[r] = r.gameObject.layer;
                            r.gameObject.layer = VecnaAI.PORTAL_ONLY_LAYER;
                        }
                    }
                }

                foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
                {
                    if (enemy != null && enemy != vecnaAI && !enemy.isEnemyDead)
                    {
                        foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r.GetComponent<Collider>() != null) continue;

                            if (r.gameObject.name.ToLower().Contains("mapdot"))
                            {
                                r.enabled = false;
                                continue;
                            }
                            if (!vecnaAI.hiddenTeammateLayers.ContainsKey(r)) vecnaAI.hiddenTeammateLayers[r] = r.gameObject.layer;
                            r.gameObject.layer = VecnaAI.PORTAL_ONLY_LAYER;
                        }
                    }
                }

                foreach (Renderer r in vecnaAI.cursingPlayer.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<Collider>() != null) continue;

                    if (vecnaAI.cursingPlayer.thisPlayerModelArms != null && r.gameObject == vecnaAI.cursingPlayer.thisPlayerModelArms.gameObject) continue;

                    string rName = r.gameObject.name.ToLower();
                    if (rName.Contains("map") || rName.Contains("radar") || rName.Contains("arrow"))
                    {
                        r.enabled = false;
                        continue;
                    }

                    bool isLocalVictim = (localPlayer == vecnaAI.cursingPlayer);
                    if (isLocalVictim)
                    {
                        if (r.gameObject.layer == 23) continue;
                        if (r == vecnaAI.cursingPlayer.thisPlayerModel ||
                            r == vecnaAI.cursingPlayer.thisPlayerModelLOD1 ||
                            r == vecnaAI.cursingPlayer.thisPlayerModelLOD2) continue;
                    }

                    if (!vecnaAI.hiddenTeammateLayers.ContainsKey(r)) vecnaAI.hiddenTeammateLayers[r] = r.gameObject.layer;
                    r.gameObject.layer = VecnaAI.UPSIDE_DOWN_LAYER;
                }

                foreach (Renderer r in vecnaAI.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<Collider>() != null) continue;

                    if (r.gameObject.name.ToLower().Contains("mapdot")) continue;

                    if (!vecnaAI.hiddenTeammateLayers.ContainsKey(r)) vecnaAI.hiddenTeammateLayers[r] = r.gameObject.layer;
                    r.gameObject.layer = VecnaAI.UPSIDE_DOWN_LAYER;
                }
            }
            else
            {
                foreach (Renderer r in vecnaAI.cursingPlayer.GetComponentsInChildren<Renderer>(true))
                {
                    string rName = r.gameObject.name.ToLower();
                    if (rName.Contains("map") || rName.Contains("radar") || rName.Contains("arrow"))
                    {
                        r.enabled = true;
                    }
                }
                if (vecnaAI.storedCameraMask != -1)
                {
                    Camera camToRestore = vecnaAI.storedCamera != null ? vecnaAI.storedCamera : mainCam;
                    camToRestore.cullingMask = vecnaAI.storedCameraMask;

                    vecnaAI.storedCameraMask = -1;
                    vecnaAI.storedCamera = null;
                }

                foreach (var kvp in vecnaAI.hiddenTeammateLayers)
                {
                    if (kvp.Key != null) kvp.Key.gameObject.layer = kvp.Value;
                }
                vecnaAI.hiddenTeammateLayers.Clear();

                foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
                {
                    if (p != null && p != vecnaAI.cursingPlayer && p.isPlayerControlled)
                    {
                        foreach (AudioSource audio in p.gameObject.GetComponentsInChildren<AudioSource>(true))
                        {
                            audio.mute = false;
                        }
                        if (p.usernameCanvas != null) p.usernameCanvas.gameObject.SetActive(true);
                        if (p.usernameBillboardText != null) p.usernameBillboardText.enabled = true;

                        if (vecnaAI.cursingPlayer.thisController != null && p.thisController != null)
                        {
                            Physics.IgnoreCollision(vecnaAI.cursingPlayer.thisController, p.thisController, false);
                            Physics.IgnoreCollision(vecnaAI.cursingPlayer.playerCollider, p.playerCollider, false);
                        }

                        foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                        {
                            string rName = r.gameObject.name.ToLower();
                            if (rName.Contains("map") || rName.Contains("radar") || rName.Contains("arrow"))
                            {
                                r.enabled = true;
                            }
                        }
                    }
                }

                foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsOfType<EnemyAI>())
                {
                    if (enemy != null && enemy != vecnaAI && !enemy.isEnemyDead)
                    {
                        foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r.gameObject.name.ToLower().Contains("mapdot"))
                            {
                                r.enabled = true;
                            }
                        }
                    }
                }
            }
        }

        public static void EnforceTeammateHeldItems(VecnaAI vecnaBrain)
        {
            foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
            {
                if (p != null && p != vecnaBrain.cursingPlayer && p.isPlayerControlled && !p.isPlayerDead)
                {
                    for (int i = 0; i < p.ItemSlots.Length; i++)
                    {
                        if (p.ItemSlots[i] != null)
                        {
                            foreach (Renderer r in p.ItemSlots[i].GetComponentsInChildren<Renderer>(true))
                            {
                                if (r.GetComponent<Collider>() != null) continue;

                                if (r.gameObject.layer != VecnaAI.PORTAL_ONLY_LAYER)
                                {
                                    if (!vecnaBrain.hiddenTeammateLayers.ContainsKey(r)) vecnaBrain.hiddenTeammateLayers[r] = r.gameObject.layer;
                                    r.gameObject.layer = VecnaAI.PORTAL_ONLY_LAYER;
                                }
                            }
                        }
                    }
                }

                if (p.ItemOnlySlot != null)
                {
                    foreach (Renderer r in p.ItemOnlySlot.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r.gameObject.layer != VecnaAI.PORTAL_ONLY_LAYER)
                        {
                            if (!vecnaBrain.hiddenTeammateLayers.ContainsKey(r)) vecnaBrain.hiddenTeammateLayers[r] = r.gameObject.layer;
                            r.gameObject.layer = VecnaAI.PORTAL_ONLY_LAYER;
                        }
                    }
                }
            }
        }
    }
}