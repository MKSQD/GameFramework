using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ServerPlayerController : PawnController {
        public const int CommandBufferSize = 10;

        public Connection Connection => _replicaView.Connection;

        readonly ReplicaView _replicaView;
        readonly ServerGame _server;
        IAuthorativePawnMovement _authorativeMovement;

        readonly IPawnCommand[] _commandQueue;
        readonly uint[] _commandFrame;
        int _currentCommandIdx;
        int _newCommandIdx;

        float _frameAcc;

        public ServerPlayerController(ReplicaView view, ServerGame server) {
            _replicaView = view;
            _server = server;
            _commandQueue = new IPawnCommand[CommandBufferSize];
            _commandFrame = new uint[CommandBufferSize];
        }

        public override void Tick() { }

        public override void Update() {
            if (Pawn != null) {
                _replicaView.transform.position = Pawn.transform.position;
                _replicaView.transform.rotation = Pawn.transform.rotation;
                if (_authorativeMovement != null) {
                    UpdateAuthorativeMovement();
                }
            }
        }

        void UpdateAuthorativeMovement() {
            _frameAcc += Time.deltaTime;
            if (_frameAcc >= Constants.FrameRate) {
                _frameAcc -= Constants.FrameRate;

                var command = _commandQueue[_currentCommandIdx];
                _authorativeMovement.ExecuteCommand(command);

                SendStateToClient();

                var currentCommandIdx = (_currentCommandIdx + 1) % CommandBufferSize;
                if (currentCommandIdx != _newCommandIdx) { // Only go to the next command if we have one queued
                    _currentCommandIdx = currentCommandIdx;
                }
            }
        }

        uint _lastAcceptedFrame;
        public void OnCommands(BitReader bs) {
            if (_authorativeMovement == null)
                return;

            var frame = bs.ReadUInt();
            var num = bs.ReadIntInRange(1, CommandBufferSize);

            var newMove = _authorativeMovement.CreateCommand();
            for (int i = 0; i < num; ++i, ++frame) {
                newMove.Deserialize(bs);
                if (frame <= _lastAcceptedFrame)
                    continue; // Old command -> ignore

                _commandQueue[_newCommandIdx] = newMove;
                _commandFrame[_newCommandIdx] = frame;
                _newCommandIdx = (_newCommandIdx + 1) % CommandBufferSize;

                _authorativeMovement.ExecuteCommand(newMove);
                _lastAcceptedFrame = frame;
            }
        }

        void SendStateToClient() {
            var state = _authorativeMovement.CreateState();
            _authorativeMovement.GetState(ref state);

            var bs2 = new BitWriter();
            bs2.WriteByte((byte)MessageId.CommandsAccepted);
            bs2.WriteUInt(_commandFrame[_currentCommandIdx]);
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