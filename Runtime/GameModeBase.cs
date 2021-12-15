using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    public abstract class GameModeBase : IGameMode {
        public ServerGame server;

        public GameModeBase(ServerGame server) {
            this.server = server;
            InstantiateGameState();
        }

        public abstract void Update();

        public abstract void StartToLeaveMap();

        public abstract void HandleNewPlayer(ServerPlayerController pc);

        protected virtual string GetGameStateKey() => null;

        void InstantiateGameState() {
            var key = GetGameStateKey();
            if (key == null)
                return;

            var gameStateHandle = server.Server.ReplicaManager.InstantiateReplicaAsync(key);
            gameStateHandle.Completed += ctx => {
                var gameStateGO = ctx.Result;

                var replica = gameStateGO.GetComponent<Replica>();
                if (replica == null) {
                    Debug.LogError("GameState Prefab needs Replica Component!");
                    return;
                }
                if ((replica.SettingsOrDefault.priorityFlags & ReplicaPriorityFlag.IgnorePosition) == 0) {
                    Debug.LogWarning("GameState Replica settings should have IgnorePosition flag set!");
                }

                var gameState = gameStateGO.GetComponent<GameState>();
                if (gameState == null) {
                    Debug.LogError("GameState Prefab needs GameState Component!");
                }
            };
        }
    }
}