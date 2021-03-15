using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace GameFramework {
    public class GameInstance : MonoBehaviour {
        static GameInstance _main;
        public static GameInstance Main {
            get {
                Assert.IsNotNull(_main);
                return _main;
            }
        }

        public ushort Port = 60000;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        public SimulatedLagSettings LagSettings;

        public AssetReference DefaultGameStatePrefab;

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

            OnClientRunning();
        }

        public void StartServer() {
            if (GameServer != null)
                return;

            var serverWorldGO = new GameObject("Server World");
            var serverWorld = serverWorldGO.AddComponent<World>();
            DontDestroyOnLoad(serverWorldGO);

            var ctx = new ServerGameContext() {
                World = serverWorld,
                Port = Port,
                ReplicaManagerSettings = ReplicaManagerSettings,
                LagSettings = LagSettings
            };
            GameServer = CreateServer(ctx);

            OnServerRunning();
        }

        protected virtual void Start() {
            if (_main != null) {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            Assert.IsNull(_main);
            _main = this;

#if !UNITY_EDITOR
            var args = System.Environment.GetCommandLineArgs();
            var isServer = false;
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "-server") {
                    isServer = true;
                    break;
                }
            }

            if (isServer) {
                StartServer();
            }
            else {
                StartClient();
            }
#else
#if SERVER
            StartServer();
#endif
#if CLIENT
            StartClient();
            GameClient.client.networkInterface.Connect("127.0.0.1", Port);
#endif
#endif
        }

        protected virtual void Update() {
            GameClient?.Update();
            GameServer?.Update();
        }

        void OnApplicationQuit() {
            GameClient?.Shutdown();
            GameServer?.Shutdown();
        }

        protected virtual ClientGame CreateClient(ClientGameContext ctx) {
            return new ClientGame(ctx);
        }

        protected virtual ServerGame CreateServer(ServerGameContext ctx) {
            return new ServerGame(ctx);
        }

        protected virtual void OnClientRunning() { }
        protected virtual void OnServerRunning() { }
    }
}