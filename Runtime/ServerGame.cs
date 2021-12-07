using Cube.Networking;
using Cube.Replication;
using Cube.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;


namespace GameFramework {
    [Serializable]
    public class ConnectionEvent : UnityEvent<Connection> { }

    public struct ServerGameContext {
        public World World;
        public ushort Port;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        public SimulatedLagSettings LagSettings;
    }

    public class ServerGame : MonoBehaviour, IWorld {
        public event Action LoadSceneDone;

        public static ServerGame Main;

        public CubeServer Server {
            get;
            private set;
        }
        public IGameMode GameMode {
            get;
            private set;
        }
        [ReadOnly]
        public GameObject GameState;
        public List<PlayerController> PlayerControllers = new List<PlayerController>();

        public bool IsLoadingScene {
            get;
            internal set;
        }

        public ushort Port = 60000;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        public SimulatedLagSettings LagSettings;

        AsyncOperationHandle<SceneInstance> sceneHandle;
        string loadSceneName;
        byte loadSceneGeneration;
        byte numLoadScenePlayerAcks;

        protected virtual void Awake() {
            //var networkInterface = new LidgrenServerNetworkInterface(ctx.Settings.Port, ctx.LagSettings);
            var networkInterface = new LiteNetServerNetworkInterface(Port);
            Server = new CubeServer(transform, networkInterface, ReplicaManagerSettings);

            Server.NetworkInterface.ApproveConnection += OnApproveConnection;
            Server.NetworkInterface.NewConnectionEstablished += OnNewIncomingConnection;
            Server.NetworkInterface.DisconnectNotification += OnDisconnectNotification;
            Server.Reactor.AddHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);

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
            if (IsLoadingScene)
                throw new Exception("Cant start loading, current loading");

            // Cleanup
            if (GameMode != null) {
                GameMode.StartToLeaveMap();
            }

            IsLoadingScene = true;
            ++loadSceneGeneration;
            numLoadScenePlayerAcks = 0;
            loadSceneName = sceneName;

            Server.ReplicaManager.Reset();

            // Instruct clients
            var bs = new BitWriter();
            bs.Write((byte)MessageId.LoadScene);
            bs.Write(sceneName);
            bs.Write(loadSceneGeneration);

            Server.NetworkInterface.BroadcastBitStream(bs, PacketReliability.ReliableSequenced);

            // Disable ReplicaViews during level load
            foreach (var connection in Server.connections) {
                var replicaView = Server.ReplicaManager.GetReplicaView(connection);
                if (replicaView == null)
                    continue;

                replicaView.IsLoadingLevel = true;
            }

            // Unload old scene
            if (sceneHandle.IsValid()) {
                var op = Addressables.UnloadSceneAsync(sceneHandle);
                op.Completed += ctx => { LoadSceneImpl(); };
            } else {
#if UNITY_EDITOR
                var loadedScene = SceneManager.GetSceneByName(sceneName);
                if (loadedScene.isLoaded) {
                    var op = SceneManager.UnloadSceneAsync(loadedScene.name);
                    op.completed += ctx => { LoadSceneImpl(); };
                } else {
                    LoadSceneImpl();
                }
#else
                LoadSceneImpl();
#endif
            }
        }

        void LoadSceneImpl() {
            Debug.Log($"[Server] Loading level {loadSceneName}");

            sceneHandle = Addressables.LoadSceneAsync(loadSceneName, LoadSceneMode.Additive);
            sceneHandle.Completed += ctx => { OnSceneLoaded(); };
        }

        void OnSceneLoaded() {
            Debug.Log("[Server] <b>Level loaded</b>");

            IsLoadingScene = false;
            LoadSceneDone?.Invoke();

            GameMode = CreateGameModeForScene(loadSceneName);
            Assert.IsNotNull(GameMode);

            Debug.Log($"[Server] New GameMode <i>{GameMode}</i>");
        }

        void OnNewIncomingConnection(Connection connection) {
            Debug.Log($"[Server] <b>New connection</b> <i>{connection}</i>");

            // Send load scene packet if we loaded one previously
            if (loadSceneName != null) {
                var bs2 = new BitWriter();
                bs2.Write((byte)MessageId.LoadScene);
                bs2.Write(loadSceneName);
                bs2.Write(loadSceneGeneration);

                Server.NetworkInterface.Send(bs2, PacketReliability.ReliableSequenced, connection);
            }

            var newPC = CreatePlayerController(connection);
            PlayerControllers.Add(newPC);

            var replicaView = CreateReplicaView(connection);
            Server.ReplicaManager.AddReplicaView(replicaView);

            if (GameMode != null) { // null if there's no ongoing match
                GameMode.HandleNewPlayer(newPC);
            }
        }

        protected virtual PlayerController CreatePlayerController(Connection connection) {
            return new PlayerController(connection);
        }

        public PlayerController GetPlayerControllerForConnection(Connection connection) {
            foreach (var pc in PlayerControllers) {
                if (pc.Connection == connection)
                    return pc;
            }
            return null;
        }

        protected virtual void OnDisconnectNotification(Connection connection) {
            Debug.Log("[Server] Lost connection: " + connection);

            Server.ReplicaManager.RemoveReplicaView(connection);

            var pc = GetPlayerControllerForConnection(connection);
            PlayerControllers.Remove(pc);

            foreach (var replica in Server.ReplicaManager.Replicas) {
                if (replica.Owner == connection) {
                    replica.Destroy();
                }
            }
        }

        protected virtual void Update() {
            Server.Update();
            if (GameMode != null) {
                GameMode.Update();
            }
        }

        protected virtual void OnApplicationQuit() {
            Debug.Log("--- Shutdown ---");

            Server.Shutdown();
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
            if (generation != loadSceneGeneration)
                return;

            Debug.Log("[Server] On load scene done: <i>" + connection + "</i> (generation=" + generation + ")");

            ++numLoadScenePlayerAcks;

            //
            var replicaView = Server.ReplicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.IsLoadingLevel = false;
                Server.ReplicaManager.ForceReplicaViewRefresh(replicaView);
            }
        }
    }
}