using UnityEngine;

namespace Vecna
{
    [CreateAssetMenu(menuName = "Vecna/Vecna Stats", fileName = "VecnaStats")]
    public class VecnaStats : ScriptableObject
    {
        [Header("Phase 1: Clock Stalking")]
        public float spawnInterval = 40f;
        public float maxUnspottedTime = 20f;
        public float maxStareTime = 10f;
        public int clocksBeforeChase = 3;

        [Header("Phase 2: Chasing")]
        public float chaseSpeed = 6.5f;
        public float killRange = 2.5f;
        public float killRangeSquared = 6.25f;
        public float maxChaseTime = 60f;

        [Header("Environment & Mechanics")]
        public float lightFlickerRadius = 25f;
        public float boomboxRescueRadius = 15f;
    }
}