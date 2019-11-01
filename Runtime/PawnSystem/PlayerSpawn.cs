using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/PlayerSpawn")]
    public class PlayerSpawn : MonoBehaviour {
        static public List<PlayerSpawn> all = new List<PlayerSpawn>();

        public Vector3 GetRandomizedPosition() {
            var offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            offset.Normalize();

            return transform.position + offset;
        }

        void OnEnable() {
            all.Add(this);
        }

        void OnDisable() {
            all.Remove(this);
        }
    }
}