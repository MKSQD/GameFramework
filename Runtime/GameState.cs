using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("Cube/GameState")]
    public class GameState : ReplicaBehaviour {
        protected virtual void Start() {
            if (isServer) {
                ServerGame.Main.GameState = gameObject;
            } else {
                ClientGame.Main.GameState = gameObject;
            }
        }
    }
}