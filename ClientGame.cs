using Cube.Networking;
using Cube.Transport;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    public class ClientGame {
        public bool connectInEditor = true;
        public ushort portInEditor = 60000;

        public CubeClient client {
            get;
            internal set;
        }
        public World world {
            get;
            internal set;
        }

        public UnityEvent onSceneLoadStart = new UnityEvent();

        public ClientGame(World world, ClientSimulatedLagSettings lagSettings) {
            if (world == null)
                throw new ArgumentNullException("world");

            this.world = world;

            client = new CubeClient(world.transform, lagSettings);

#if UNITY_EDITOR
            if (connectInEditor) {
                client.networkInterface.Connect("127.0.0.1", portInEditor);
            }
#endif

            client.reactor.AddHandler((byte)MessageId.ConnectionRequestAccepted, OnConnectionRequestAccepted);
            client.reactor.AddHandler((byte)MessageId.ConnectionRequestFailed, OnConnectionRequestFailed);
            client.reactor.AddHandler((byte)MessageId.DisconnectNotification, OnDisconnectNotification);
            client.reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
        }

        public virtual void Update() {
            client.Update();
        }

        public virtual void Shutdown() {
            client.Shutdown();
        }

        void OnConnectionRequestAccepted(BitStream bs) {
            Debug.Log("[Client] Connection request to server accepted");

            var newPC = CreatePlayerController();
            world.playerControllers.Add(newPC);
        }

        void OnConnectionRequestFailed(BitStream bs) {
            Debug.Log("Connection request to server failed");
        }

        void OnDisconnectNotification(BitStream bs) {
            Debug.Log("Disconnected");

            // localPlayerController = null;
        }

        void OnLoadScene(BitStream bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            Debug.Log("[Client] Loading level '" + sceneName + "' (generation=" + generation + ")");

            client.replicaManager.Reset();

            onSceneLoadStart.Invoke();

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
                return; // See log for errors

            op.completed += _ => {
                var bs2 = new BitStream();
                bs2.Write((byte)MessageId.LoadSceneDone);
                bs2.Write(generation);

                client.networkInterface.Send(bs2, PacketPriority.High, PacketReliability.Reliable);
            };
        }

        protected virtual PlayerController CreatePlayerController() {
            return new DefaultPlayerController(Connection.Invalid);
        }
    }
}