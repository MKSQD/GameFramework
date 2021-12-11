using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ServerPlayerController : PlayerController {
        public Connection Connection => _replicaView.Connection;

        ReplicaView _replicaView;

        public ServerPlayerController(ReplicaView view) {
            _replicaView = view;
        }

        public override void Update() {
            if (Pawn != null) {
                _replicaView.transform.position = Pawn.transform.position;
                _replicaView.transform.rotation = Pawn.transform.rotation;
            }
        }

        float baseTime;
        public void OnMove(Connection connection, BitReader bs) {
            var num = bs.ReadIntInRange(1, 20);

            for (int i = 0; i < num; ++i) {
                var newMove = Pawn.ReadMove(bs);
                if (newMove.GetTime() < baseTime)
                    return;

                var t = newMove.GetTime() - baseTime;
                Pawn.ExecuteMove(newMove, t);

                baseTime = newMove.GetTime();
            }

            var bs2 = new BitWriter();
            bs2.Write((byte)MessageId.MoveCorrect);
            bs2.Write(baseTime);
            Pawn.WriteMoveResult(bs2);

            ServerGame.Main.Server.NetworkInterface.Send(bs2, PacketReliability.Unreliable, connection);
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
            bs.Write((byte)MessageId.PossessPawn);
            bs.Write(Pawn.Replica.Id);
            bs.Write(pawnIdx);

            Pawn.server.NetworkInterface.Send(bs, PacketReliability.ReliableSequenced, Connection, MessageChannel.SceneLoad);
        }

        public override string ToString() {
            var s = Connection != Connection.Invalid ? Connection.ToString() : "Invalid";
            return "ServerPlayerController(" + s + ")";
        }
    }
}