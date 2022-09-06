using System;
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

        static readonly int CommandBufferSize = 30;
        readonly List<IPawnCommand> _commands = new(CommandBufferSize);
        IPawnState _lastAcceptedState;
        int _currentCommandIdx = 0;
        int _acceptedCommandIdx = 0;
        uint _acceptedFrame = 0;

        float _frameAcc;

        readonly ClientGame _client;

        IAuthorativePawnMovement _authorativeMovement;

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
            //


            // Gather input
            Input.Update();

            if (_authorativeMovement != null) {
                // Reset to last good state
                _authorativeMovement.ResetToState(_lastAcceptedState);

                // Replay pending commands
                for (int i = _acceptedCommandIdx; i != _currentCommandIdx; i = (i + 1) % CommandBufferSize) {
                    _authorativeMovement.ExecuteCommand(_commands[i]);
                }

                _frameAcc += Time.deltaTime;
                if (_frameAcc >= Constants.FrameRate) {
                    _frameAcc -= Constants.FrameRate;

                    var queueFull = ((_currentCommandIdx + 1) % CommandBufferSize) == _acceptedCommandIdx;
                    if (!queueFull) {
                        var command = _authorativeMovement.ConsumeCommand();
                        _commands[_currentCommandIdx] = command;
                        _currentCommandIdx = (_currentCommandIdx + 1) % CommandBufferSize;

                        _authorativeMovement.ExecuteCommand(command);
                    } else {
                        Debug.LogWarning("Command queue full");
                    }
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
            bs.WriteIntInRange(numMoves, 1, ServerPlayerController.NumMaxCommands);
            for (int i = _acceptedCommandIdx; i != _currentCommandIdx; i = (i + 1) % CommandBufferSize) {
                _commands[i].Serialize(bs);
            }

            _client.NetworkInterface.Send(bs, PacketReliability.Unreliable, MessageChannel.Move);
        }

        public void OnCommandsAccepted(BitReader bs) {
            var acceptedFrame = bs.ReadUInt();
            if (acceptedFrame < _acceptedFrame)
                return;

            _lastAcceptedState.Deserialize(bs);

            // Throw away old moves
            int num = 0;
            while (_acceptedFrame < acceptedFrame) {
                _acceptedCommandIdx = (_acceptedCommandIdx + 1) % CommandBufferSize;
                ++_acceptedFrame;
                ++num;
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

                // Now, apply first state
                _authorativeMovement.DeserializeInitialState(_firstPawnState);
                _firstPawnState = null;
            }
        }
    }
}