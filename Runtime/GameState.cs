using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("Cube/GameState")]
    public class GameState : ReplicaBehaviour {
        protected virtual void Start() {
            if (isServer) {
                var serverGame = (ServerGame)server;
                serverGame.GameState = gameObject;
            } else {
                var clientGame = (ClientGame)client;
                clientGame.GameState = gameObject;
            }
        }
    }
}