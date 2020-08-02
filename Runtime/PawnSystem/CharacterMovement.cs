using Cube.Replication;
using UnityEngine;
using UnityEngine.Assertions;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : ReplicaBehaviour, IPawnMovement {
        public delegate void CharacterEvent(Character character);

        public CharacterController CC;
        public CharacterMovementSettings settings;
        public LayerMask clientGroundMask, serverGroundMask;

        public Vector3 velocity {
            get { return CC.velocity; }
        }

        public Vector3 localVelocity {
            get { return transform.InverseTransformDirection(CC.velocity); }
        }

        public bool isMoving {
            get { return CC.velocity.sqrMagnitude > 0.1f; }
        }

        public bool isGrounded {
            get { return CC.isGrounded; }
        }

        public event CharacterEvent onJump, onLand;

        const float minViewPitch = -55;
        const float maxViewPitch = 60;

        float _nextSendTime;
        const float minSendDelay = 1 / 60f;

        [Range(0, 500)]
        [Tooltip("The delay remote players are displayed at")]
        public int interpolationDelayMs;

        public Platform platform;

        Character _character;

        float _lastGroundedTime;
        float _jumpForce;
        Vector3 _lastMovement;
        Vector3 _moveInput;
        float _yaw;
        float _viewPitch;
        float _viewPitchLerp;
        bool _jump;
        bool _run;

        bool hasNewPlatform;
        Vector3 _platformLocalPoint;
        Vector3 _platformGlobalPoint;

        bool _onLadder;

        TransformHistory _history;

        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            CC.enabled = false;
            
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            
            CC.enabled = true;
        }

        public void AddMoveInput(Vector3 direction) {
            _moveInput += direction;
        }

        public void AddYawInput(float value) {
            _yaw += value;
            _yaw = Mathf.Repeat(_yaw, 360);
        }

        public void AddPitchInput(float value) {
            _viewPitch += value;
            _viewPitch = Mathf.Clamp(_viewPitch, minViewPitch, maxViewPitch);
        }

        public void SetRun(bool run) {
            _run = run;
        }

        public void Jump() {
            if (!isGrounded)
                return;

            _jump = true;
        }

        public void OnEnterLadder() {
            _onLadder = true;
        }

        public void OnExitLadder() {
            _onLadder = false;
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            if (replica.Owner == ctx.Observer.connection)
                return;

            bs.Write(transform.position);
            bs.WriteLossyFloat(_yaw, 0, 360, 2);
            bs.WriteLossyFloat(_viewPitch, minViewPitch, maxViewPitch, 5);
        }

        public override void Deserialize(BitStream bs) {
            if (isOwner)
                return;

            var pos = bs.ReadVector3();
            var yaw = bs.ReadLossyFloat(0, 360, 2);
            _viewPitch = bs.ReadLossyFloat(minViewPitch, maxViewPitch, 5);

            var t = Time.time + interpolationDelayMs * 0.001f;
            _history.Add(t, new Pose(pos, Quaternion.AngleAxis(yaw, Vector3.up)));
        }

        protected void Update() {
            UpdateGround();
            UpdatePlatform();

            if (isClient && !isOwner) {
                UpdateRemote();
                return;
            }

            _character.view.localRotation = Quaternion.AngleAxis(_viewPitch, Vector3.left);
            transform.localRotation = Quaternion.AngleAxis(_yaw, Vector3.up);

            var actualMovement = _moveInput.normalized;
            if(_onLadder) {
                var f = actualMovement.z;
                actualMovement.z = 0;
                actualMovement.y = f;
            }


            // Apply modifiers
            var speed = _run ? settings.moveSpeed : settings.runSpeed;
            actualMovement *= speed;

            if (actualMovement.z < 0) {
                actualMovement.z *= settings.backwardSpeedModifier;
            }
            actualMovement.x *= settings.sideSpeedModifier;

            var modifier = isGrounded ? settings.groundControl : settings.airControl;
            actualMovement = Vector3.Lerp(_lastMovement, actualMovement, modifier);

            // Apply movement
            _lastMovement = actualMovement;
            actualMovement = transform.rotation * actualMovement;

            // Landing
            if (isGrounded) {
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
                _jumpForce = Mathf.Max(_jumpForce - Time.deltaTime * 1.2f, 0);

                actualMovement += _jumpForce * Vector3.up * settings.jumpForce2;
            }

            // Gravity
            if (settings.useGravity && !_onLadder) {
                actualMovement += Physics.gravity;
            }

            // Move
            CC.Move(actualMovement * Time.deltaTime);

            // Consume input
            _moveInput = Vector3.zero;
            _jump = false;

            if (isClient && Time.time >= _nextSendTime) {
                _nextSendTime = Time.time + minSendDelay;

                RpcServerMove(transform.position, _yaw, _viewPitch);
            }
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
                Teleport(finalPos, transform.rotation);
            }
            else {
                hasNewPlatform = false;
            }

            _platformLocalPoint = platform.referenceTransform.InverseTransformPoint(transform.position);
            _platformGlobalPoint = transform.position;
        }

        void UpdateRemote() {
            _viewPitchLerp = Mathf.Lerp(_viewPitchLerp, _viewPitch, 5 * Time.deltaTime);
            _character.view.localRotation = Quaternion.AngleAxis(_viewPitchLerp, Vector3.left);

            _history.Sample(Time.time, out Vector3 position, out Quaternion rotation);
            var diff = position - transform.position;
            if (diff.sqrMagnitude < 1) { // Physics might cause the client-side to become desynced
                CC.Move(position - transform.position);
            }
            else {
                Teleport(position, transform.rotation);
            }

            transform.localRotation = rotation;
        }

        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerMove(Vector3 finalPosition, float yaw, float viewPitch) {
            var diff = transform.position - finalPosition;
            if (diff.sqrMagnitude > 3) {
                RpcOwnerResetPosition(transform.position, transform.rotation.eulerAngles.y);
                return;
            }

            _yaw = yaw;
            _viewPitch = viewPitch;
            Teleport(finalPosition, Quaternion.AngleAxis(yaw, Vector3.up));
        }

        [ReplicaRpc(RpcTarget.Owner)]
        void RpcOwnerResetPosition(Vector3 finalPos, float yaw) {
            Teleport(finalPos, Quaternion.AngleAxis(yaw, Vector3.up));
        }

        void OnControllerColliderHit(ControllerColliderHit hit) {
            var body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic)
                return;

            var tooHeavyToPush = body.mass > 80;
            if (tooHeavyToPush)
                return;

            if (hit.moveDirection.y < -0.3f)
                return;

            var pushDir = hit.moveDirection;
            pushDir.y = 0;

            body.velocity = pushDir * settings.pushPower;
        }

        void Awake() {
            CC = GetComponent<CharacterController>();
            _character = GetComponent<Character>();
            Assert.IsNotNull(CC);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(_character);

            _history = new TransformHistory(replica.settings.desiredUpdateRateMs, interpolationDelayMs * 2.5f);
        }
    }
}