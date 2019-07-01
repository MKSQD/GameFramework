using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    public class GameInstance : MonoBehaviour {
        public ServerReplicaManagerSettings replicaManagerSettings;

        ClientGame _client;
        ServerGame _server;

        protected virtual void Start() {
            DontDestroyOnLoad(gameObject);

            var clientTransform = new GameObject("Client");
            var serverTransform = new GameObject("Server");

            DontDestroyOnLoad(clientTransform);
            DontDestroyOnLoad(serverTransform);

            _client = CreateClient(clientTransform.transform);
            _server = CreateServer(serverTransform.transform, replicaManagerSettings);
        }

        protected virtual void Update() {
            _client.Update();
            _server.Update();
        }

        protected virtual void OnApplicationQuit() {
            _client.Shutdown();
            _server.Shutdown();
        }

        protected virtual ClientGame CreateClient(Transform transform) {
            return new ClientGame(transform);
        }

        protected virtual ServerGame CreateServer(Transform transform, ServerReplicaManagerSettings replicaManagerSettings) {
            return new ServerGame(transform, replicaManagerSettings);
        }
    }
}