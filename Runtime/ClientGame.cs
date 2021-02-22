using Cube.Networking;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    public struct ClientGameContext {
        public World World;
        public SimulatedLagSettings LagSettings;
    }

    public class ClientGame {
        public CubeClient client {
            get;
            internal set;
        }
        public World world {
            get;
            internal set;
        }

        public static bool CharacterInputEnabled {
            get {
                return Application.isFocused;
            }
        }

        public UnityEvent onSceneLoadStart = new UnityEvent();

        byte _lastOnLoadSceneGeneration;

        ReplicaId currentReplicaPossess;
        byte pawnIdxToPossess;

        public ClientGame(ClientGameContext ctx) {
            Assert.IsNotNull(ctx.World);
            world = ctx.World;

            client = new CubeClient(ctx.World, ctx.LagSettings);

            client.networkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;
            client.networkInterface.Disconnected += OnDisconnected;

            client.reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            client.reactor.AddHandler((byte)MessageId.PossessPawn, OnPossessPawn);
        }

        public virtual void Update() {
            client.Update();

            if (currentReplicaPossess != ReplicaId.Invalid) {
                var replica = client.replicaManager.GetReplica(currentReplicaPossess);
                if (replica != null) {
                    var pawnsOnReplica = replica.GetComponentsInChildren<Pawn>();
                    if (pawnIdxToPossess >= pawnsOnReplica.Length) {
                        Debug.LogError("Oh shit, abort");
                        return;
                    }

                    var pawn = pawnsOnReplica[pawnIdxToPossess];

                    var pc = world.playerControllers[0];
                    if (pawn.Controller != pc) {
                        pc.Possess(pawn);
                        Debug.Log("[Client] <b>Possessed Pawn</b> <i>" + pawn + "</i> idx=" + pawnIdxToPossess, pawn);
                    }
                }
            }
        }

        public virtual void Shutdown() {
            client.Shutdown();
        }

        protected virtual PlayerController CreatePlayerController() {
            return new PlayerController(Connection.Invalid);
        }

        void OnConnectionRequestAccepted() {
            Debug.Log("[Client] Connection request to server accepted");

            var newPC = CreatePlayerController();
            world.playerControllers.Add(newPC);
        }

        void OnDisconnected(string reason) {
            Debug.Log("[Client] <b>Disconnected</b> (" + reason + ")");
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

            currentReplicaPossess = replicaId;
            pawnIdxToPossess = pawnIdx;
        }
    }
}