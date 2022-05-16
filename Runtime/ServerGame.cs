using System;
using System.Collections.Generic;
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
    public struct PlayerJoinedEvent : IEvent {
        public readonly ServerPlayerController PlayerController;
        public PlayerJoinedEvent(ServerPlayerController playerController) {
            PlayerController = playerController;
        }
    }

    public abstract class ServerGame : CubeServer {
        public event Action SceneLoaded;

        public IGameMode GameMode { get; private set; }
        [ReadOnly]
        public GameObject GameState;
        public List<ServerPlayerController> PlayerControllers = new();

        public bool IsLoadingMap { get; private set; }
        public string CurrentMapName { get; private set; }


        AsyncOperationHandle<SceneInstance> _sceneHandle;
        byte _loadMapGeneration;


        protected override void Awake() {
            base.Awake();

            NetworkInterface.ApproveConnection += OnApproveConnection;
            NetworkInterface.NewConnectionEstablished += OnNewIncomingConnection;
            NetworkInterface.DisconnectNotification += OnDisconnectNotification;
            Reactor.AddHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
            Reactor.AddHandler((byte)MessageId.Commands, OnCommands);
        }

        public abstract IGameMode CreateGameModeForScene(string sceneName);

        protected virtual ApprovalResult OnApproveConnection(BitReader bs) {
            return new ApprovalResult() { Approved = true };
        }

        public void ReloadCurrentMap() => LoadMap(CurrentMapName);

        /// <summary>
        /// Reset replication, instruct all clients to load the new scene, actually
        /// load the new scene on the server and finally create a new GameMode instance.
        /// </summary>
        /// <param name="name"></param>
        public void LoadMap(string name) {
            Debug.Log($"[Server] Loading map <i>{name}</i>...");

            Assert.IsTrue(name.Length > 0);

            if (IsLoadingMap)
                throw new Exception("Cant start loading, current loading");

            // Cleanup
            if (GameMode != null) {
                GameMode.StartToLeaveMap();
            }

            IsLoadingMap = true;
            ++_loadMapGeneration;
            CurrentMapName = name;

            ReplicaManager.Reset();

            BroadcastLoadMap(name, _loadMapGeneration);

            // Disable ReplicaViews during level load
            foreach (var connection in Connections) {
                var replicaView = ReplicaManager.GetReplicaView(connection);
                if (replicaView != null) {
                    replicaView.IsLoadingLevel = true;
                }
            }

            // Unload old scene
            if (_sceneHandle.IsValid()) {
                var op = Addressables.UnloadSceneAsync(_sceneHandle);
                op.Completed += ctx => { LoadMapImpl(); };
                return;
            }

#if UNITY_EDITOR
            var loadedScene = SceneManager.GetSceneByName(name);
            if (loadedScene.isLoaded) {
                var op = SceneManager.UnloadSceneAsync(loadedScene);
                op.completed += ctx => { LoadMapImpl(); };
                return;
            }
#endif

            LoadMapImpl();
        }

        void BroadcastLoadMap(string sceneName, byte gen) {
            var bs = new BitWriter();
            bs.WriteByte((byte)MessageId.LoadScene);
            bs.WriteString(sceneName);
            bs.WriteByte(gen);

            NetworkInterface.BroadcastBitStream(bs, PacketReliability.ReliableSequenced, MessageChannel.SceneLoad);
        }

        void LoadMapImpl() {
            _sceneHandle = Addressables.LoadSceneAsync(CurrentMapName, LoadSceneMode.Additive);
            _sceneHandle.Completed += ctx => { OnMapLoaded(); };
        }

        void OnMapLoaded() {
            Debug.Log("[Server] Map loaded");

            IsLoadingMap = false;
            SceneLoaded?.Invoke();

            GameMode = CreateGameModeForScene(CurrentMapName);
            if (GameMode == null)
                throw new Exception("Failed to create GameMode for scene " + CurrentMapName);

            Debug.Log($"[Server] New GameMode <i>{GameMode}</i>");
        }

        protected virtual ServerPlayerController CreatePlayerController(ReplicaView view) {
            return new ServerPlayerController(view, this);
        }

        void OnNewIncomingConnection(Connection connection) {
            Debug.Log($"[Server] New connection <i>{connection}</i>");

            // Send load scene packet if we loaded one previously
            if (CurrentMapName != null) {
                var bs2 = new BitWriter();
                bs2.WriteByte((byte)MessageId.LoadScene);
                bs2.WriteString(CurrentMapName);
                bs2.WriteByte(_loadMapGeneration);

                NetworkInterface.Send(bs2, PacketReliability.ReliableSequenced, connection, MessageChannel.SceneLoad);
            }

            var replicaView = CreateReplicaView(connection);

            var newPC = CreatePlayerController(replicaView);
            PlayerControllers.Add(newPC);

            ReplicaManager.AddReplicaView(replicaView);

            if (GameMode != null) { // null if there's no ongoing match
                GameMode.HandleNewPlayer(newPC);
            }

            EventHub<PlayerJoinedEvent>.Emit(new(newPC));
        }

        public ServerPlayerController GetPlayerControllerForConnection(Connection connection) {
            foreach (var pc in PlayerControllers) {
                if (pc.Connection == connection)
                    return pc;
            }
            return null;
        }

        protected virtual void OnDisconnectNotification(Connection connection) {
            Debug.Log("[Server] Lost connection: " + connection);

            ReplicaManager.RemoveReplicaView(connection);

            var pc = GetPlayerControllerForConnection(connection);
            PlayerControllers.Remove(pc);

            foreach (var replica in ReplicaManager.Replicas) {
                if (replica.Owner == connection) {
                    replica.Destroy();
                }
            }
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

        protected virtual ReplicaView CreateReplicaView(Connection connection) {
            var view = new GameObject("ReplicaView " + connection);
            view.transform.parent = transform;

            var rw = view.AddComponent<ReplicaView>();
            rw.Connection = connection;

            return rw;
        }

        void OnLoadSceneDone(Connection connection, BitReader bs) {
            var generation = bs.ReadByte();
            if (generation != _loadMapGeneration)
                return;

            Debug.Log($"[Server] Client <i>{connection}</i> done loading scene (generation={generation})");

            //
            var replicaView = ReplicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.IsLoadingLevel = false;
                ReplicaManager.ForceReplicaViewRefresh(replicaView);
            }
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