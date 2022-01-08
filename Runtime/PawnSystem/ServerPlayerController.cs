using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ServerPlayerController : PawnController {
        public Connection Connection => _replicaView.Connection;

        readonly ReplicaView _replicaView;

        public ServerPlayerController(ReplicaView view) {
            _replicaView = view;
        }

        public override void Update() {
            if (Pawn != null) {
                _replicaView.transform.position = Pawn.transform.position;
                _replicaView.transform.rotation = Pawn.transform.rotation;
            }
        }

        public override void Tick() {
        }

        float _lastMoveFirstTimestamp;
        public void OnMove(Connection connection, BitReader bs) {
            var num = bs.ReadIntInRange(1, 20);
            var firstTimestamp = bs.ReadFloat();
            if (firstTimestamp < _lastMoveFirstTimestamp)
                return;

            _lastMoveFirstTimestamp = firstTimestamp;

            IMove lastMove = null;
            for (int i = 0; i < num; ++i) {
                lastMove = Pawn.CreateMove();
                lastMove.DeserializeInput(bs);

                Pawn.ExecuteMove(lastMove);

                _lastMoveFirstTimestamp += Constants.TickRate;
            }

            {
                var bs2 = new BitWriter();
                bs2.WriteByte((byte)MessageId.MoveCorrect);
                bs2.WriteFloat(_lastMoveFirstTimestamp);
                lastMove.SerializeResult(bs2);

                ServerGame.Main.NetworkInterface.Send(bs2, PacketReliability.Unreliable, connection);
            }
        }

        protected override void OnPossessed(Pawn pawn) {
            pawn.Replica.AssignOwnership(Connection);
            SendPossession();
        }

        protected override void OnUnpossessed() {
            Pawn.Replica.TakeOwnership();
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

            var bs = new BitWriter();
            bs.WriteByte((byte)MessageId.PossessPawn);
            bs.WriteReplicaId(Pawn.Replica);
            bs.WriteByte(pawnIdx);

            Pawn.server.NetworkInterface.Send(bs, PacketReliability.ReliableSequenced, Connection, MessageChannel.SceneLoad);
        }

        public override string ToString() {
            var s = Connection != Connection.Invalid ? Connection.ToString() : "Invalid";
            return $"ServerPlayerController({s})";
        }
    }
}