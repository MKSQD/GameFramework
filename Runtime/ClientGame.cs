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

    public class ClientGame : CubeClient {
        public static ClientGame Main;

        [ReadOnly]
        public GameObject GameState;

        AsyncOperationHandle<SceneInstance> _sceneHandle;
        byte _currentLoadedSceneGeneration;

        ClientPlayerController _localPlayerController;

        public virtual bool PawnInputEnabled => true;

        protected override void Awake() {
            base.Awake();

            NetworkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;
            NetworkInterface.Disconnected += OnDisconnected;

            Reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            Reactor.AddHandler((byte)MessageId.PossessPawn, bs => _localPlayerController.OnPossessPawn(bs));
            Reactor.AddHandler((byte)MessageId.MoveCorrect, bs => _localPlayerController.OnMoveCorrect(bs));

            Main = this;

#if UNITY_EDITOR
            for (var i = 0; i < SceneManager.sceneCount; ++i) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                ReplicaManager.ProcessSceneReplicasInScene(scene);
            }
#endif
        }

        protected override void Update() {
            base.Update();

            _localPlayerController?.Update();
        }

        protected override void Tick() {
            base.Tick();

            _localPlayerController?.Tick();
        }

        void OnConnectionRequestAccepted() {
            Debug.Log("[Client] Connection request to server accepted");

            _localPlayerController = new ClientPlayerController();
        }

        void OnDisconnected(string reason) {
            Debug.Log($"[Client] <b>Disconnected</b> ({reason})");

            // Cleanup
            _localPlayerController = null;

            ReplicaManager.Reset();

            if (_sceneHandle.IsValid()) {
                Addressables.UnloadSceneAsync(_sceneHandle);
            }
        }

        void OnLoadScene(BitReader bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            if (_currentLoadedSceneGeneration != generation) {
                _currentLoadedSceneGeneration = generation;

                StartCoroutine(LoadScene(sceneName));
            }
        }

        IEnumerator LoadScene(string sceneName) {
            Debug.Log($"[Client] <b>Loading scene</b> <i>{sceneName}</i>");

            EventHub<StartedLoading>.Emit(new(sceneName));

            ReplicaManager.Reset();

            if (_sceneHandle.IsValid())
                yield return Addressables.UnloadSceneAsync(_sceneHandle);

            // New map
#if UNITY_EDITOR
            // Assume server loaded map already
            var scene = SceneManager.GetSceneByName(sceneName);
            ReplicaManager.ProcessSceneReplicasInScene(scene);

            SendLoadSceneDone();
            EventHub<EndedLoading>.EmitDefault();
#else
            _sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            _sceneHandle.Completed += ctx => {
                ReplicaManager.ProcessSceneReplicasInScene(ctx.Result.Scene);

                SendLoadSceneDone();
                EventHub<EndedLoading>.EmitDefault();
            };
#endif
        }

        void SendLoadSceneDone() {
            var bs = new BitWriter(1);
            bs.WriteByte((byte)MessageId.LoadSceneDone);
            bs.WriteByte(_currentLoadedSceneGeneration);

            NetworkInterface.Send(bs, PacketReliability.ReliableUnordered, MessageChannel.SceneLoad);
        }
    }
}