﻿﻿﻿using UnityEngine;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;

namespace Vecna
{
    public class VecnaEnvironmentManipulator
    {
        private VecnaAI vecnaBrain; 
        private Light[] cachedLights;
        private DoorLock[] cachedDoors;
        private float slowScanTimer = 0f;
        private const float SLOW_SCAN_INTERVAL = 2.0f;
        private float flickerTimer = 0f;
        private float currentFlickerRand = 0f;

        private Dictionary<Light, float> originalLightIntensities = new Dictionary<Light, float>();
        private Dictionary<DoorLock, float> telekinesisCooldowns = new Dictionary<DoorLock, float>();

        public VecnaEnvironmentManipulator(VecnaAI brain)
        {
            this.vecnaBrain = brain;
        }

        public void UpdateScanner(float deltaTime)
        {
            this.slowScanTimer += deltaTime;
            if (this.slowScanTimer >= SLOW_SCAN_INTERVAL)
            {
                this.slowScanTimer = 0f;
                this.cachedDoors = GameObject.FindObjectsOfType<DoorLock>();

                Light[] allLights = GameObject.FindObjectsOfType<Light>();
                List<Light> filteredLights = new List<Light>();

                foreach (Light light in allLights)
                {
                    if (light == null) continue;
                    if (light.type == LightType.Directional) continue;

                    string nameLower = light.gameObject.name.ToLower();
                    if (nameLower.Contains("helmet") || nameLower.Contains("visor") || nameLower.Contains("sun")) continue;

                    filteredLights.Add(light);

                    if (!this.originalLightIntensities.ContainsKey(light))
                    {
                        this.originalLightIntensities[light] = light.intensity;
                    }
                }

                this.cachedLights = filteredLights.ToArray();
            }
        }

        public void FlickerNearbyLights(float deltaTime, PlayerControllerB victim)
        {
            if (this.cachedLights == null || victim == null) return;

            this.flickerTimer += deltaTime;
            if (this.flickerTimer > 0.15f)
            {
                this.flickerTimer = 0f;
                this.currentFlickerRand = UnityEngine.Random.value;
            }

            float squaredRadius = this.vecnaBrain.stats.lightFlickerRadius * this.vecnaBrain.stats.lightFlickerRadius;
            bool isIntensePhase = (this.vecnaBrain.currentLocalPhase == VecnaAI.VecnaPhase.Chasing || this.vecnaBrain.currentLocalPhase == VecnaAI.VecnaPhase.ExecutingKill);
            bool isClockPhase = (this.vecnaBrain.currentLocalPhase == VecnaAI.VecnaPhase.ClockStalking || this.vecnaBrain.currentLocalPhase == VecnaAI.VecnaPhase.ClockSpotted) && this.vecnaBrain.currentClock != null;

            foreach (Light light in this.cachedLights)
            {
                if (light == null) continue;
                
                if (!this.originalLightIntensities.TryGetValue(light, out float originalIntensity)) continue;

                float lightDistanceFromPlayerSq = (light.transform.position - victim.transform.position).sqrMagnitude;

                if (lightDistanceFromPlayerSq <= squaredRadius)
                {
                    if (isIntensePhase)
                    {
                        if (this.currentFlickerRand > 0.8f) light.intensity = originalIntensity * 2.5f;
                        else light.intensity = originalIntensity;
                    }
                    else if (isClockPhase)
                    {
                        if (this.currentFlickerRand > 0.9f) light.intensity = originalIntensity * 1.3f;
                        else if (this.currentFlickerRand > 0.7f) light.intensity = originalIntensity * 0.3f;
                        else light.intensity = originalIntensity;
                    }
                    else light.intensity = originalIntensity;
                }
                else
                {
                    light.intensity = originalIntensity;
                }
            }
        }

        public void RestoreLights()
        {
            foreach (var kvp in this.originalLightIntensities)
            {
                if (kvp.Key != null) kvp.Key.intensity = kvp.Value;
            }
            this.originalLightIntensities.Clear();
        }

        public void BlastDoorsOpen()
        {
            if (!this.vecnaBrain.IsServer || this.cachedDoors == null || this.cachedDoors.Length == 0) return;

            float blastRangeSq = 7f * 7f;

            foreach (DoorLock door in this.cachedDoors)
            {
                if (door == null) continue;

                if ((this.vecnaBrain.transform.position - door.transform.position).sqrMagnitude < blastRangeSq)
                {
                    bool isOnCooldown = this.telekinesisCooldowns.ContainsKey(door) && (Time.time - this.telekinesisCooldowns[door] < 5f);

                    if (!isOnCooldown && !door.isDoorOpened)
                    {
                        if (door.isLocked) door.UnlockDoorServerRpc();

                        AnimatedObjectTrigger doorSwing = door.GetComponent<AnimatedObjectTrigger>();
                        if (doorSwing != null) doorSwing.TriggerAnimationNonPlayer(true, true, false);

                        this.vecnaBrain.PlayDoorAnimationClientRpc();
                        door.OpenDoorAsEnemyServerRpc();

                        this.telekinesisCooldowns[door] = Time.time;
                    }
                }
            }
        }
    }
}