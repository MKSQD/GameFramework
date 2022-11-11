using System.Collections.Generic;
using System.IO;
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

        uint _frame;

        public ServerPlayerController(ReplicaView view, ServerGame server) {
            _replicaView = view;
            _server = server;

            File.Delete(@"C:\Users\Admin\Desktop\ServerPC.log");
        }

        public override void Tick() { }

        public override void Update() {
            if (Pawn == null)
                return;

            _replicaView.transform.position = Pawn.transform.position;
            _replicaView.transform.rotation = Pawn.transform.rotation;
        }

        public void OnCommands(BitReader bs) {
            if (Pawn == null || _authorativeMovement == null)
                return;

            var pos = bs.ReadVector3();
            var lastFrame = bs.ReadUInt();
            var num = bs.ReadIntInRange(1, CommandBufferSize);

            File.AppendAllText(@"C:\Users\Admin\Desktop\ServerPC.log", $"Received\n");

            for (int i = 0; i < num; ++i) {
                var newMove = _authorativeMovement.CreateCommand();
                newMove.Deserialize(bs);

                var frame = (lastFrame - (num - 1)) + i;
                if (frame <= _frame) {
                    File.AppendAllText(@"C:\Users\Admin\Desktop\ServerPC.log", $"  {frame} discarded\n");
                    continue; // Old command -> ignore
                }

                _authorativeMovement.ExecuteCommand(newMove);

                _frame = (uint)frame;

                File.AppendAllText(@"C:\Users\Admin\Desktop\ServerPC.log", $"  {frame} ok\n");
            }

            var diffToClient = (pos - Pawn.transform.position).magnitude;
            if (diffToClient < 0.25f) {
                File.AppendAllText(@"C:\Users\Admin\Desktop\ServerPC.log", $"  HUGE divergance from client ({diffToClient})\n");

                _authorativeMovement.Teleport(pos, Pawn.transform.rotation);
            } else {
                //DebugExt.DrawText(Pawn.transform.position + Vector3.up * 0.1f, "Movement miss", Color.blue, 10);
            }

            SendStateToClient();
        }

        void SendStateToClient() {
            var state = _authorativeMovement.CreateState();
            _authorativeMovement.GetState(ref state);

            var bs2 = new BitWriter();
            bs2.WriteByte((byte)MessageId.CommandsAccepted);
            bs2.WriteUInt(_frame);
            state.Serialize(bs2);

            _server.NetworkInterface.SendPacket(bs2, PacketReliability.Unreliable, Connection);
        }

        protected override void OnPossessed(Pawn pawn) {
            _authorativeMovement = pawn.GetComponent<IAuthorativePawnMovement>();
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