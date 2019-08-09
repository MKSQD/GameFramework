using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/PlayerSpawn")]
    public class PlayerSpawn : MonoBehaviour {
        static public PlayerSpawn instance;

        public Vector3 GetRandomizedPosition() {
            var offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            offset.Normalize();

            return transform.position + offset;
        }

        void OnEnable() {
            instance = this;
        }

        void OnDisable() {
            instance = null;
        }
    }
}