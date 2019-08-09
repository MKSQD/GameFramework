using UnityEngine;

namespace GameFramework {
    public abstract class GameModeBase : IGameMode {
        public GameState gameState {
            get;
            protected set;
        }
        public ServerGame server;

        public T GetGameState<T>() where T : GameState {
            return (T)gameState;
        }

        public GameModeBase(ServerGame server) {
            this.server = server;

            var prefab = GetGameStatePrefab();
            var gsGO = server.server.replicaManager.InstantiateReplica(prefab);

            gameState = gsGO.GetComponent<GameState>();
        }

        public abstract void Tick();

        public abstract void StartToLeaveMap();

        public abstract void HandleNewPlayer(PlayerController pc);

        public virtual GameObject GetGameStatePrefab() {
            return GameInstance.instance.defaultGameStatePrefab;
        }
    }
}