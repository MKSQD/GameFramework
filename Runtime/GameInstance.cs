using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    public class GameInstance : MonoBehaviour {
        static GameInstance _instance;
        public static GameInstance instance {
            get {
                Assert.IsNotNull(_instance);
                return _instance;
            }
        }

        public ushort Port = 60000;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        public SimulatedLagSettings LagSettings;

        public GameObject DefaultGameStatePrefab;

        ClientGame _clientGame;
        ServerGame _serverGame;

        protected virtual void Start() {
            DontDestroyOnLoad(gameObject);

            Assert.IsNull(_instance);
            _instance = this;

            StartClient();
#if UNITY_EDITOR
            _clientGame.client.networkInterface.Connect("127.0.0.1", Port);

            StartServer();
#endif
        }

        public void StartClient() {
            if (_clientGame != null)
                return;

            var clientWorldGO = new GameObject("Client World");
            var clientWorld = clientWorldGO.AddComponent<World>();
            DontDestroyOnLoad(clientWorldGO);

            var ctx = new ClientGameContext() {
                World = clientWorld,
                LagSettings = LagSettings
            };
            _clientGame = CreateClient(ctx);
        }

        public void StartServer() {
            if (_serverGame != null)
                return;

            var serverWorldGO = new GameObject("Server World");
            var serverWorld = serverWorldGO.AddComponent<World>();
            DontDestroyOnLoad(serverWorldGO);

            var ctx = new ServerGameContext() {
                Port = Port,
                World = serverWorld,
                ReplicaManagerSettings = ReplicaManagerSettings,
                LagSettings = LagSettings
            };
            _serverGame = CreateServer(ctx);
        }

        protected virtual void Update() {
            if (_clientGame != null) {
                _clientGame.Update();
            }
            if (_serverGame != null) {
                _serverGame.Update();
            }
        }

        protected virtual void OnApplicationQuit() {
            if (_clientGame != null) {
                _clientGame.Shutdown();
            }
            if (_serverGame != null) {
                _serverGame.Shutdown();
            }
        }

        protected virtual ClientGame CreateClient(ClientGameContext ctx) {
            return new ClientGame(ctx);
        }

        protected virtual ServerGame CreateServer(ServerGameContext ctx) {
            return new ServerGame(ctx);
        }
    }
}