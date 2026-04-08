using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace Vecna.Configuration
{
    public class PluginConfig
    {
        public ConfigEntry<int> SpawnWeightTitan;
        public ConfigEntry<int> SpawnWeightRend;
        public ConfigEntry<int> SpawnWeightDine;
        public ConfigEntry<int> SpawnWeightOffense;
        public ConfigEntry<int> SpawnWeightMarch;
        public ConfigEntry<int> SpawnWeightExperimentation;
        public ConfigEntry<int> SpawnWeightAssurance;
        public ConfigEntry<int> SpawnWeightVow;
        public ConfigEntry<int> SpawnWeightArtifice;
        public ConfigEntry<int> SpawnWeightEmbrion;
        public ConfigEntry<int> SpawnWeightAdamance;
        public ConfigEntry<int> SpawnWeightModded;

        // Phase 1
        public ConfigEntry<float> SpawnInterval;
        public ConfigEntry<float> MaxUnspottedTime;
        public ConfigEntry<float> MaxStareTime;
        public ConfigEntry<int> ClocksBeforeChase;

        // Phase 2
        public ConfigEntry<float> ChaseSpeed;
        public ConfigEntry<float> KillRange;
        public ConfigEntry<float> MaxChaseTime;

        // Environment
        public ConfigEntry<float> LightFlickerRadius;
        public ConfigEntry<float> BoomboxRescueRadius;

        public PluginConfig(ConfigFile cfg)
        {
            SpawnWeightTitan = cfg.Bind("0. Spawn Weights", "Titan", 50, "Spawn weight of Vecna on Titan.");
            SpawnWeightRend = cfg.Bind("0. Spawn Weights", "Rend", 75, "Spawn weight of Vecna on Rend.");
            SpawnWeightDine = cfg.Bind("0. Spawn Weights", "Dine", 55, "Spawn weight of Vecna on Dine.");
            SpawnWeightOffense = cfg.Bind("0. Spawn Weights", "Offense", 30, "Spawn weight of Vecna on Offense.");
            SpawnWeightMarch = cfg.Bind("0. Spawn Weights", "March", 30, "Spawn weight of Vecna on March.");
            SpawnWeightExperimentation = cfg.Bind("0. Spawn Weights", "Experimentation", 10, "Spawn weight of Vecna on Experimentation.");
            SpawnWeightAssurance = cfg.Bind("0. Spawn Weights", "Assurance", 20, "Spawn weight of Vecna on Assurance.");
            SpawnWeightVow = cfg.Bind("0. Spawn Weights", "Vow", 20, "Spawn weight of Vecna on Vow.");
            SpawnWeightArtifice = cfg.Bind("0. Spawn Weights", "Artifice", 65, "Spawn weight of Vecna on Artifice.");
            SpawnWeightEmbrion = cfg.Bind("0. Spawn Weights", "Embrion", 100, "Spawn weight of Vecna on Embrion.");
            SpawnWeightAdamance = cfg.Bind("0. Spawn Weights", "Adamance", 55, "Spawn weight of Vecna on Adamance.");
            SpawnWeightModded = cfg.Bind("0. Spawn Weights", "Modded Moons", 45, "Base spawn weight of Vecna on custom modded moons.");

            // Phase 1
            SpawnInterval = cfg.Bind("1. Phase 1 (Stalking)", "Clock Spawn Interval", 40f, "Time in seconds between clock spawns.");
            MaxUnspottedTime = cfg.Bind("1. Phase 1 (Stalking)", "Max Unspotted Time", 20f, "Time before an unseen clock vanishes.");
            MaxStareTime = cfg.Bind("1. Phase 1 (Stalking)", "Max Stare Time", 10f, "Time before a spotted clock vanishes (first 2 clocks).");
            ClocksBeforeChase = cfg.Bind("1. Phase 1 (Stalking)", "Clocks Before Chase", 3, "Number of clocks that must spawn before the chase begins.");

            // Phase 2
            ChaseSpeed = cfg.Bind("2. Phase 2 (Chasing)", "Chase Speed", 6.5f, "Vecna's movement speed while chasing.");
            KillRange = cfg.Bind("2. Phase 2 (Chasing)", "Kill Range", 2.5f, "Distance required to initiate the execution.");
            MaxChaseTime = cfg.Bind("2. Phase 2 (Chasing)", "Max Chase Time", 60f, "How long the chase lasts before the victim survives.");

            // Mechanics
            LightFlickerRadius = cfg.Bind("3. Mechanics", "Light Flicker Radius", 25f, "Radius around the victim where lights will flicker.");
            BoomboxRescueRadius = cfg.Bind("3. Mechanics", "Boombox Rescue Radius", 15f, "Distance a boombox must be from the real-world body to open a portal.");

            ClearUnusedEntries(cfg);
        }

        public void ApplyTo(VecnaStats stats)
        {
            if (stats == null) return;

            stats.spawnInterval = SpawnInterval.Value;
            stats.maxUnspottedTime = MaxUnspottedTime.Value;
            stats.maxStareTime = MaxStareTime.Value;
            stats.clocksBeforeChase = ClocksBeforeChase.Value;

            stats.chaseSpeed = ChaseSpeed.Value;
            stats.killRange = KillRange.Value;
            stats.killRangeSquared = KillRange.Value * KillRange.Value;
            stats.maxChaseTime = MaxChaseTime.Value;

            stats.lightFlickerRadius = LightFlickerRadius.Value;
            stats.boomboxRescueRadius = BoomboxRescueRadius.Value;
        }

        private void ClearUnusedEntries(ConfigFile cfg)
        {
            PropertyInfo orphanedEntriesProp = cfg.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg, null);
            orphanedEntries.Clear();
            cfg.Save();
        }
    }
}