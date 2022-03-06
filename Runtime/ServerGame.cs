using System;
using System.Collections.Generic;
using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace GameFramework {
    public class ServerGame : CubeServer {
        public event Action SceneLoaded;

        public static ServerGame Main;

        public IGameMode GameMode { get; private set; }
        [ReadOnly]
        public GameObject GameState;
        public List<ServerPlayerController> PlayerControllers = new();

        public bool IsLoadingScene { get; private set; }





        AsyncOperationHandle<SceneInstance> _sceneHandle;
        string _loadSceneName;
        byte _loadSceneGeneration;
        byte _numLoadScenePlayerAcks;



        protected override void Awake() {
            base.Awake();

            NetworkInterface.ApproveConnection += OnApproveConnection;
            NetworkInterface.NewConnectionEstablished += OnNewIncomingConnection;
            NetworkInterface.DisconnectNotification += OnDisconnectNotification;
            Reactor.AddHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
            Reactor.AddHandler((byte)MessageId.Move, OnMove);

            Main = this;
        }

        public virtual IGameMode CreateGameModeForScene(string sceneName) {
            return new GameMode(this);
        }

        protected virtual ApprovalResult OnApproveConnection(BitReader bs) {
            return new ApprovalResult() { Approved = true };
        }

        /// <summary>
        /// Reset replication, instruct all clients to load the new scene, actually
        /// load the new scene on the server and finally create a new GameMode instance.
        /// </summary>
        /// <param name="sceneName"></param>
        public void LoadScene(string sceneName) {
            Assert.IsTrue(sceneName.Length > 0);

            if (IsLoadingScene)
                throw new Exception("Cant start loading, current loading");

            // Cleanup
            if (GameMode != null) {
                GameMode.StartToLeaveMap();
            }

            IsLoadingScene = true;
            ++_loadSceneGeneration;
            _numLoadScenePlayerAcks = 0;
            _loadSceneName = sceneName;

            ReplicaManager.Reset();

            BroadcastLoadScene(sceneName, _loadSceneGeneration);

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
                op.Completed += ctx => { LoadSceneImpl(); };
                return;
            }

#if UNITY_EDITOR
            var loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.isLoaded) {
                var op = SceneManager.UnloadSceneAsync(loadedScene);
                op.completed += ctx => { LoadSceneImpl(); };
                return;
            }
#endif

            LoadSceneImpl();
        }

        void BroadcastLoadScene(string sceneName, byte gen) {
            var bs = new BitWriter();
            bs.WriteByte((byte)MessageId.LoadScene);
            bs.WriteString(sceneName);
            bs.WriteByte(gen);

            NetworkInterface.BroadcastBitStream(bs, PacketReliability.ReliableSequenced, MessageChannel.SceneLoad);
        }

        void LoadSceneImpl() {
            Debug.Log($"[Server] Loading level {_loadSceneName}");

            _sceneHandle = Addressables.LoadSceneAsync(_loadSceneName, LoadSceneMode.Additive);
            _sceneHandle.Completed += ctx => { OnSceneLoaded(); };
        }

        void OnSceneLoaded() {
            Debug.Log("[Server] <b>Scene loaded</b>");

            IsLoadingScene = false;
            SceneLoaded?.Invoke();

            GameMode = CreateGameModeForScene(_loadSceneName);
            Assert.IsNotNull(GameMode);

            Debug.Log($"[Server] New GameMode <i>{GameMode}</i>");
        }

        protected virtual ServerPlayerController CreatePlayerController(ReplicaView view) {
            return new ServerPlayerController(view);
        }

        void OnNewIncomingConnection(Connection connection) {
            Debug.Log($"[Server] <b>New connection</b> <i>{connection}</i>");

            // Send load scene packet if we loaded one previously
            if (_loadSceneName != null) {
                var bs2 = new BitWriter();
                bs2.WriteByte((byte)MessageId.LoadScene);
                bs2.WriteString(_loadSceneName);
                bs2.WriteByte(_loadSceneGeneration);

                NetworkInterface.Send(bs2, PacketReliability.ReliableSequenced, connection, MessageChannel.SceneLoad);
            }

            var replicaView = CreateReplicaView(connection);

            var newPC = CreatePlayerController(replicaView);
            PlayerControllers.Add(newPC);

            ReplicaManager.AddReplicaView(replicaView);

            if (GameMode != null) { // null if there's no ongoing match
                GameMode.HandleNewPlayer(newPC);
            }
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
            if (generation != _loadSceneGeneration)
                return;

            Debug.Log($"[Server] On load scene done: <i>{connection}</i> (generation={generation})");

            ++_numLoadScenePlayerAcks;

            //
            var replicaView = ReplicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.IsLoadingLevel = false;
                ReplicaManager.ForceReplicaViewRefresh(replicaView);
            }
        }

        void OnMove(Connection connection, BitReader bs) {
            var pc = GetPlayerControllerForConnection(connection);
            if (pc == null)
                return;

            pc.OnMove(connection, bs);
        }
    }
}