using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/PlayerSpawn")]
    public class PlayerSpawn : MonoBehaviour {
        public static List<PlayerSpawn> All = new List<PlayerSpawn>();

        public Vector3 GetRandomizedPosition() {
            var offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            offset.Normalize();

            return transform.position + offset;
        }

        void OnEnable() {
            All.Add(this);
        }

        void OnDisable() {
            All.Remove(this);
        }
    }
}