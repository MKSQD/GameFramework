using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("Cube/GameState")]
    public class GameState : ReplicaBehaviour {
        protected virtual void Start() {
            var world = (World)(isServer ? server.World : client.World);
            world.GameState = gameObject;
        }
    }
}