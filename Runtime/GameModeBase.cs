using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameFramework {
    public abstract class GameModeBase : IGameMode {
        public GameState gameState {
            get;
            protected set;
        }
        public ServerGame server;

        public T GetGameState<T>() where T : GameState => (T)gameState;

        public GameModeBase(ServerGame server) {
            this.server = server;
            InstantiateGameState();
        }

        public abstract void Update();

        public abstract void StartToLeaveMap();

        public abstract void HandleNewPlayer(PlayerController pc);

        protected virtual AsyncOperationHandle<GameObject> InstantiateGameStatePrefabAsync() {
            return GameInstance.Main.DefaultGameStatePrefab.LoadAssetAsync<GameObject>();
        }

        void InstantiateGameState() {
            var prefabAsyncHandle = InstantiateGameStatePrefabAsync();
            prefabAsyncHandle.Completed += obj => {
                var gsGO = server.server.replicaManager.InstantiateReplica(obj.Result);

                gameState = gsGO.GetComponent<GameState>();
            };
        }
    }
}