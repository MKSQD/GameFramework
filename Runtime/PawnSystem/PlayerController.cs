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

            if (replicaView != null && Pawn != null) {
                replicaView.transform.position = Pawn.transform.position;
                replicaView.transform.rotation = Pawn.transform.rotation;
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
            if (Pawn.isServer) {
                Pawn.Replica.TakeOwnership();
            }
            if (Pawn.isClient) {
                Pawn.InputMap.Disable();
            }
        }

        void SendPossession() {
            var pawnIdx = byte.MaxValue;
            var pawnsOnReplica = Pawn.Replica.GetComponentsInChildren<Pawn>();
            for (int i = 0; i < pawnsOnReplica.Length; ++i) {
                var pawnOnReplica = pawnsOnReplica[i];
                if (Pawn == pawnOnReplica) {
                    pawnIdx = (byte)i;
                    break;
                }
            }

            Assert.IsTrue(pawnIdx != byte.MaxValue);

            var bs = BitStreamPool.Create();
            bs.Write((byte)MessageId.PossessPawn);
            bs.Write(Pawn.Replica.Id);
            bs.Write(pawnIdx);

            Pawn.server.NetworkInterface.SendBitStream(bs, PacketPriority.High, PacketReliability.ReliableSequenced, Connection);
        }
    }
}