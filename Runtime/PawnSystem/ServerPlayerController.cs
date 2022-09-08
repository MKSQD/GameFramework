using Cube.Replication;
using Cube.Transport;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ServerPlayerController : PawnController {
        public static int NumMaxCommands = 10;

        public Connection Connection => _replicaView.Connection;

        readonly ReplicaView _replicaView;
        readonly ServerGame _server;
        IAuthorativePawnMovement _authorativeMovement;

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
        public void OnCommands(BitReader bs) {
            if (_authorativeMovement == null)
                return;

            ExecuteReceivedCommands(bs);
            SendStateToClient();
        }

        void ExecuteReceivedCommands(BitReader bs) {
            // #todo can be cheated by sending max commands with every packet

            var frame = bs.ReadUInt();
            var num = bs.ReadIntInRange(1, NumMaxCommands);

            var lastMove = _authorativeMovement.CreateCommand();
            for (int i = 0; i < num; ++i, ++frame) {
                lastMove.Deserialize(bs);
                if (frame <= _lastAcceptedFrame)
                    continue; // Old command -> ignore

                _authorativeMovement.ExecuteCommand(lastMove);
                _lastAcceptedFrame = frame;
            }
        }

        void SendStateToClient() {
            var state = _authorativeMovement.CreateState();
            _authorativeMovement.GetState(ref state);

            var bs2 = new BitWriter();
            bs2.WriteByte((byte)MessageId.CommandsAccepted);
            bs2.WriteUInt(_lastAcceptedFrame);
            state.Serialize(bs2);

            _server.NetworkInterface.SendPacket(bs2, PacketReliability.Unreliable, Connection);
        }

        protected override void OnPossessed(Pawn pawn) {
            _authorativeMovement = Pawn.GetComponent<IAuthorativePawnMovement>();
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


            if (_authorativeMovement != null) {
                // Make sure the player starts at a valid state (f.i. with the correct rotation)
                _authorativeMovement.SerializableInitialState(bs);
            }

            Pawn.server.NetworkInterface.SendPacket(bs, PacketReliability.ReliableSequenced, Connection, MessageChannel.SceneLoad);
        }

        public override string ToString() {
            var s = Connection != Connection.Invalid ? Connection.ToString() : "Invalid";
            return $"ServerPlayerController({s})";
        }
    }
}