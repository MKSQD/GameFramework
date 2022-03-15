using Cube.Replication;
using Cube.Transport;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ServerPlayerController : PawnController {
        public Connection Connection => _replicaView.Connection;

        readonly ReplicaView _replicaView;
        readonly ServerGame _server;

        public ServerPlayerController(ReplicaView view, ServerGame server) {
            _replicaView = view;
            _server = server;
        }

        public override void Update() {
            if (Pawn != null) {
                _replicaView.transform.position = Pawn.transform.position;
                _replicaView.transform.rotation = Pawn.transform.rotation;
            }
        }

        public override void Tick() {
        }

        uint _lastAcceptedFrame;
        public void OnCommands(Connection connection, BitReader bs) {
            var acceptedFrame = bs.ReadUInt();
            if (acceptedFrame < _lastAcceptedFrame)
                return;

            var num = bs.ReadIntInRange(1, 60);

            var lastMove = Pawn.CreateCommand();
            for (int i = 0; i < num; ++i) {
                lastMove.Deserialize(bs);
                Pawn.ExecuteCommand(lastMove);

                ++acceptedFrame;
            }

            {
                var state = Pawn.CreateState();
                Pawn.GetState(ref state);

                var bs2 = new BitWriter();
                bs2.WriteByte((byte)MessageId.CommandsAccepted);
                bs2.WriteUInt(acceptedFrame);
                state.Serialize(bs2);

                _server.NetworkInterface.Send(bs2, PacketReliability.Unreliable, connection);
            }

            _lastAcceptedFrame = acceptedFrame;
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