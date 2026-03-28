using UnityEngine;
using System;
using GameNetcodeStuff;
using Unity.Netcode;

namespace Vecna
{
    [Serializable]
    public class VecnaPortalManager
    {
        private VecnaAI vecnaBrain;

        public GameObject activeEscapePortal;
        private Camera portalCamera;
        private RenderTexture portalRenderTexture;
        private AudioSource portalAudioSource;

        public VecnaPortalManager(VecnaAI brain)
        {
            this.vecnaBrain = brain;
        }

        public void UpdatePortalRotation()
        {
            if (this.vecnaBrain.cursingLocalPlayer && this.activeEscapePortal != null && this.vecnaBrain.cursingPlayer != null)
            {
                Vector3 lookPos = this.vecnaBrain.cursingPlayer.gameplayCamera.transform.position;
                this.activeEscapePortal.transform.rotation = Quaternion.LookRotation(this.activeEscapePortal.transform.position - lookPos);
            }
        }

        public void SpawnEscapePortalAtPosition(BoomboxItem rescuingBoombox, Vector3 position)
        {
            if (this.vecnaBrain.activeClone == null || this.vecnaBrain.cursingPlayer == null) return;

            this.portalRenderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.DefaultHDR);
            this.portalRenderTexture.antiAliasing = 8;

            GameObject camObj = new GameObject("VecnaPortalCamera");

            camObj.transform.position = this.vecnaBrain.activeClone.transform.position + (this.vecnaBrain.activeClone.transform.forward * 3f) + Vector3.up * 1.5f;
            camObj.transform.LookAt(this.vecnaBrain.activeClone.transform.position + Vector3.up * 1f);

            this.portalCamera = camObj.AddComponent<Camera>();
            this.portalCamera.targetTexture = this.portalRenderTexture;

            this.portalCamera.cullingMask = ~((1 << 5) | (1 << 14) | (1 << VecnaAI.UPSIDE_DOWN_LAYER));
            this.portalCamera.nearClipPlane = 0.1f;

            GameObject portalPrefab = Plugin.ModAssets.LoadAsset<GameObject>("VecnaPortalScreen");

            this.activeEscapePortal = UnityEngine.Object.Instantiate(portalPrefab, position, Quaternion.identity);
            this.activeEscapePortal.name = "VecnaEscapePortal";

            MeshRenderer portalRenderer = this.activeEscapePortal.GetComponentInChildren<MeshRenderer>();

            if (portalRenderer != null && portalRenderer.material != null)
            {
                portalRenderer.material.EnableKeyword("_EMISSION");
                portalRenderer.material.EnableKeyword("_EMISSION_WITH_TEXTURE");

                if (portalRenderer.material.HasProperty("_EmissiveColorMap"))
                    portalRenderer.material.SetTexture("_EmissiveColorMap", this.portalRenderTexture);

                if (portalRenderer.material.HasProperty("_EmissiveColor"))
                    portalRenderer.material.SetColor("_EmissiveColor", Color.white * 7f);

                portalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                portalRenderer.receiveShadows = false;
                portalRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                portalRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                portalRenderer.allowOcclusionWhenDynamic = false;
            }

            this.portalAudioSource = this.activeEscapePortal.AddComponent<AudioSource>();
            this.portalAudioSource.spatialBlend = 1f;
            this.portalAudioSource.minDistance = 15f;
            this.portalAudioSource.maxDistance = 50f;
            this.portalAudioSource.rolloffMode = AudioRolloffMode.Linear;
            this.portalAudioSource.loop = true;
            this.portalAudioSource.volume = 1f;

            if (SoundManager.Instance != null && SoundManager.Instance.diageticMixer != null)
                this.portalAudioSource.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("Master")[0];

            if (rescuingBoombox != null && rescuingBoombox.boomboxAudio != null)
            {
                this.portalAudioSource.clip = rescuingBoombox.boomboxAudio.clip;
                this.portalAudioSource.time = rescuingBoombox.boomboxAudio.time;
                this.portalAudioSource.Play();
            }

            Debug.Log("VECNA: Escape Portal Opened at " + position);
        }

        public void TogglePortal(bool open, BoomboxItem boombox, Vector3 position)
        {
            if (open)
            {
                if (this.activeEscapePortal == null) SpawnEscapePortalAtPosition(boombox, position);
            }
            else DestroyEscapePortal();
        }

        public void DestroyEscapePortal()
        {
            if (this.portalCamera != null) UnityEngine.Object.Destroy(this.portalCamera.gameObject);
            if (this.activeEscapePortal != null) UnityEngine.Object.Destroy(this.activeEscapePortal);
            if (this.portalRenderTexture != null)
            {
                this.portalRenderTexture.Release();
                UnityEngine.Object.Destroy(this.portalRenderTexture);
            }
            this.portalCamera = null;
            this.activeEscapePortal = null;
        }
    }
}