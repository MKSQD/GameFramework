using Cube.Networking;
using Cube.Replication;
using Cube.Transport;
using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.SceneManagement; // SceneManager
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [Serializable]
    public class ConnectionEvent : UnityEvent<Connection> {
    }

    public struct ServerGameContext {
        public ushort Port;
        public World World;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        public SimulatedLagSettings LagSettings;
    }

    public class ServerGame {
        public CubeServer server {
            get;
            internal set;
        }
        public IGameMode gameMode {
            get;
            internal set;
        }
        public World world {
            get;
            internal set;
        }

        public ConnectionEvent onNewIncomingConnection = new ConnectionEvent();
        public ConnectionEvent onDisconnectionNotification = new ConnectionEvent();
        public UnityEvent onAllClientsLoadedScene = new UnityEvent();

        string _loadSceneName;
        byte _loadSceneGeneration;
        byte _loadScenePlayerAcks;
        bool _onAllClientsLoadedSceneTriggeredThisGeneration;

        public ServerGame(ServerGameContext ctx) {
            Assert.IsNotNull(ctx.World);
            world = ctx.World;

            server = new CubeServer(ctx.Port, ctx.World, ctx.LagSettings, ctx.ReplicaManagerSettings);

            server.networkInterface.ApproveConnection += OnApproveConnection;
            server.networkInterface.NewConnectionEstablished += OnNewIncomingConnection;
            server.networkInterface.DisconnectNotification += OnDisconnectNotification;
            server.reactor.AddMessageHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
        }

        public virtual IGameMode CreateGameModeForScene(string sceneName) {
            return new GameMode(this);
        }

        protected virtual ApprovalResult OnApproveConnection(BitStream bs) {
            return new ApprovalResult() { Approved = true };
        }

        /// <summary>
        /// Reset replication, instruct all clients to load the new scene, actually
        /// load the new scene on the server and finally create a new GameMode instance.
        /// </summary>
        /// <param name="sceneName"></param>
        public void LoadScene(string sceneName) {
            if (gameMode != null) {
                gameMode.StartToLeaveMap();
            }

            ++_loadSceneGeneration;
            _loadScenePlayerAcks = 0;
            _loadSceneName = sceneName;
            _onAllClientsLoadedSceneTriggeredThisGeneration = false;

            server.replicaManager.Reset();

            var bs = new BitStream();
            bs.Write((byte)MessageId.LoadScene);
            bs.Write(sceneName); // #todo send scene idx instead
            bs.Write(_loadSceneGeneration);

            server.networkInterface.BroadcastBitStream(bs, PacketPriority.High, PacketReliability.ReliableSequenced);

            // Disable Replicas during level load
            foreach (var connection in server.connections) {
                var replicaView = server.replicaManager.GetReplicaView(connection);
                if (replicaView == null)
                    continue;

                replicaView.IsLoadingLevel = true;
            }

#if !UNITY_EDITOR
            Debug.Log("[Server] Loading level " + sceneName);
            SceneManager.LoadScene(sceneName);
#endif

            gameMode = CreateGameModeForScene(sceneName);
            Assert.IsNotNull(gameMode);

            Debug.Log("[Server] <b>New GameMode</b> <i>" + gameMode + "</i>");
        }

        void OnNewIncomingConnection(Connection connection) {
            Debug.Log("[Server] <b>New connection</b> <i>" + connection + "</i>");

            // Send load scene packet if we loaded one previously
            if (_loadSceneName != null) {
                var bs2 = new BitStream();
                bs2.Write((byte)MessageId.LoadScene);
                bs2.Write(_loadSceneName);
                bs2.Write(_loadSceneGeneration);

                server.networkInterface.SendBitStream(bs2, PacketPriority.High, PacketReliability.ReliableSequenced, connection);
            }

            var newPC = CreatePlayerController(connection);
            world.playerControllers.Add(newPC);

            CreateReplicaView(connection);

            gameMode.HandleNewPlayer(newPC);

            onNewIncomingConnection.Invoke(connection);
        }

        protected virtual PlayerController CreatePlayerController(Connection connection) {
            return new PlayerController(connection);
        }

        protected virtual void OnDisconnectNotification(Connection connection) {
            Debug.Log("[Server] Lost connection: " + connection);

            onDisconnectionNotification.Invoke(connection);

            server.replicaManager.RemoveReplicaView(connection);

            OnNumReadyClientsChanged();
        }

        public virtual void Update() {
            server.Update();
            if (gameMode != null) {
                gameMode.Update();
            }
        }

        public virtual void Shutdown() {
            server.Shutdown();
        }

        void CreateReplicaView(Connection connection) {
            var view = new GameObject("ReplicaView " + connection);
            view.transform.parent = server.world.transform;

            var rw = view.AddComponent<ReplicaView>();
            rw.Connection = connection;

            server.replicaManager.AddReplicaView(rw);
        }

        void OnLoadSceneDone(Connection connection, BitStream bs) {
            var generation = bs.ReadByte();
            if (generation != _loadSceneGeneration)
                return;

            Debug.Log("[Server] On load scene done: <i>" + connection + "</i> (generation=" + generation + ")");

            ++_loadScenePlayerAcks;

            OnNumReadyClientsChanged();

            //
            var replicaView = server.replicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.IsLoadingLevel = false;
                server.replicaManager.ForceReplicaViewRefresh(replicaView);
            }
        }

        void OnNumReadyClientsChanged() {
            if (!_onAllClientsLoadedSceneTriggeredThisGeneration && _loadScenePlayerAcks >= server.connections.Count) {
                _onAllClientsLoadedSceneTriggeredThisGeneration = true;
                onAllClientsLoadedScene.Invoke();
            }
        }
    }
}