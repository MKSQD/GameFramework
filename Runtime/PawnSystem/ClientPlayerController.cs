using System.Collections.Generic;
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
        List<IBitSerializable> _commands = new(CommandBufferSize);
        IBitSerializable _lastAcceptedState;
        int _currentCommandIdx = 0;
        int _acceptedCommandIdx = 0;
        uint _acceptedFrame = 0;

        float _frameAcc;

        readonly ClientGame _client;

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

        IBitSerializable _lastLocalState, _currentLocalState;

        void UpdateCommands() {
            bool didMove = false;

            Input.Update();

            _frameAcc += Time.unscaledDeltaTime;
            while (_frameAcc >= Constants.FrameRate) {
                _frameAcc -= Constants.FrameRate;

                if (((_currentCommandIdx + 1) % CommandBufferSize) == _acceptedCommandIdx)
                    continue; // Queued commands full, wait

                var command = Pawn.ConsumeCommand();
                _commands[_currentCommandIdx] = command;
                _currentCommandIdx = (_currentCommandIdx + 1) % CommandBufferSize;

                didMove = true;
            }

            if (didMove && _lastAcceptedState != null) {
                Pawn.ResetToState(_lastAcceptedState);
                for (int i = _acceptedCommandIdx; i != _currentCommandIdx; i = (i + 1) % CommandBufferSize) {
                    Pawn.ExecuteCommand(_commands[i]);
                }

                _lastLocalState = _currentLocalState;
                _currentLocalState = Pawn.CreateState();
                Pawn.GetState(ref _currentLocalState);
            }

            if (_lastLocalState != null) {
                var a = _frameAcc / Constants.FrameRate;
                Pawn.InterpState(_lastLocalState, _currentLocalState, a);
            }
        }

        public override void Tick() {
            PossessReplica();
            SendCommands();
        }

        void SendCommands() {
            var numMoves = _currentCommandIdx >= _acceptedCommandIdx ? (_currentCommandIdx - _acceptedCommandIdx) : (CommandBufferSize + _currentCommandIdx - _acceptedCommandIdx);
            if (Pawn == null || numMoves == 0)
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

            _lastAcceptedState = Pawn.CreateState();
            _lastAcceptedState.Deserialize(bs);

            // Throw away old moves
            while (_acceptedFrame < acceptedFrame) {
                _acceptedCommandIdx = (_acceptedCommandIdx + 1) % CommandBufferSize;
                ++_acceptedFrame;
            }
        }

        public void OnPossessPawn(BitReader bs) {
            _currentReplicaToPossess = bs.ReadReplicaId();
            _pawnIdxToPossess = bs.ReadByte();
        }

        public override string ToString() => "ClientPlayerController";

        protected override void OnPossessed(Pawn pawn) {
            Input = new PlayerInput(pawn.InputMap);
            pawn.SetupPlayerInput(Input);
            pawn.InputMap.Enable();
        }

        protected override void OnUnpossessed() {
            Input.Dispose();
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