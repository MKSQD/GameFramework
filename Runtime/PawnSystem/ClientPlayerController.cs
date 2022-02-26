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

        static readonly int MoveBufferSize = 60;
        List<IBitSerializable> _moves = new(MoveBufferSize);
        IBitSerializable _lastAcceptedState;
        int _currentBufferIdx = 0;
        int _acceptedBufferIdx = 0;
        uint _acceptedFrame = 0;

        float _frameAcc;

        public ClientPlayerController() {
            for (int i = 0; i < MoveBufferSize; ++i) {
                _moves.Add(null);
            }
        }

        public override void Update() {
            if (Pawn == null)
                return;

            Input.Update();
            UpdateCurrentMove();
        }

        IBitSerializable _lastLocalState, _currentLocalState;

        void UpdateCurrentMove() {
            bool didMove = false;

            _frameAcc += Time.deltaTime;
            while (_frameAcc >= Constants.FrameRate) {
                _frameAcc -= Constants.FrameRate;

                if (((_currentBufferIdx + 1) % MoveBufferSize) == _acceptedBufferIdx) {
                    Debug.Log("Move buffer exhausted");
                    continue; // Move buffer exhausted
                }

                var move = Pawn.ConsumeMove();
                _moves[_currentBufferIdx] = move;
                _currentBufferIdx = (_currentBufferIdx + 1) % MoveBufferSize;

                didMove = true;
            }

            if (didMove && _lastAcceptedState != null) {
                Pawn.ResetToState(_lastAcceptedState);
                for (int i = _acceptedBufferIdx; i != _currentBufferIdx; i = (i + 1) % MoveBufferSize) {
                    Pawn.ExecuteMove(_moves[i]);
                }

                _lastLocalState = _currentLocalState;
                _currentLocalState = Pawn.CreateState();
                Pawn.GetState(ref _currentLocalState);
            }


            var a = _frameAcc / Constants.FrameRate;
            Pawn.InterpState(_lastLocalState, _currentLocalState, a);
        }

        public override void Tick() {
            PossessReplica();
            SendMoves();
        }

        void SendMoves() {
            var numMoves = _currentBufferIdx >= _acceptedBufferIdx ? (_currentBufferIdx - _acceptedBufferIdx) : (MoveBufferSize + _currentBufferIdx - _acceptedBufferIdx);
            if (Pawn == null || numMoves == 0)
                return;

            var bs = new BitWriter(64);
            bs.WriteByte((byte)MessageId.Move);

            bs.WriteUInt(_acceptedFrame);
            bs.WriteIntInRange(numMoves, 1, 60);
            for (int i = _acceptedBufferIdx; i != _currentBufferIdx; i = (i + 1) % MoveBufferSize) {
                _moves[i].Serialize(bs);
            }

            ClientGame.Main.NetworkInterface.Send(bs, PacketReliability.Unreliable, MessageChannel.Move);
        }

        public void OnMoveCorrect(BitReader bs) {
            var acceptedFrame = bs.ReadUInt();
            if (acceptedFrame < _acceptedFrame) {
                Debug.Log("Received old move correct");
                return;
            }

            _lastAcceptedState = Pawn.CreateState();
            _lastAcceptedState.Deserialize(bs);

            // Throw away old moves
            while (_acceptedFrame < acceptedFrame) {
                _acceptedBufferIdx = (_acceptedBufferIdx + 1) % MoveBufferSize;
                ++_acceptedFrame;
            }
        }

        public void OnPossessPawn(BitReader bs) {
            _currentReplicaToPossess = bs.ReadReplicaId();
            _pawnIdxToPossess = bs.ReadByte();
        }

        protected override void OnPossessed(Pawn pawn) {
            Input = new PlayerInput(pawn.InputMap);
            pawn.SetupPlayerInputComponent(Input);
            pawn.InputMap.Enable();
        }

        protected override void OnUnpossessed() {
            Input.Dispose();

            Pawn.InputMap.Disable();
        }

        void PossessReplica() {
            if (_currentReplicaToPossess == ReplicaId.Invalid)
                return;

            var replica = ClientGame.Main.ReplicaManager.GetReplica(_currentReplicaToPossess);
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

        public override string ToString() => "ClientPlayerController";
    }
}