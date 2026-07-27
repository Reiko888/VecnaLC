using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Vecna
{
    public class HauntVisibilityRegistry
    {
        private static readonly Dictionary<GameObject, EntityState> VisibilityStates = new Dictionary<GameObject, EntityState>();

        private sealed class EntityState
        {
            public readonly HashSet<string> tags = new HashSet<string>();
            public readonly HashSet<Renderer> disabledRenderers = new HashSet<Renderer>();
            public readonly HashSet<Light> disabledLights = new HashSet<Light>();
            public readonly HashSet<Rigidbody> disabledGravitys = new HashSet<Rigidbody>();
            public readonly HashSet<Collider> disabledColliders = new HashSet<Collider>();
            public readonly HashSet<TextMeshProUGUI> disabledTextMeshes = new HashSet<TextMeshProUGUI>();
            public readonly HashSet<DecalProjector> disabledDecals = new HashSet<DecalProjector>();
            public readonly HashSet<InteractTrigger> disabledInteractTriggers = new HashSet<InteractTrigger>();
            public readonly HashSet<ParticleSystem> disabledParticles = new HashSet<ParticleSystem>();
            public readonly Dictionary<AudioSource, float> audioVolumes = new Dictionary<AudioSource, float>();
            public readonly Dictionary<Component, List<string>> cachedCosmetics = new Dictionary<Component, List<string>>();
        }

        public static void Hide(GameObject entity, string tag)
        {
            if (entity == null) return;
            if (string.IsNullOrWhiteSpace(tag)) tag = "default";

            CleanupDeadEntries();

            if (!VisibilityStates.TryGetValue(entity, out EntityState state))
            {
                state = new EntityState();
                VisibilityStates[entity] = state;
            }

            HideInternal(entity, state);
            state.tags.Add(tag);
        }

        public static void Restore(GameObject entity, string tag)
        {
            if (entity == null) return;
            if (string.IsNullOrWhiteSpace(tag)) tag = "default";

            if (VisibilityStates.TryGetValue(entity, out EntityState state))
            {
                state.tags.Remove(tag);
                if (state.tags.Count == 0)
                {
                    RestoreInternal(entity, state);
                }
            }
        }

        public static void ForceRestore(GameObject entity)
        {
            if (entity != null && VisibilityStates.TryGetValue(entity, out EntityState state))
            {
                RestoreInternal(entity, state);
            }
        }

        public static bool IsHidden(GameObject entity)
        {
            return entity != null && VisibilityStates.ContainsKey(entity);
        }

        private static void HideInternal(GameObject entity, EntityState state)
        {
            // Reflectively cache and destroy MoreCompany cosmetics to avoid ModelReplacementAPI rendering leaks
            try
            {
                Type cosmeticAppType = Type.GetType("MoreCompany.Cosmetics.CosmeticApplication, MoreCompany");
                if (cosmeticAppType != null)
                {
                    var cosmeticApps = entity.GetComponentsInChildren(cosmeticAppType, true);
                    foreach (var cosmeticApp in cosmeticApps)
                    {
                        if (cosmeticApp == null) continue;

                        var spawnedCosmeticsField = cosmeticAppType.GetField("spawnedCosmeticsIds", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (spawnedCosmeticsField != null)
                        {
                            var list = spawnedCosmeticsField.GetValue(cosmeticApp) as List<string>;
                            if (list != null && list.Count > 0)
                            {
                                List<string> idsCopy = new List<string>(list);
                                state.cachedCosmetics[cosmeticApp] = idsCopy;

                                var clearMethod = cosmeticAppType.GetMethod("ClearCosmetics", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (clearMethod != null)
                                {
                                    //Plugin.Logger.LogInfo($"VECNA: Caching and destroying {idsCopy.Count} cosmetics on {entity.name}");
                                    clearMethod.Invoke(cosmeticApp, null);
                                }
                            }
                        }
                    }
                }

                // Temporarily bypass culling prefix to force the manager to clear its own cache
                Type cosmeticManagerType = Type.GetType("ModelReplacement.MoreCompanyCosmeticManager, ModelReplacementAPI");
                if (cosmeticManagerType != null)
                {
                    var cosmeticManager = entity.GetComponent(cosmeticManagerType);
                    if (cosmeticManager != null)
                    {
                        var updateMethod = cosmeticManagerType.GetMethod("UpdateModelReplacement", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (updateMethod != null)
                        {
                            //Plugin.Logger.LogInfo($"VECNA: Temporarily bypassing prefix and forcing UpdateModelReplacement to clear manager cache on hide");
                            ModelReplacementAPISoftCompat.BypassCosmeticPrefix = true;
                            updateMethod.Invoke(cosmeticManager, null);
                            ModelReplacementAPISoftCompat.BypassCosmeticPrefix = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelReplacementAPISoftCompat.BypassCosmeticPrefix = false;
                Plugin.Logger.LogError($"VECNA: Error clearing cosmetics in HideInternal: {ex}");
            }

            foreach (Renderer renderer in entity.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.name != "ScavengerModelArmsOnly" && !renderer.gameObject.name.Contains("CloneNametag"))
                {
                    renderer.enabled = false;
                    state.disabledRenderers.Add(renderer);
                }
            }
            foreach (Light light in entity.GetComponentsInChildren<Light>(true))
            {
                if (light != null && light.enabled)
                {
                    if (light.gameObject.name.ToLower().Contains("map") || light.gameObject.name.ToLower().Contains("nightvision")) continue;
                    light.enabled = false;
                    state.disabledLights.Add(light);
                }
            }
            foreach (Rigidbody rigidbody in entity.GetComponentsInChildren<Rigidbody>(true))
            {
                if (rigidbody != null && rigidbody.useGravity)
                {
                    rigidbody.useGravity = false;
                    state.disabledGravitys.Add(rigidbody);
                }
            }
            foreach (Collider collider in entity.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null && collider.enabled && collider.gameObject.name != "PlayerPhysicsBox" && collider.gameObject.name != "ItemOnlySlot")
                {
                    if (collider.GetComponentInParent<DoorLock>() != null) continue;

                    collider.enabled = false;
                    state.disabledColliders.Add(collider);
                }
            }
            foreach (TextMeshProUGUI textMesh in entity.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (textMesh != null && textMesh.enabled)
                {
                    textMesh.enabled = false;
                    state.disabledTextMeshes.Add(textMesh);
                }
            }
            foreach (DecalProjector decal in entity.GetComponentsInChildren<DecalProjector>(true))
            {
                if (decal != null && decal.enabled)
                {
                    decal.enabled = false;
                    state.disabledDecals.Add(decal);
                }
            }
            foreach (InteractTrigger interactTrigger in entity.GetComponentsInChildren<InteractTrigger>(true))
            {
                if (interactTrigger != null && interactTrigger.interactable)
                {
                    if (interactTrigger.GetComponentInParent<DoorLock>() != null) continue;

                    interactTrigger.interactable = false;
                    state.disabledInteractTriggers.Add(interactTrigger);
                }
            }
            foreach (ParticleSystem particle in entity.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle != null && particle.isPlaying)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    state.disabledParticles.Add(particle);
                }
            }
            foreach (AudioSource audioSource in entity.GetComponentsInChildren<AudioSource>(true))
            {
                if (audioSource != null && audioSource.volume > 0f)
                {
                    state.audioVolumes[audioSource] = audioSource.volume;
                    audioSource.volume = 0f;
                }
            }
        }

        private static void RestoreInternal(GameObject entity, EntityState state)
        {
            foreach (Renderer renderer in state.disabledRenderers)
            {
                if (renderer != null) renderer.enabled = true;
            }
            foreach (Light light in state.disabledLights)
            {
                if (light != null) light.enabled = true;
            }
            foreach (Rigidbody rigidbody in state.disabledGravitys)
            {
                if (rigidbody != null) rigidbody.useGravity = true;
            }
            foreach (Collider collider in state.disabledColliders)
            {
                if (collider != null) collider.enabled = true;
            }
            foreach (TextMeshProUGUI textMesh in state.disabledTextMeshes)
            {
                if (textMesh != null) textMesh.enabled = true;
            }
            foreach (DecalProjector decal in state.disabledDecals)
            {
                if (decal != null) decal.enabled = true;
            }
            foreach (InteractTrigger interactTrigger in state.disabledInteractTriggers)
            {
                if (interactTrigger != null) interactTrigger.interactable = true;
            }
            foreach (ParticleSystem particle in state.disabledParticles)
            {
                if (particle != null && particle.gameObject != null) particle.Play(true);
            }
            foreach (KeyValuePair<AudioSource, float> kv in state.audioVolumes)
            {
                if (kv.Key != null) kv.Key.volume = kv.Value;
            }

            // Remove from registry first so IsHidden becomes false
            state.tags.Clear();
            VisibilityStates.Remove(entity);
            // Delay it reassignment.,
            if (state.cachedCosmetics.Count > 0)
            {
                List<string> allIds = new List<string>();
                foreach (var kvp in state.cachedCosmetics)
                {
                    foreach (var id in kvp.Value)
                    {
                        if (!allIds.Contains(id)) allIds.Add(id);
                    }
                }
                if (allIds.Count > 0 && HUDManager.Instance != null)
                {
                    HUDManager.Instance.StartCoroutine(RestoreCosmeticsCoroutine(entity, allIds));
                }
            }
        }

        private static System.Collections.IEnumerator RestoreCosmeticsCoroutine(GameObject entity, List<string> ids)
        {
            if (entity == null) yield break;
            Type cosmeticManagerType = Type.GetType("ModelReplacement.MoreCompanyCosmeticManager, ModelReplacementAPI");
            Type bodyReplacementType = Type.GetType("ModelReplacement.BodyReplacementBase, ModelReplacementAPI");

            if (bodyReplacementType != null)
            {
                var bodyReplacement = entity.GetComponent(bodyReplacementType);
                if (bodyReplacement != null)
                {
                    var replacementModelField = bodyReplacementType.GetField("replacementModel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (replacementModelField != null)
                    {
                        float timer = 0f;
                        while (replacementModelField.GetValue(bodyReplacement) == null && timer < 5f)
                        {
                            yield return new WaitForSeconds(0.1f);
                            timer += 0.1f;
                        }
                        //Plugin.Logger.LogInfo($"VECNA: Waited {timer:F1}s for replacementModel to be loaded on {entity.name}");
                    }
                }
            }

            try
            {
                Type cosmeticAppType = Type.GetType("MoreCompany.Cosmetics.CosmeticApplication, MoreCompany");
                if (cosmeticAppType != null && ids.Count > 0)
                {
                    var cosmeticApps = entity.GetComponentsInChildren(cosmeticAppType, true);
                    var applyMethod = cosmeticAppType.GetMethod("ApplyCosmetic", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (applyMethod != null)
                    {
                        foreach (var cosmeticApp in cosmeticApps)
                        {
                            if (cosmeticApp == null) continue;

                            Plugin.Logger.LogInfo($"VECNA: Coroutine re-applying {ids.Count} cosmetics on {entity.name}");
                            foreach (var id in ids)
                            {
                                applyMethod.Invoke(cosmeticApp, new object[] { id, true });
                            }

                            var spawnedCosmeticsField = cosmeticAppType.GetField("spawnedCosmetics", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (spawnedCosmeticsField != null)
                            {
                                var list = spawnedCosmeticsField.GetValue(cosmeticApp) as System.Collections.IList;
                                if (list != null)
                                {
                                    foreach (var cosmeticObj in list)
                                    {
                                        if (cosmeticObj is MonoBehaviour cosmeticBehaviour)
                                        {
                                            cosmeticBehaviour.transform.localScale *= 0.38f;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Plugin.Logger.LogError($"VECNA: Error restoring cosmetics in RestoreCosmeticsCoroutine: {ex}");
            }

            try
            {
                if (cosmeticManagerType != null && bodyReplacementType != null)
                {
                    var cosmeticManager = entity.GetComponent(cosmeticManagerType);
                    var bodyReplacement = entity.GetComponent(bodyReplacementType);
                    if (cosmeticManager != null && bodyReplacement != null)
                    {
                        var reportMethod = cosmeticManagerType.GetMethod("ReportBodyReplacementAddition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (reportMethod != null)
                        {
                            //Plugin.Logger.LogInfo($"VECNA: Coroutine calling ReportBodyReplacementAddition on {entity.name} to snap cosmetics and fix scaling");
                            ModelReplacementAPISoftCompat.BypassCosmeticPrefix = true;
                            reportMethod.Invoke(cosmeticManager, new object[] { bodyReplacement });
                            ModelReplacementAPISoftCompat.BypassCosmeticPrefix = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModelReplacementAPISoftCompat.BypassCosmeticPrefix = false;
                //Plugin.Logger.LogError($"VECNA: Error reflectively updating MoreCompanyCosmeticManager in coroutine: {ex}");
            }
        }

        private static void CleanupDeadEntries()
        {
            List<GameObject> toRemove = null;

            foreach (KeyValuePair<GameObject, EntityState> kv in VisibilityStates)
            {
                if (kv.Key == null)
                {
                    if (toRemove == null) toRemove = new List<GameObject>();
                    toRemove.Add(kv.Key);
                }
            }

            if (toRemove == null) return;

            foreach (GameObject gameObject in toRemove)
            {
                VisibilityStates.Remove(gameObject);
            }
        }
    }
}
