using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    public class CharacterMovement : ReplicaBehaviour, IPawnMovement {
        protected class Move : ISerializable {
            public uint tick;
            public Vector3 movement;
            public float yaw;
            public float pitch;
            public bool jump;

            public bool forceNoCombine = false;

            public bool CanCombineWith(Move other) {
                if (forceNoCombine || other.forceNoCombine)
                    return false;

                var diff = movement - other.movement;
                if (diff.sqrMagnitude > 0.1f)
                    return true;

                return false;
            }

            public virtual void CombineWith(Move other) {
                movement += other.movement;
                yaw = other.yaw;
                pitch = other.pitch;
                if (other.jump) {
                    jump = true;
                }
            }

            public virtual void CopyFromLastMove(Move last) {
                yaw = last.yaw;
                pitch = last.pitch;
            }

            public virtual void Serialize(BitStream bs) {
                bs.Write(tick);
                bs.WriteLossyFloat(movement.x, -maxMovement, maxMovement, 0.01f);
                bs.WriteLossyFloat(movement.z, -maxMovement, maxMovement, 0.01f);
                bs.WriteLossyFloat(yaw, 0, 360, yawPrecision);
                bs.WriteLossyFloat(pitch, minPitch, maxPitch, 1f);
                bs.Write(jump);
            }

            public virtual void Deserialize(BitStream bs) {
                tick = bs.ReadUInt();
                movement.x = bs.ReadLossyFloat(-maxMovement, maxMovement, 0.01f);
                movement.z = bs.ReadLossyFloat(-maxMovement, maxMovement, 0.01f);
                yaw = bs.ReadLossyFloat(0, 360, yawPrecision);
                pitch = bs.ReadLossyFloat(minPitch, maxPitch, 1f);
                jump = bs.ReadBool();
            }
        }

        const float maxMovement = 10;
        const float yawPrecision = 0.01f;
        const float minPitch = -60;
        const float maxPitch = 50;
        const float movementClientAdjustThresholdSqr = 0.3f;

        Character _character;

        Vector3 _lastMovement;
        float _lastGroundedTime;
        float _jumpForce;

        Move _currentMove;
        LinkedList<Move> _pendingMoves = new LinkedList<Move>();
        List<Move> _subMoves = new List<Move>();

        Vector3? _clientAdjustedPosition;
        uint _clientAdjustTick;

        StreamWriter logFile;

        public void AddMoveInput(Vector3 worldDirection) {
            _currentMove.movement += worldDirection.normalized;
            _currentMove.movement.x = Mathf.Clamp(_currentMove.movement.x, -maxMovement, maxMovement);
            _currentMove.movement.z = Mathf.Clamp(_currentMove.movement.z, -maxMovement, maxMovement);
        }

        public void AddYawInput(float value) {
            _currentMove.yaw += value;
            _currentMove.yaw = Mathf.Repeat(_currentMove.yaw, 360);
        }

        public void AddPitchInput(float value) {
            _currentMove.pitch += value;
            _currentMove.pitch = Mathf.Clamp(_currentMove.pitch, minPitch, maxPitch);
        }

        public void Jump() {
            if (!_character.isGrounded && !_currentMove.jump)
                return;

            _currentMove.forceNoCombine = true;
            _currentMove.jump = true;
        }

        public void OnEnterLadder() {
        }

        public void OnExitLadder() {
        }

        void LogMove(Move move) {
            if (logFile == null)
                return;

            logFile.WriteLine(move.tick + " " + move.movement + " " + move.yaw);
            //logFile.Flush();
        }

        Vector3 _lastTickPosition;
        public void Tick() {
            if (isClient && isOwner) {
                transform.position = _lastTickPosition;
                foreach (var move in _subMoves) {
                    ReplicaMoveToServer(move, GameFramework.Tick.tickRate);
                }
                _lastTickPosition = transform.position;

                _subMoves.Clear();
            }
        }

        void ReplicaMoveToServer(Move move, float deltaTime) {
            _pendingMoves.AddLast(move);
            LogMove(move);
            PerformMovement(move, deltaTime);


            RpcServerMove(move, transform.position);
        }

        protected Move AllocateNewMove() {
            return new Move();
        }

        protected virtual void Update() {
            if (logFile == null) {
                logFile = File.CreateText(isClient ? "C:\\projects\\CubeWIP\\client.log" : "C:\\projects\\CubeWIP\\server.log");
            }

            PerformMovement(_currentMove, Time.deltaTime);

            var combined = false;
            if (_subMoves.Count > 0) {
                var lastMove = _subMoves.Last();
                if (lastMove.CanCombineWith(_currentMove)) {
                    lastMove.CombineWith(_currentMove);
                    combined = true;
                }
            }
            if (!combined) {
                _subMoves.Add(_currentMove);
            }


            var nextMove = AllocateNewMove();
            nextMove.tick = GameFramework.Tick.tick;
            nextMove.CopyFromLastMove(_currentMove);
            _currentMove = nextMove;
        }

        void Awake() {
            _character = GetComponent<Character>();
            _currentMove = AllocateNewMove();
        }

        void PerformMovement(Move move, float deltaTime) {
            if (isClient && _clientAdjustedPosition.HasValue) {
                var adjustPosition = _clientAdjustedPosition.Value;
                _clientAdjustedPosition = null;

                ClientUpdatePosition(adjustPosition, _clientAdjustTick);
            }

            transform.localRotation = Quaternion.AngleAxis(move.yaw, Vector3.up);
            _character.view.localRotation = Quaternion.AngleAxis(move.pitch, Vector3.left);


            var actualMovement = move.movement * _character.moveSpeed;

            var modifier = _character.isGrounded ? _character.groundControl : _character.airControl;
            actualMovement = Vector3.Lerp(_lastMovement, actualMovement, modifier);

            _lastMovement = actualMovement;


            // Landing
            if (_character.isGrounded) {
                if (_lastGroundedTime < Time.time - 0.1f) {
                    _character.onLand.Invoke();
                    _jumpForce = 0;
                }

                if (move.jump && _jumpForce < 0.1f) {
                    _jumpForce = 1;
                    _character.onJump.Invoke();
                }

                _lastGroundedTime = Time.time;
            }


            // Jump
            if (_jumpForce > 0.1f) {
                _jumpForce *= 0.98f;

                actualMovement += _jumpForce * Vector3.up * _character.jumpForce;
            }


            // Gravity
            if (!_character.isGrounded && _character.useGravity) {
                actualMovement += Physics.gravity;
            }


            // Move
            _character.characterController.Move(actualMovement * deltaTime);
        }

        uint _clientLastTick;
        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerMove(Move move, Vector3 finalPosition) {
            if (move.tick <= _clientLastTick)
                return;

            var OLDPOS = transform.position;

            LogMove(move);
            PerformMovement(move, GameFramework.Tick.tickRate);
            _clientLastTick = move.tick;

            var diff = transform.position - finalPosition;
            if (diff.sqrMagnitude > movementClientAdjustThresholdSqr) {
                RpcClientAdjustPosition(transform.position, move.tick);
                logFile.WriteLine("Reset client to tick " + move.tick + " diff=" + diff.sqrMagnitude);
                return;
            }

            _character.characterController.Move(finalPosition - transform.position);

            var diff2 = transform.position - finalPosition;
            if (diff2.sqrMagnitude > 0.01f) {
                RpcClientAdjustPosition(transform.position, move.tick);
                return;
            }

            Debug.DrawLine(OLDPOS, transform.position, Color.green, 1f);
            RpcClientAckGoodPosition(move.tick);
        }

        uint _lastServerAckedTick;
        [ReplicaRpc(RpcTarget.Owner)]
        void RpcClientAdjustPosition(Vector3 finalPosition, uint tick) {
            if (tick <= _lastServerAckedTick)
                return;

            _lastServerAckedTick = tick;

            _clientAdjustedPosition = finalPosition;
            _clientAdjustTick = tick;
        }

        [ReplicaRpc(RpcTarget.Owner)]
        void RpcClientAckGoodPosition(uint tick) {
            if (tick <= _lastServerAckedTick)
                return;

            _lastServerAckedTick = tick;

            DiscardPendingMovesBeforeTick(tick);
        }

        void ClientUpdatePosition(Vector3 worldPosition, uint tick) {
            logFile.WriteLine("--- Client rewind " + tick + " ---");

            Debug.DrawLine(transform.position, worldPosition, Color.red, 1f);
            _character.Teleport(worldPosition, transform.rotation);

            DiscardPendingMovesBeforeTick(tick);

            foreach (var move in _pendingMoves) {
                LogMove(move);
                PerformMovement(move, GameFramework.Tick.tickRate);
            }
            foreach (var move in _subMoves) {
                LogMove(move);
                PerformMovement(move, GameFramework.Tick.tickRate);
            }

            logFile.WriteLine("--- Client rewind " + tick + " END ---");
        }

        void DiscardPendingMovesBeforeTick(uint tick) {
            while (_pendingMoves.Count > 0 && _pendingMoves.First.Value.tick <= tick) {
                _pendingMoves.RemoveFirst();
            }
        }
    }
}