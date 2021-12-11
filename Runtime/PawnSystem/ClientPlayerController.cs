using System.Collections.Generic;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    public class ClientPlayerController : PlayerController {
        ReplicaId _currentReplicaToPossess = ReplicaId.Invalid;
        byte _pawnIdxToPossess;

        Queue<IMove> moveQueue = new();

        double nextMoveSendTime;
        public override void Update() {
            PossessReplica();

            if (Pawn == null)
                return;

            while (moveQueue.Count > 0)
                moveQueue.Dequeue();

            // if (moveQueue.Count < 10) {
            Input.Update();

            var newMove = Pawn.GetCurrentMove();
            Assert.IsTrue(newMove.GetTime() > 0.001f, "most certainly an error");

            Pawn.ResetCurrentMove();

            moveQueue.Enqueue(newMove);

            Pawn.ExecuteMove(newMove, Time.deltaTime);
            //}

            // if (moveQueue.Count >= 10)
            //    Debug.Log("Stalled movement");

            if (Time.timeAsDouble >= nextMoveSendTime) {
                nextMoveSendTime = Time.timeAsDouble + (1f / 30);
                SendMove();
            }
        }

        void SendMove() {
            if (Pawn == null || moveQueue.Count == 0)
                return;

            var bs = new BitWriter(32);
            bs.Write((byte)MessageId.Move);

            bs.WriteIntInRange(moveQueue.Count, 1, 20);
            foreach (var move in moveQueue) {
                move.Serialize(bs);
            }

            ClientGame.Main.Client.NetworkInterface.Send(bs, PacketReliability.Unreliable, MessageChannel.Move);
        }

        public void OnMoveCorrect(BitReader bs) {
            var acceptedTime = bs.ReadFloat();
            Pawn.ExecuteMoveResult(bs);

            // Throw away old moves
            int num = 0;

            while (moveQueue.Count > 0) {
                var move = moveQueue.Peek();
                if (move.GetTime() > acceptedTime)
                    break;

                moveQueue.Dequeue();
                ++num;
            }

            // Replay moves
            foreach (var move in moveQueue) {
                var t2 = move.GetTime() - acceptedTime;
                Pawn.ExecuteMove(move, t2);
                acceptedTime = move.GetTime();
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
            moveQueue.Clear();

            Pawn.InputMap.Disable();
        }

        void PossessReplica() {
            if (_currentReplicaToPossess == ReplicaId.Invalid)
                return;

            var replica = ClientGame.Main.Client.ReplicaManager.GetReplica(_currentReplicaToPossess);
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

        public override string ToString() {
            return "ClientPlayerController";
        }
    }
}