using UnityEngine;
using System;
using GameNetcodeStuff;
using Unity.Netcode;

namespace Vecna
{
    [Serializable]
    public class VecnaAudioManager
    {
        private VecnaAI vecnaBrain;

        // Constructor takes the brain reference
        public VecnaAudioManager(VecnaAI brain)
        {
            Initialize(brain);
        }

        public void Initialize(VecnaAI brain)
        {
            this.vecnaBrain = brain;
        }

        public void HandleBreathing()
        {
            if (this.vecnaBrain.breathingAudioSource == null)
            {
                Debug.LogWarning("VECNA AUDIO ERROR: Breathing AudioSource is NULL!");
                return;
            }

            if (this.vecnaBrain.breathingAudioSource.outputAudioMixerGroup == null)
            {
                Debug.LogWarning("VECNA AUDIO WARNING: Breathing AudioSource is missing an outputAudioMixerGroup! Reassigning...");
                if (SoundManager.Instance != null && SoundManager.Instance.diageticMixer != null)
                {
                    this.vecnaBrain.breathingAudioSource.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("Master")[0];
                }
            }

            if (this.vecnaBrain.breathingClips != null && this.vecnaBrain.breathingClips.Length > 0)
            {
                if (!this.vecnaBrain.breathingAudioSource.isPlaying)
                {
                    int randomIndex = UnityEngine.Random.Range(0, this.vecnaBrain.breathingClips.Length);
                    this.vecnaBrain.breathingAudioSource.clip = this.vecnaBrain.breathingClips[randomIndex];
                    Debug.Log("VECNA: Attempting to play breathing audio.");
                    this.vecnaBrain.breathingAudioSource.Play();
                }
            }
        }

        public void StopBreathing()
        {
            if (this.vecnaBrain.breathingAudioSource != null) this.vecnaBrain.breathingAudioSource.Stop();
        }

        public void PlayClockChime()
        {
            if (this.vecnaBrain.finalChimeClip != null && HUDManager.Instance != null && HUDManager.Instance.UIAudio != null)
            {
                HUDManager.Instance.UIAudio.PlayOneShot(this.vecnaBrain.finalChimeClip, 1f);
            }
        }

        public void PlayTelekinesisSound()
        {
            if (this.vecnaBrain.vecnaSnapAudioSource != null && this.vecnaBrain.liftTelekinesisClip != null)
            {
                this.vecnaBrain.vecnaSnapAudioSource.PlayOneShot(this.vecnaBrain.liftTelekinesisClip, 0.5f);
            }
        }

        public void PlayClockSpotTaunt()
        {
            if (this.vecnaBrain.clockSpotTaunts != null && this.vecnaBrain.clockSpotTaunts.Length > 0 && HUDManager.Instance != null && HUDManager.Instance.UIAudio != null)
            {
                AudioClip randomTaunt = this.vecnaBrain.clockSpotTaunts[UnityEngine.Random.Range(0, this.vecnaBrain.clockSpotTaunts.Length)];
                HUDManager.Instance.UIAudio.PlayOneShot(randomTaunt, 1f);
            }
        }

        public void PlayExecutionVoiceLine()
        {
            if (this.vecnaBrain != null && this.vecnaBrain.creatureVoice != null && this.vecnaBrain.executionVoiceLines != null && this.vecnaBrain.executionVoiceLines.Length > 0)
            {
                int randomVoice = UnityEngine.Random.Range(0, this.vecnaBrain.executionVoiceLines.Length);
                this.vecnaBrain.creatureVoice.clip = this.vecnaBrain.executionVoiceLines[randomVoice];
                this.vecnaBrain.creatureVoice.Play();
            }
        }

        public void PlayEscapeVoiceLine()
        {
            if (this.vecnaBrain != null && this.vecnaBrain.creatureVoice != null && this.vecnaBrain.escapeVoiceLines != null && this.vecnaBrain.escapeVoiceLines.Length > 0)
            {
                AudioClip randomEscape = this.vecnaBrain.escapeVoiceLines[UnityEngine.Random.Range(0, this.vecnaBrain.escapeVoiceLines.Length)];
                if (HUDManager.Instance != null && HUDManager.Instance.UIAudio != null)
                {
                    HUDManager.Instance.UIAudio.PlayOneShot(randomEscape, 1f);
                }
                else if (this.vecnaBrain.creatureVoice != null)
                {
                    this.vecnaBrain.creatureVoice.PlayOneShot(randomEscape);
                }
            }
        }

        public void StartChaseMusic(float volume = 0.6f)
        {
            if (this.vecnaBrain.chimechase == null)
            {
                Debug.LogWarning("VECNA AUDIO ERROR: Chimechase AudioSource is NULL!");
                return;
            }

            if (this.vecnaBrain.chimechase.outputAudioMixerGroup == null)
            {
                Debug.LogWarning("VECNA AUDIO WARNING: Chimechase AudioSource is missing an outputAudioMixerGroup! Reassigning...");
                if (SoundManager.Instance != null && SoundManager.Instance.diageticMixer != null)
                {
                    this.vecnaBrain.chimechase.outputAudioMixerGroup = SoundManager.Instance.diageticMixer.FindMatchingGroups("Master")[0];
                }
            }

            this.vecnaBrain.chimechase.volume = volume;
            Debug.Log("VECNA: Attempting to play chase music.");
            this.vecnaBrain.chimechase.Play();
        }

        public void StopChaseMusic()
        {
            if (this.vecnaBrain.chimechase != null) this.vecnaBrain.chimechase.Stop();
        }

        public float GetChaseMusicLength()
        {
            if (this.vecnaBrain.chimechase != null && this.vecnaBrain.chimechase.clip != null)
            {
                return this.vecnaBrain.chimechase.clip.length;
            }
            return 60f; // Default fallback if no audio is assigned
        }

        public void PlayBoneSnap(Vector3 position, AudioSource optionalSource = null)
        {
            if (this.vecnaBrain.playerSnapClip != null)
            {
                if (optionalSource != null)
                {
                    optionalSource.PlayOneShot(this.vecnaBrain.playerSnapClip, 1f);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(this.vecnaBrain.playerSnapClip, position, 1f);
                }
            }
        }
    }
}