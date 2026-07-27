using UnityEngine;
using System.Collections.Generic;
using GameNetcodeStuff;

namespace Vecna
{
    public class VecnaLairTrigger : MonoBehaviour
    {
        public List<PlayerControllerB> playersInLair = new List<PlayerControllerB>();
        private Collider lairCol;

        void Start()
        {
            lairCol = GetComponent<Collider>();
        }

        void Update()
        {
            if (lairCol == null || StartOfRound.Instance == null) return;

            playersInLair.Clear();
            foreach (PlayerControllerB p in StartOfRound.Instance.allPlayerScripts)
            {
                if (p != null && !p.isPlayerDead && p.isPlayerControlled && p.playerCollider != null)
                {
                    if (lairCol.bounds.Intersects(p.playerCollider.bounds))
                    {
                        playersInLair.Add(p);
                    }
                }
            }
        }
    }
}
