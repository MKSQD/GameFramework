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

        public ServerReplicaManagerSettings replicaManagerSettings;
        public ClientSimulatedLagSettings lagSettings;

        public GameObject defaultGameStatePrefab;

        ClientGame _client;
        ServerGame _server;

        protected virtual void Start() {
            DontDestroyOnLoad(gameObject);

            Assert.IsNull(_instance);
            _instance = this;

            StartClient();
#if UNITY_EDITOR
            StartServer();
#endif
        }

        public void StartClient() {
            if (_client != null)
                return;

            var clientWorldGO = new GameObject("Client World");
            var clientWorld = clientWorldGO.AddComponent<World>();
            DontDestroyOnLoad(clientWorldGO);

            _client = CreateClient(clientWorld, lagSettings);
        }

        public void StartServer() {
            if (_server != null)
                return;

            var serverWorldGO = new GameObject("Server World");
            var serverWorld = serverWorldGO.AddComponent<World>();
            DontDestroyOnLoad(serverWorldGO);

            _server = CreateServer(serverWorld, replicaManagerSettings);
        }

        float _tickAccumulator;
        protected virtual void Update() {
            if (_client != null) {
                _client.Update();
            }
            if (_server != null) {
                _server.Update();
            }

            _tickAccumulator += Time.deltaTime;
            while (_tickAccumulator >= Tick.tickRate) {
                _tickAccumulator -= Tick.tickRate;
                ++Tick.tick;

                Pawn.TickAll();
            }
        }

        protected virtual void OnApplicationQuit() {
            if (_client != null) {
                _client.Shutdown();
            }
            if (_server != null) {
                _server.Shutdown();
            }
        }

        protected virtual ClientGame CreateClient(World world, ClientSimulatedLagSettings lagSettings) {
            return new ClientGame(world, lagSettings);
        }

        protected virtual ServerGame CreateServer(World world, ServerReplicaManagerSettings replicaManagerSettings) {
            return new ServerGame(world, replicaManagerSettings);
        }
    }
}