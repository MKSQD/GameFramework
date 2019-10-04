using Cube.Networking;
using Cube.Replication;
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

        byte _lastOnLoadSceneGeneration;

        ReplicaId _todoReplicaPossess;
        byte _pawnIdxToPossess;

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

            client.reactor.AddHandler((byte)Cube.Transport.MessageId.ConnectionRequestAccepted, OnConnectionRequestAccepted);
            client.reactor.AddHandler((byte)Cube.Transport.MessageId.ConnectionRequestFailed, OnConnectionRequestFailed);
            client.reactor.AddHandler((byte)Cube.Transport.MessageId.DisconnectNotification, OnDisconnectNotification);
            client.reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            client.reactor.AddHandler((byte)MessageId.PossessPawn, OnPossessPawn);
        }

        public virtual void Update() {
            client.Update();

            if (_todoReplicaPossess != ReplicaId.Invalid) {
                var replica = client.replicaManager.GetReplicaById(_todoReplicaPossess);
                if (replica != null) {
                    var pawnsOnReplica = replica.GetComponentsInChildren<Pawn>();
                    var pawn = pawnsOnReplica[_pawnIdxToPossess];

                    var pc = world.playerControllers[0];
                    pc.Possess(pawn);

                    _todoReplicaPossess = ReplicaId.Invalid;

                    Debug.Log("[Client] Possessed Pawn <i>" + pawn + "</i> idx=" + _pawnIdxToPossess, pawn);
                }
            }
        }

        public virtual void Shutdown() {
            client.Shutdown();
        }

        protected virtual PlayerController CreatePlayerController() {
            return new DefaultPlayerController(Connection.Invalid);
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
            Debug.Log("[Client] <b>Disconnected</b>");
        }

        void OnLoadScene(BitStream bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            if (_lastOnLoadSceneGeneration == generation)
                return;

            _lastOnLoadSceneGeneration = generation;

            Debug.Log("[Client] <b>Loading level</b> '<i>" + sceneName + "</i>' (generation=" + generation + ")");

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

        void OnPossessPawn(BitStream bs) {
            var replicaId = bs.ReadReplicaId();
            var pawnIdx = bs.ReadByte();

            _todoReplicaPossess = replicaId;
            _pawnIdxToPossess = pawnIdx;
        }
    }
}