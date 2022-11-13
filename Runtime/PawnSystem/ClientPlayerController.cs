using System.IO;
using Cube;
using Cube.Replication;
using Cube.Transport;
using GameCore;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ClientPlayerController : PawnController {
        public PlayerInput Input { get; protected set; }

        ReplicaId _currentReplicaToPossess = ReplicaId.Invalid;
        byte _pawnIdxToPossess;


        IPawnState _lastAcceptedState;
        uint _lastAcceptedFrame;
        uint _currentFrame;

        readonly ClientGame _client;

        static readonly int CommandBufferSize = ServerPlayerController.CommandBufferSize;
        readonly RingBuffer<(uint, IPawnCommand)> _commandQueue;

        float _frameAcc;

        IAuthorativePawnMovement _authorativeMovement;

        public ClientPlayerController(ClientGame client) {
            _client = client;
            _commandQueue = new RingBuffer<(uint, IPawnCommand)>(CommandBufferSize);

            //File.Delete(@"C:\Users\Admin\Desktop\ClientPC.log");
        }

        public override void Update() {
            if (Pawn == null)
                return;

            Input.Update();
            if (_authorativeMovement != null) {
                UpdateAuthorativeMovement();
            }
        }

        void UpdateAuthorativeMovement() {
            _frameAcc += Time.deltaTime;
            while (_frameAcc >= Constants.FrameRate) {
                _frameAcc -= Constants.FrameRate;
                ++_currentFrame;

                if (_commandQueue.IsFull) {
                    //File.AppendAllText(@"C:\Users\Admin\Desktop\ClientPC.log", $"{_currentFrame,3}: Command queue full\n");
                    Debug.LogWarning("Command queue full");
                    return;
                }

                var command = _authorativeMovement.ConsumeCommand();
                _commandQueue.Enqueue((_currentFrame, command));

                _authorativeMovement.ExecuteCommand(command);
            }
        }

        public override void Tick() {
            PossessReplica();
            SendCommands();
        }

        void SendCommands() {
            if (Pawn == null || _commandQueue.Length == 0 || _authorativeMovement == null)
                return;

            // File.AppendAllText(@"C:\Users\Admin\Desktop\ClientPC.log", $"Sending\n");

            var moves = _commandQueue.ToArray();

            var bs = new BitWriter(64);
            bs.WriteByte((byte)MessageId.Commands);
            bs.WriteVector3(Pawn.transform.position);
            bs.WriteUInt(_currentFrame);
            bs.WriteIntInRange(moves.Length, 1, ServerPlayerController.CommandBufferSize);
            foreach (var move in moves) {
                move.Item2.Serialize(bs);
                //File.AppendAllText(@"C:\Users\Admin\Desktop\ClientPC.log", $"  {move.Item1}\n");
            }

            _client.NetworkInterface.Send(bs, PacketReliability.Unreliable, MessageChannel.Move);
        }

        public void OnCommandsAccepted(BitReader bs) {
            //File.AppendAllText(@"C:\Users\Admin\Desktop\ClientPC.log", $"Accepted\n");

            var acceptedFrame = bs.ReadUInt();
            _lastAcceptedState.Deserialize(bs);

            if (acceptedFrame <= _lastAcceptedFrame) {
                //Debug.Log($"Old update {acceptedFrame} <= {_lastAcceptedFrame}");
                return; // Make sure this is not an old update
            }
            //Debug.Log($"Update {acceptedFrame}");

            _lastAcceptedFrame = acceptedFrame;

            // Throw away old moves
            while (_commandQueue.Length > 0 && _commandQueue.Peek().Item1 <= acceptedFrame) {
                //File.AppendAllText(@"C:\Users\Admin\Desktop\ClientPC.log", $"  {_commandQueue.Peek().Item1} accepted\n");
                _commandQueue.Dequeue();
            }

            {
                var prevPos = Pawn.transform.position;
                var prevRot = Pawn.transform.rotation;

                // Reset to last good state
                _authorativeMovement.ResetToState(_lastAcceptedState);

                // Replay pending commands
                var pendingMoves = _commandQueue.ToArray();
                foreach (var pendingMove in pendingMoves) {
                    _authorativeMovement.ExecuteCommand(pendingMove.Item2);
                }

                if ((prevPos - Pawn.transform.position).magnitude < 0.1f) {
                    _authorativeMovement.Teleport(prevPos, prevRot);
                } else {
                    //DebugExt.DrawText(Pawn.transform.position, "Movement miss " + pendingMoves.Length, Color.red, 10);
                }
            }
        }

        BitReader _firstPawnState;
        public void OnPossessPawn(BitReader bs) {
            _currentReplicaToPossess = bs.ReadReplicaId();
            _pawnIdxToPossess = bs.ReadByte();
            _firstPawnState = bs.Clone();
        }

        public override string ToString() => "ClientPlayerController";

        protected override void OnPossessed(Pawn pawn) {
            _authorativeMovement = Pawn.GetComponent<IAuthorativePawnMovement>();
            _lastAcceptedState = _authorativeMovement != null ? _authorativeMovement.CreateState() : null;

            Input = new PlayerInput(pawn.InputMap);
            pawn.SetupPlayerInput(Input);
            pawn.InputMap.Enable();
        }

        protected override void OnUnpossessed() {
            Input.Dispose();
            Input = null;

            Pawn.InputMap.Disable();
        }

        void PossessReplica() {
            if (_currentReplicaToPossess == ReplicaId.Invalid)
                return;

            var replica = _client.ReplicaManager.GetReplica(_currentReplicaToPossess);
            if (replica == null)
                return;

            var pawnsOnReplica = replica.GetComponentsInChildren<Pawn>();
            if (_pawnIdxToPossess >= pawnsOnReplica.Length) {
                Debug.LogWarning("Invalid Pawn prossession idx");
                return;
            }

            var pawn = pawnsOnReplica[_pawnIdxToPossess];
            if (Possess(pawn)) {
                Debug.Log($"[Client] Possessed Pawn <i>{pawn.name}</i> Idx={_pawnIdxToPossess}", pawn);

                _currentReplicaToPossess = ReplicaId.Invalid;
                // Note: If we ever loose the Pawn we will NOT repossess it! This should be OK since we never timeout owned Replicas

                if (_authorativeMovement != null) {
                    // Now, apply first state
                    _authorativeMovement.DeserializeInitialState(_firstPawnState);
                }
                _firstPawnState = null;
            }
        }
    }
}