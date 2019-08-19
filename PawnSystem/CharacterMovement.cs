using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    public class CharacterMovement : ReplicaBehaviour, IPawnMovement {
        public delegate void CharacterEvent(Character character);

        const float minPitch = -60;
        const float maxPitch = 40;

        public event CharacterEvent onJump;
        public event CharacterEvent onLand;

        Character _character;

        float _lastGroundedTime;
        float _jumpForce;
        Vector3 _lastMovement;
        Vector3 _move;
        float _yaw;
        float _viewPitch;
        bool _jump;
       
        public void AddMoveInput(Vector3 direction) {
            _move += direction.normalized;
        }

        public void AddYawInput(float value) {
            _yaw += value * 2;
            _yaw = Mathf.Repeat(_yaw, 360);
        }

        public void AddPitchInput(float value) {
            _viewPitch += value * 2;
            _viewPitch = Mathf.Clamp(_viewPitch, minPitch, maxPitch);
        }

        public void Jump() {
            if (!_character.isGrounded)
                return;

            _jump = true;
        }

        public void OnEnterLadder() {
        }

        public void OnExitLadder() {
        }

        public void Tick() {
            if (isClient && isOwner) {
                RpcServerMove(transform.position);
            }
        }

        protected virtual void Update() {
            if (!isClient || !isOwner)
                return;

            transform.localRotation = Quaternion.AngleAxis(_yaw, Vector3.up);
            _character.view.localRotation = Quaternion.AngleAxis(_viewPitch, Vector3.left);

            var actualMovement = _move * _character.moveSpeed;

            var modifier = _character.isGrounded ? _character.groundControl : _character.airControl;
            actualMovement = Vector3.Lerp(_lastMovement, actualMovement, modifier);

            _lastMovement = actualMovement;

            // Landing
            if (_character.isGrounded) {
                if (_lastGroundedTime < Time.time - 0.1f) {
                    onLand?.Invoke(_character);
                    _jumpForce = 0;
                }

                if (_jump && _jumpForce < 0.1f) {
                    _jumpForce = 1;
                    onJump?.Invoke(_character);
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
            _character.characterController.Move(actualMovement * Time.deltaTime);

            _move = Vector3.zero;
            _jump = false;
        }

        void Awake() {
            _character = GetComponent<Character>();
        }

        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerMove(Vector3 finalPosition) {
            transform.position = finalPosition;
        }
    }
}