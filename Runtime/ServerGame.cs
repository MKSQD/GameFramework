using Cube.Networking;
using Cube.Replication;
using Cube.Transport;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement; // SceneManager
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [Serializable]
    public class ConnectionEvent : UnityEvent<Connection> {
    }

    public struct ServerGameContext {
        public World World;
        public ushort Port;
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

        AsyncOperationHandle<SceneInstance> sceneHandle;
        public bool IsLoadingScene {
            get;
            internal set;
        }
        string loadSceneName;
        byte loadSceneGeneration;
        byte numLoadScenePlayerAcks;

        public ServerGame(ServerGameContext ctx) {
            Assert.IsNotNull(ctx.World);
            world = ctx.World;

            //var networkInterface = new LidgrenServerNetworkInterface(ctx.Settings.Port, ctx.LagSettings);
            var networkInterface = new LiteNetServerNetworkInterface(ctx.Port);
            server = new CubeServer(ctx.World, networkInterface, ctx.ReplicaManagerSettings);

            server.NetworkInterface.ApproveConnection += OnApproveConnection;
            server.NetworkInterface.NewConnectionEstablished += OnNewIncomingConnection;
            server.NetworkInterface.DisconnectNotification += OnDisconnectNotification;
            server.Reactor.AddMessageHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
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
            // Cleanup
            if (gameMode != null) {
                gameMode.StartToLeaveMap();
            }

            IsLoadingScene = true;
            ++loadSceneGeneration;
            numLoadScenePlayerAcks = 0;
            loadSceneName = sceneName;

            server.ReplicaManager.Reset();
            if (sceneHandle.IsValid()) {
                Addressables.UnloadSceneAsync(sceneHandle);
            }



            // Instruct clients
            var bs = new BitStream();
            bs.Write((byte)MessageId.LoadScene);
            bs.Write(sceneName);
            bs.Write(loadSceneGeneration);

            server.NetworkInterface.BroadcastBitStream(bs, PacketReliability.ReliableSequenced);

            // Disable ReplicaViews during level load
            foreach (var connection in server.connections) {
                var replicaView = server.ReplicaManager.GetReplicaView(connection);
                if (replicaView == null)
                    continue;

                replicaView.IsLoadingLevel = true;
            }

            // Load new map
            Debug.Log($"[Server] Loading level {sceneName}");
#if UNITY_EDITOR
            if (!SceneManager.GetSceneByName(sceneName).isLoaded) {
#endif
                sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                sceneHandle.Completed += ctx => {
                    IsLoadingScene = false;
                };
#if UNITY_EDITOR
            } else {
                server.ReplicaManager.ProcessSceneReplicasInScene(SceneManager.GetSceneByName(sceneName));
                IsLoadingScene = false;
            }
#endif

            gameMode = CreateGameModeForScene(sceneName);
            Assert.IsNotNull(gameMode);

            Debug.Log($"[Server] New GameMode <i>{gameMode}</i>");
        }

        void OnNewIncomingConnection(Connection connection) {
            Debug.Log($"[Server] <b>New connection</b> <i>{connection}</i>");

            // Send load scene packet if we loaded one previously
            if (loadSceneName != null) {
                var bs2 = new BitStream();
                bs2.Write((byte)MessageId.LoadScene);
                bs2.Write(loadSceneName);
                bs2.Write(loadSceneGeneration);

                server.NetworkInterface.SendBitStream(bs2, PacketReliability.ReliableSequenced, connection);
            }

            var newPC = CreatePlayerController(connection);
            world.playerControllers.Add(newPC);

            var replicaView = CreateReplicaView(connection);
            server.ReplicaManager.AddReplicaView(replicaView);

            gameMode.HandleNewPlayer(newPC);
        }

        protected virtual PlayerController CreatePlayerController(Connection connection) {
            return new PlayerController(connection);
        }

        protected virtual void OnDisconnectNotification(Connection connection) {
            Debug.Log("[Server] Lost connection: " + connection);

            server.ReplicaManager.RemoveReplicaView(connection);

            var pc = world.GetPlayerController(connection);
            world.playerControllers.Remove(pc);

            foreach (var replica in server.ReplicaManager.Replicas) {
                if (replica.Owner == connection) {
                    replica.Destroy();
                }
            }
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

        protected virtual ReplicaView CreateReplicaView(Connection connection) {
            var view = new GameObject("ReplicaView " + connection);
            view.transform.parent = server.World.transform;

            var rw = view.AddComponent<ReplicaView>();
            rw.Connection = connection;

            return rw;
        }

        void OnLoadSceneDone(Connection connection, BitStream bs) {
            var generation = bs.ReadByte();
            if (generation != loadSceneGeneration)
                return;

            Debug.Log("[Server] On load scene done: <i>" + connection + "</i> (generation=" + generation + ")");

            ++numLoadScenePlayerAcks;

            //
            var replicaView = server.ReplicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.IsLoadingLevel = false;
                server.ReplicaManager.ForceReplicaViewRefresh(replicaView);
            }
        }
    }
}