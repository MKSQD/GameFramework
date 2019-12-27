using Cube.Replication;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    public class CharacterMovement : ReplicaBehaviour, IPawnMovement {
        public delegate void CharacterEvent(Character character);

        const float minPitch = -55;
        const float maxPitch = 60;

        public event CharacterEvent onJump;
        public event CharacterEvent onLand;

        public LayerMask clientGroundMask, serverGroundMask;

        [Range(0, 500)]
        public int interpolationDelayMs;

        public Platform platform;

        Character _character;

        float _lastGroundedTime;
        float _jumpForce;
        Vector3 _lastMovement;
        Vector3 _move;
        float _yaw;
        float _viewPitch;
        bool _jump;
        bool _run;

        bool hasNewPlatform;
        Vector3 _platformLocalPoint;
        Vector3 _platformGlobalPoint;

        TransformHistory _history;

        public void AddMoveInput(Vector3 direction) {
            _move += direction;
        }

        public void AddYawInput(float value) {
            _yaw += value;
            _yaw = Mathf.Repeat(_yaw, 360);
        }

        public void AddPitchInput(float value) {
            _viewPitch += value;
            _viewPitch = Mathf.Clamp(_viewPitch, minPitch, maxPitch);
        }

        public void SetRun(bool run) {
            _run = run;
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
                RpcServerMove(transform.position, _yaw, _viewPitch);
            }
        }

        protected virtual void FixedUpdate() {
            UpdateGround();
            UpdatePlatform();

            _character.view.localRotation = Quaternion.AngleAxis(_viewPitch, Vector3.left);

            if (isClient && !isOwner) {
                UpdateRemote();
                return;
            }

            transform.localRotation = Quaternion.AngleAxis(_yaw, Vector3.up);

            var actualMovement = _move.normalized * (!_run ? _character.moveSpeed : _character.runSpeed);

            var modifier = _character.isGrounded ? _character.groundControl : _character.airControl;
            actualMovement = Vector3.Lerp(_lastMovement, actualMovement, modifier);

            _lastMovement = actualMovement;

            // Landing
            if (_character.isGrounded) {
                if (_lastGroundedTime < Time.time - 0.2f) {
                    onLand?.Invoke(_character);
                    _jumpForce = 0;
                }

                var beginNewJump = _jump && _jumpForce < 0.1f;
                if (beginNewJump) {
                    _jumpForce = 1;
                    onJump?.Invoke(_character);
                }

                _lastGroundedTime = Time.time;
            }

            // Jump
            var isJumping = _jumpForce > 0.1f;
            if (isJumping) {
                _jumpForce *= Mathf.Max(1 - 3f * Time.deltaTime, 0);

                actualMovement += _jumpForce * Vector3.up * _character.jumpForce;
            }

            // Gravity
            if (_character.gravity) {
                actualMovement += Physics.gravity;
            }

            // Move
            _character.characterController.Move(actualMovement * Time.deltaTime);

            // Consume input
            _move = Vector3.zero;
            _jump = false;
        }

        void UpdateGround() {
            Platform newPlatform = null;

            var layerMask = isClient ? clientGroundMask : serverGroundMask;
            if (Physics.Raycast(transform.position + Vector3.up * 0.01f, Vector3.down, out RaycastHit hit, 0.2f, layerMask)) {
                newPlatform = hit.collider.GetComponentInParent<Platform>();
            }

            if (newPlatform != platform) {
                platform = newPlatform;
                hasNewPlatform = true;
            }
        }

        void UpdatePlatform() {
            if (platform == null)
                return;

            if (!hasNewPlatform) {
                var playerNoMovePlatformWorldPos = platform.referenceTransform.TransformPoint(_platformLocalPoint);
                var playerMovementLastFrame = transform.position - _platformGlobalPoint;

                var finalPos = (playerNoMovePlatformWorldPos + playerMovementLastFrame);
                _character.Teleport(finalPos, transform.rotation);
            }
            else {
                hasNewPlatform = false;
            }

            _platformLocalPoint = platform.referenceTransform.InverseTransformPoint(transform.position);
            _platformGlobalPoint = transform.position;
        }

        void UpdateRemote() {
            _history.Sample(Time.time, out Vector3 position, out Quaternion rotation);
            var diff = position - transform.position;
            if (diff.sqrMagnitude < 1) { // Physics might cause the client-side to become desynced
                _character.characterController.Move(position - transform.position);
            }
            else {
                _character.Teleport(position, transform.rotation);
            }

            transform.localRotation = rotation;
        }

        void Awake() {
            _character = GetComponent<Character>();
            _history = new TransformHistory();
        }

        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerMove(Vector3 finalPosition, float yaw, float viewPitch) {
            var diff = transform.position - finalPosition;
            if (diff.sqrMagnitude > 10) {
                RpcOwnerResetPosition(transform.position);
                return;
            }

            _yaw = yaw;
            _viewPitch = viewPitch;
            _character.Teleport(finalPosition, Quaternion.AngleAxis(yaw, Vector3.up));
        }

        [ReplicaRpc(RpcTarget.Owner)]
        void RpcOwnerResetPosition(Vector3 finalPos) {
            _character.Teleport(finalPos, transform.rotation);
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            if (replica.Owner == ctx.Observer.connection)
                return;

            bs.Write(transform.position);
            bs.WriteLossyFloat(_yaw, 0, 360, 1);
            bs.WriteLossyFloat(_viewPitch, minPitch, maxPitch, 1);
        }

        public override void Deserialize(BitStream bs) {
            if (isOwner)
                return;

            var pos = bs.ReadVector3();
            var yaw = bs.ReadLossyFloat(0, 360, 1);
            _viewPitch = bs.ReadLossyFloat(minPitch, maxPitch, 1);
            _history.Add(new Pose(pos, Quaternion.AngleAxis(yaw, Vector3.up)), Time.time + interpolationDelayMs * 0.001f);
        }
    }
}