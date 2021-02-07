using Cube.Replication;
using Cube.Transport;
using UnityEngine.Assertions;

namespace GameFramework {
    public class PlayerController : PawnController {
        public PlayerInput input {
            get;
            internal set;
        }
        public Connection connection {
            get;
            internal set;
        }

        ReplicaView _replicaView;

        public PlayerController(Connection connection) {
            this.connection = connection;
        }

        public override void Update() {
            if (input != null) {
                input.Update();
            }

            if (_replicaView != null && pawn != null) {
                _replicaView.transform.position = pawn.transform.position;
            }
        }

        public override string ToString() {
            var s = connection != Connection.Invalid ? connection.ToString() : "Invalid";
            return "PlayerController(" + s + ")";
        }

        protected override void OnPossess(Pawn pawn) {
            if (pawn.isServer) {
                pawn.replica.AssignOwnership(connection);
                _replicaView = pawn.server.replicaManager.GetReplicaView(connection);
                SendPossession();
            }
            if (pawn.isClient) {
                input = new PlayerInput();
                SetupInputComponent(input);
                pawn.SetupPlayerInputComponent(input);
            }
        }

        protected override void OnUnpossess() {
            if (pawn.isServer) {
                pawn.replica.TakeOwnership();
            }
        }

        protected virtual void SetupInputComponent(PlayerInput input) {
        }

        void SendPossession() {
            var pawnIdx = byte.MaxValue;
            var pawnsOnReplica = pawn.replica.GetComponentsInChildren<Pawn>();
            for(int i = 0; i < pawnsOnReplica.Length; ++i) {
                var pawnOnReplica = pawnsOnReplica[i];
                if(pawn == pawnOnReplica) {
                    pawnIdx = (byte)i;
                    break;
                }
            }

            Assert.IsTrue(pawnIdx != byte.MaxValue);

            var bs = pawn.server.networkInterface.bitStreamPool.Create();
            bs.Write((byte)MessageId.PossessPawn);
            bs.Write(pawn.replica.ReplicaId);
            bs.Write(pawnIdx);

            pawn.server.networkInterface.SendBitStream(bs, PacketPriority.High, PacketReliability.ReliableSequenced, connection);
        }
    }
}