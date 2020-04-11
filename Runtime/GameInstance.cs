using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace GameFramework {
    public class GameInstance : MonoBehaviour {
        static GameInstance _main;
        public static GameInstance main {
            get {
                Assert.IsNotNull(_main);
                return _main;
            }
        }

        public ushort Port = 60000;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        public SimulatedLagSettings LagSettings;

        public AssetReference DefaultGameStatePrefab;

        public bool CharacterInputEnabled = true;

        public ClientGame GameClient {
            get;
            internal set;
        }
        public ServerGame GameServer {
            get;
            internal set;
        }

        public void StartClient() {
            if (GameClient != null)
                return;

            var clientWorldGO = new GameObject("Client World");
            var clientWorld = clientWorldGO.AddComponent<World>();
            DontDestroyOnLoad(clientWorldGO);

            var ctx = new ClientGameContext() {
                World = clientWorld,
                LagSettings = LagSettings
            };
            GameClient = CreateClient(ctx);
        }

        public void StartServer() {
            if (GameServer != null)
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
            GameServer = CreateServer(ctx);
        }

        protected virtual void Start() {
            DontDestroyOnLoad(gameObject);

            Assert.IsNull(_main);
            _main = this;

            StartClient();
#if UNITY_EDITOR
            GameClient.client.networkInterface.Connect("127.0.0.1", Port);

            StartServer();
#endif
        }

        protected virtual void Update() {
            if (GameClient != null) {
                GameClient.Update();
            }
            if (GameServer != null) {
                GameServer.Update();
            }
        }

        protected virtual void OnApplicationQuit() {
            if (GameClient != null) {
                GameClient.Shutdown();
            }
            if (GameServer != null) {
                GameServer.Shutdown();
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