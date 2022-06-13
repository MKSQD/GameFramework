using System.Collections.Generic;
using System.IO;
using Cube;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace GameFramework {
    public class ClientPlayerController : PawnController {
        public PlayerInput Input { get; protected set; }

        ReplicaId _currentReplicaToPossess = ReplicaId.Invalid;
        byte _pawnIdxToPossess;

        static readonly int CommandBufferSize = 60;
        readonly List<IBitSerializable> _commands = new(CommandBufferSize);
        IBitSerializable _lastAcceptedState;
        int _currentCommandIdx = 0;
        int _acceptedCommandIdx = 0;
        uint _acceptedFrame = 0;

        float _frameAcc;

        readonly ClientGame _client;

        IAuthorativePawnMovement _authorativeMovement;

        static void LogToFile(string txt) => File.AppendAllLines("D:/AuthClientMovement.log", new List<string>() { txt });

        public ClientPlayerController(ClientGame client) {
            _client = client;

            for (int i = 0; i < CommandBufferSize; ++i) {
                _commands.Add(null);
            }
        }

        public override void Update() {
            if (Pawn == null)
                return;

            UpdateCommands();
        }

        void UpdateCommands() {
            Input.Update();

            if (_authorativeMovement != null) {
                _frameAcc += Time.deltaTime;
                while (_frameAcc >= Constants.FrameRate) {
                    _frameAcc -= Constants.FrameRate;

                    if (((_currentCommandIdx + 1) % CommandBufferSize) == _acceptedCommandIdx)
                        continue; // Queued commands full, wait

                    var command = _authorativeMovement.ConsumeCommand();
                    _commands[_currentCommandIdx] = command;
                    _currentCommandIdx = (_currentCommandIdx + 1) % CommandBufferSize;

                    _authorativeMovement.BeforeCommands();
                    _authorativeMovement.ExecuteCommand(command);
                    _authorativeMovement.AfterCommands();
                    LogToFile("Client CMD");
                }
            }
        }

        public override void Tick() {
            PossessReplica();
            SendCommands();
        }

        void SendCommands() {
            var numMoves = _currentCommandIdx >= _acceptedCommandIdx ? (_currentCommandIdx - _acceptedCommandIdx) : (CommandBufferSize + _currentCommandIdx - _acceptedCommandIdx);
            if (Pawn == null || numMoves == 0 || _authorativeMovement == null)
                return;

            var bs = new BitWriter(64);
            bs.WriteByte((byte)MessageId.Commands);

            bs.WriteUInt(_acceptedFrame);
            bs.WriteIntInRange(numMoves, 1, 60);
            for (int i = _acceptedCommandIdx; i != _currentCommandIdx; i = (i + 1) % CommandBufferSize) {
                _commands[i].Serialize(bs);
            }

            _client.NetworkInterface.Send(bs, PacketReliability.Unreliable, MessageChannel.Move);
        }

        public void OnCommandsAccepted(BitReader bs) {
            var acceptedFrame = bs.ReadUInt();
            if (acceptedFrame < _acceptedFrame) {
                Debug.Log("Received old move correct");
                return;
            }

            _lastAcceptedState = _authorativeMovement.CreateState();
            _lastAcceptedState.Deserialize(bs);

            // Throw away old moves
            while (_acceptedFrame < acceptedFrame) {
                _acceptedCommandIdx = (_acceptedCommandIdx + 1) % CommandBufferSize;
                ++_acceptedFrame;
            }

            // Reset to last good state

            LogToFile("REPLAY");
            var oldPos = Pawn.transform.position;

            _authorativeMovement.BeforeCommands();
            _authorativeMovement.ResetToState(_lastAcceptedState);

            // Replay pending commands
            for (int i = _acceptedCommandIdx; i != _currentCommandIdx; i = (i + 1) % CommandBufferSize) {
                _authorativeMovement.ExecuteCommand(_commands[i]);
            }

            _authorativeMovement.AfterCommands();

            LogToFile($"Diff after replay: {(Pawn.transform.position - oldPos).magnitude:0.00}");
        }

        public void OnPossessPawn(BitReader bs) {
            _currentReplicaToPossess = bs.ReadReplicaId();
            _pawnIdxToPossess = bs.ReadByte();
        }

        public override string ToString() => "ClientPlayerController";

        protected override void OnPossessed(Pawn pawn) {
            _authorativeMovement = Pawn.GetComponent<IAuthorativePawnMovement>();

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
            }
        }
    }
}