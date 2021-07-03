using Cube.Replication;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameFramework {
    public abstract class GameModeBase : IGameMode {
        public GameObject GameState {
            get;
            protected set;
        }
        public ServerGame server;

        public GameModeBase(ServerGame server) {
            this.server = server;
            InstantiateGameState();
        }

        public abstract void Update();

        public abstract void StartToLeaveMap();

        public abstract void HandleNewPlayer(PlayerController pc);

        protected virtual string GetGameStateKey() => null;

        void InstantiateGameState() {
            var key = GetGameStateKey();
            if (key == null)
                return;

            var gameStateHandle = server.server.ReplicaManager.InstantiateReplicaAsync(key);
            gameStateHandle.Completed += ctx => {
                GameState = ctx.Result;

                var replica = GameState.GetComponent<Replica>();
                if (replica == null) {
                    Debug.LogError("GameState Prefab needs to be a Replica!");
                    return;
                }

                if ((replica.settingsOrDefault.priorityFlags & ReplicaPriorityFlag.IgnorePosition) == 0) {
                    Debug.LogWarning("GameState Replica settings should have IgnorePosition flag set!");
                }
            };
        }
    }
}