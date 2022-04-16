using System.Collections;
using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;


namespace GameFramework {
    public struct StartedLoading : IEvent {
        public string SceneName;
        public StartedLoading(string sceneName) {
            SceneName = sceneName;
        }
    }
    public struct EndedLoadingEvent : IEvent { }

    public class ClientGame : CubeClient {
        public static ClientGame Main;

        [ReadOnly]
        public GameObject GameState;

        public virtual bool PawnInputEnabled => true;

        AsyncOperationHandle<SceneInstance> _sceneHandle;
        byte _currentLoadedSceneGeneration;

        ClientPlayerController _localPlayerController;

        protected override void Awake() {
            base.Awake();

            NetworkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;
            NetworkInterface.Disconnected += OnDisconnected;

            Reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            Reactor.AddHandler((byte)MessageId.PossessPawn, bs => _localPlayerController.OnPossessPawn(bs));
            Reactor.AddHandler((byte)MessageId.CommandsAccepted, bs => _localPlayerController.OnCommandsAccepted(bs));

            Main = this;
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

            _localPlayerController = new ClientPlayerController(this);
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

#if UNITY_EDITOR
            // Assume server loaded map already
            var scene = SceneManager.GetSceneByName(sceneName);
#else
            _sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return _sceneHandle;
            var scene = _sceneHandle.Result.Scene;
#endif

            ReplicaManager.ProcessSceneReplicasInScene(scene);

            SendLoadSceneDone();
            EventHub<EndedLoadingEvent>.EmitDefault();
        }

        void SendLoadSceneDone() {
            var bs = new BitWriter(1);
            bs.WriteByte((byte)MessageId.LoadSceneDone);
            bs.WriteByte(_currentLoadedSceneGeneration);

            NetworkInterface.Send(bs, PacketReliability.ReliableUnordered, MessageChannel.SceneLoad);
        }

        [MenuItem("GameObject/GameFramework/ClientGame", false, 10)]
        static void CreateCustomGameObject(MenuCommand menuCommand) {
            var go = new GameObject("Client Game");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
            go.AddComponent<ClientGame>();
        }
    }
}