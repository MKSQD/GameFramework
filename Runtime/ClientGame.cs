using System.Collections;
using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
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

        ClientPlayerController _localPlayerController;

        protected override void Awake() {
            base.Awake();

            NetworkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;


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

        protected override void OnDisconnected() {
            _localPlayerController = null;
        }

        protected override void OnStartedLoadingMap(string mapName) {
            EventHub<StartedLoading>.Emit(new(mapName));
        }

        protected override void OnEndedLoadingMap() {
            EventHub<EndedLoadingEvent>.EmitDefault();
        }

        void OnConnectionRequestAccepted() {
            Debug.Log("[Client] Connection request to server accepted");

            _localPlayerController = new ClientPlayerController(this);
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/GameFramework/ClientGame", false, 10)]
        static void CreateCustomGameObject(MenuCommand menuCommand) {
            var go = new GameObject("Client Game");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
            go.AddComponent<ClientGame>();
        }
#endif
    }
}