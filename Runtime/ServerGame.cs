using System;
using System.Collections.Generic;
using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEditor;
using UnityEngine;

namespace GameFramework {
    public struct PlayerJoinedEvent : IEvent {
        public readonly ServerPlayerController PlayerController;
        public PlayerJoinedEvent(ServerPlayerController playerController) {
            PlayerController = playerController;
        }
    }

    public abstract class ServerGame : CubeServer {
        public IGameMode GameMode { get; private set; }
        [ReadOnly]
        public GameObject GameState;
        public List<ServerPlayerController> PlayerControllers = new();






        protected override void Awake() {
            base.Awake();

            NetworkInterface.ApproveConnection += OnApproveConnection;
            Reactor.AddPacketHandler((byte)MessageId.Commands, OnCommands);
        }

        public abstract IGameMode CreateGameModeForScene(string sceneName);

        protected virtual ApprovalResult OnApproveConnection(BitReader bs) {
            return new ApprovalResult() { Approved = true };
        }

        protected virtual ServerPlayerController CreatePlayerController(ReplicaView view) {
            return new ServerPlayerController(view, this);
        }

        protected override void OnNewConnectionEstablished(Connection connection, ReplicaView replicaView) {
            var newPC = CreatePlayerController(replicaView);
            PlayerControllers.Add(newPC);

            ReplicaManager.AddReplicaView(replicaView);

            if (GameMode != null) { // null if there's no ongoing match
                GameMode.HandleNewPlayer(newPC);
            }

            EventHub<PlayerJoinedEvent>.Emit(new(newPC));
        }

        protected override void OnDisconnectNotification(Connection connection) {
            var playerController = GetPlayerControllerForConnection(connection);
            PlayerControllers.Remove(playerController);

            ReplicaManager.RemoveReplicaView(connection);

            if (GameMode != null) { // null if there's no ongoing match
                GameMode.HandleLeavingPlayer(playerController);
            }
        }

        public ServerPlayerController GetPlayerControllerForConnection(Connection connection) {
            foreach (var pc in PlayerControllers) {
                if (pc.Connection == connection)
                    return pc;
            }
            return null;
        }



        protected override void Update() {
            base.Update();

            if (GameMode != null) {
                GameMode.Update();
            }

            foreach (var pc in PlayerControllers) {
                pc.Update();
            }
        }

        protected override void Tick() {
            base.Tick();
            foreach (var pc in PlayerControllers) {
                pc.Tick();
            }
        }

        protected override void OnLeaveMap() {
            if (GameMode != null) {
                GameMode.StartToLeaveMap();
            }
        }

        protected override void OnMapLoaded() {
            GameMode = CreateGameModeForScene(CurrentMapName);
            if (GameMode == null)
                throw new Exception($"Failed to create GameMode for scene {CurrentMapName}");

            Debug.Log($"[Server] New GameMode <i>{GameMode}</i>");
        }

        void OnCommands(Connection connection, BitReader bs) {
            var pc = GetPlayerControllerForConnection(connection);
            if (pc == null)
                return;

            pc.OnCommands(connection, bs);
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/GameFramework/ServerGame", false, 10)]
        static void CreateCustomGameObject(MenuCommand menuCommand) {
            var go = new GameObject("Server Game");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
            go.AddComponent<ServerGame>();
        }
#endif
    }
}