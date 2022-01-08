using System.Collections.Generic;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace GameFramework {
    public class ClientPlayerController : PawnController {
        public PlayerInput Input { get; protected set; }

        ReplicaId _currentReplicaToPossess = ReplicaId.Invalid;
        byte _pawnIdxToPossess;

        Queue<MoveWrapper> _moveQueue = new();

        public override void Update() {
            if (Pawn == null)
                return;

            Input.Update();
        }

        public override void Tick() {
            PossessReplica();

            if (Pawn == null)
                return;

            while (_moveQueue.Count > 10) {
                _moveQueue.Dequeue();
            }

            var newMove = Pawn.ConsumeMove();

            var newMoveWrapper = new MoveWrapper() {
                Move = newMove,
                Timestamp = Time.time
            };
            _moveQueue.Enqueue(newMoveWrapper);

            Pawn.ExecuteMove(newMove);

            SendMove();
        }

        void SendMove() {
            if (Pawn == null || _moveQueue.Count == 0)
                return;

            var bs = new BitWriter(32);
            bs.WriteByte((byte)MessageId.Move);

            bs.WriteIntInRange(_moveQueue.Count, 1, 20);
            bs.WriteFloat(_moveQueue.Peek().Timestamp);
            foreach (var move in _moveQueue) {
                move.Move.SerializeInput(bs);
            }

            ClientGame.Main.NetworkInterface.Send(bs, PacketReliability.Unreliable, MessageChannel.Move);
        }

        public void OnMoveCorrect(BitReader bs) {
            var acceptedTime = bs.ReadFloat();

            {
                var move = Pawn.CreateMove();
                move.DeserializeResult(bs);

                Pawn.ResetToState(move);
            }

            // Throw away old moves
            int num = 0;

            while (_moveQueue.Count > 0) {
                var move = _moveQueue.Peek();
                if (move.Timestamp > acceptedTime)
                    break;

                _moveQueue.Dequeue();
                ++num;
            }

            // Replay moves
            foreach (var move in _moveQueue) {
                Pawn.ExecuteMove(move.Move);
                acceptedTime = move.Timestamp;
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
            _moveQueue.Clear();

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

    public interface IMove {
        void SerializeInput(IBitWriter bs);
        void DeserializeInput(BitReader bs);

        void SerializeResult(IBitWriter bs);
        void DeserializeResult(BitReader bs);
    }

    public class MoveWrapper {
        public float Timestamp;
        public IMove Move;
    }
}