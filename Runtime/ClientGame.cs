using System.Collections;
using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;


namespace GameFramework {
    public class StartedLoading : IEvent {
        public string SceneName;

        public StartedLoading(string sceneName) {
            SceneName = sceneName;
        }
    }
    public class EndedLoading : IEvent { }

    public class ClientGame : MonoBehaviour, IWorld {
        public static ClientGame Main;

        public CubeClient Client { get; private set; }
        [ReadOnly]
        public GameObject GameState;

        AsyncOperationHandle<SceneInstance> _sceneHandle;
        byte currentLoadedSceneGeneration;

        ClientPlayerController localPlayerController;

        public virtual bool PawnInputEnabled => true;

        protected virtual void Awake() {
            var transport = GetComponent<ITransport>();

            var networkInterface = transport.CreateClient();

            Client = new CubeClient(this, networkInterface);

            Client.NetworkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;
            Client.NetworkInterface.Disconnected += OnDisconnected;

            Client.Reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            Client.Reactor.AddHandler((byte)MessageId.PossessPawn, bs => localPlayerController.OnPossessPawn(bs));
            Client.Reactor.AddHandler((byte)MessageId.MoveCorrect, bs => localPlayerController.OnMoveCorrect(bs));

            Main = this;

#if UNITY_EDITOR
            for (var i = 0; i < SceneManager.sceneCount; ++i) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                Client.ReplicaManager.ProcessSceneReplicasInScene(scene);
            }
#endif
        }

        double _nextNetworkTick;

        protected virtual void Update() {
            Client.Update();
            localPlayerController?.Update();

            if (Time.timeAsDouble >= _nextNetworkTick) {
                _nextNetworkTick = Time.timeAsDouble + Constants.TickRate;

                Client.Tick();
                localPlayerController?.Tick();
            }
        }

        protected virtual void OnApplicationQuit() {
            Client.Shutdown();
        }

        void OnConnectionRequestAccepted() {
            Debug.Log("[Client] Connection request to server accepted");

            localPlayerController = new ClientPlayerController();
        }

        void OnDisconnected(string reason) {
            Debug.Log($"[Client] <b>Disconnected</b> ({reason})");

            // Cleanup
            localPlayerController = null;

            Client.ReplicaManager.Reset();

            if (_sceneHandle.IsValid()) {
                Addressables.UnloadSceneAsync(_sceneHandle);
            }
        }

        void OnLoadScene(BitReader bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            if (currentLoadedSceneGeneration != generation) {
                currentLoadedSceneGeneration = generation;

                StartCoroutine(LoadScene(sceneName));
            }
        }

        IEnumerator LoadScene(string sceneName) {
            Debug.Log($"[Client] <b>Loading scene</b> <i>{sceneName}</i>");

            EventHub<StartedLoading>.Emit(new(sceneName));

            Client.ReplicaManager.Reset();

            if (_sceneHandle.IsValid())
                yield return Addressables.UnloadSceneAsync(_sceneHandle);

            // New map
#if UNITY_EDITOR
            // Assume server loaded map already
            var scene = SceneManager.GetSceneByName(sceneName);
            Client.ReplicaManager.ProcessSceneReplicasInScene(scene);

            SendLoadSceneDone();
            EventHub<EndedLoading>.EmitDefault();
#else
            _sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            _sceneHandle.Completed += ctx => {
                Client.ReplicaManager.ProcessSceneReplicasInScene(ctx.Result.Scene);

                SendLoadSceneDone();
                EventHub<EndedLoading>.EmitDefault();
            };
#endif
        }

        void SendLoadSceneDone() {
            var bs = new BitWriter(1);
            bs.WriteByte((byte)MessageId.LoadSceneDone);
            bs.WriteByte(currentLoadedSceneGeneration);

            Client.NetworkInterface.Send(bs, PacketReliability.ReliableUnordered, MessageChannel.SceneLoad);
        }
    }
}