using Cube.Replication;
using Cube.Transport;
using UnityEngine.Assertions;

namespace GameFramework {
    public class PlayerController : PawnController {
        public PlayerInput Input {
            get;
            internal set;
        }
        public Connection Connection {
            get;
            internal set;
        }

        ReplicaView replicaView;

        public PlayerController(Connection connection) {
            Connection = connection;
        }

        public override void Update() {
            Input?.Update();

            if (replicaView != null && pawn != null) {
                replicaView.transform.position = pawn.transform.position;
                replicaView.transform.rotation = pawn.transform.rotation;
            }
        }

        public override string ToString() {
            var s = Connection != Connection.Invalid ? Connection.ToString() : "Invalid";
            return "PlayerController(" + s + ")";
        }

        protected override void OnPossess(Pawn pawn) {
            if (pawn.isServer) {
                pawn.Replica.AssignOwnership(Connection);
                replicaView = pawn.server.ReplicaManager.GetReplicaView(Connection);
                SendPossession();
            }
            if (pawn.isClient) {
                Input = new PlayerInput(pawn.InputMap);
                pawn.SetupPlayerInputComponent(Input);
                pawn.InputMap.Enable();
            }
        }

        protected override void OnUnpossess() {
            if (pawn.isServer) {
                pawn.Replica.TakeOwnership();
            }
            if (pawn.isClient) {
                pawn.InputMap.Disable();
            }
        }

        void SendPossession() {
            var pawnIdx = byte.MaxValue;
            var pawnsOnReplica = pawn.Replica.GetComponentsInChildren<Pawn>();
            for (int i = 0; i < pawnsOnReplica.Length; ++i) {
                var pawnOnReplica = pawnsOnReplica[i];
                if (pawn == pawnOnReplica) {
                    pawnIdx = (byte)i;
                    break;
                }
            }

            Assert.IsTrue(pawnIdx != byte.MaxValue);

            var bs = BitStreamPool.Create();
            bs.Write((byte)MessageId.PossessPawn);
            bs.Write(pawn.Replica.Id);
            bs.Write(pawnIdx);

            pawn.server.NetworkInterface.SendBitStream(bs, PacketPriority.High, PacketReliability.ReliableSequenced, Connection);
        }
    }
}