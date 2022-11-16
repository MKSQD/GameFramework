using Cube;
using GameCore;
using UnityEditor;
using UnityEngine;

namespace GameFramework {
    public class StartedLoading : IEvent {
        public string SceneName { get; }
        public StartedLoading(string sceneName) {
            SceneName = sceneName;
        }
    }
    public class EndedLoadingEvent : IEvent { }

    public class ClientGame : CubeClient {
        public static ClientGame Main;

        [ReadOnly]
        public GameObject GameState;

        public virtual bool PawnInputEnabled => true;

        ClientPlayerController _localPlayerController;

        protected override void Awake() {
            base.Awake();

            NetworkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;

            Reactor.AddPacketHandler((byte)MessageId.PossessPawn, bs => _localPlayerController.OnPossessPawn(bs));
            Reactor.AddPacketHandler((byte)MessageId.CommandsAccepted, bs => _localPlayerController.OnCommandsAccepted(bs));

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
            EventHub<EndedLoadingEvent>.Emit(new());
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