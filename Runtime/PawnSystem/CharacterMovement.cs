using Cube.Replication;
using UnityEngine;
using UnityEngine.Assertions;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : ReplicaBehaviour, IPawnMovement {
        public delegate void CharacterEvent(Character character);

        public CharacterMovementSettings settings;
        public LayerMask clientGroundMask, serverGroundMask;

        public Vector3 Velocity {
            get { return characterController.velocity; }
        }

        public Vector3 LocalVelocity {
            get { return transform.InverseTransformDirection(characterController.velocity); }
        }

        public bool IsMoving {
            get { return characterController.velocity.sqrMagnitude > 0.1f; }
        }

        public bool IsGrounded {
            get;
            internal set;
        }

        public PhysicMaterial GroundMaterial {
            get;
            internal set;
        }

        public event CharacterEvent OnJump, OnLand;

        const float minViewPitch = -55;
        const float maxViewPitch = 60;

        float nextSendTime;
        const float minSendDelay = 1 / 60f;

        [Range(0, 500)]
        [Tooltip("The delay remote players are displayed at")]
        public int interpolationDelayMs;
        [ReadOnly]
        public Platform Platform;

        CharacterController characterController;
        Character character;

        float lastGroundedTime;
        float jumpForce;
        Vector3 lastMovement;
        Vector3 moveInput;
        float yaw;
        float viewPitch;
        float viewPitchLerp;
        bool jump;
        bool run;

        bool hasNewPlatform;
        Vector3 platformLocalPoint;
        Vector3 platformGlobalPoint;

        bool onLadder;

        TransformHistory history;

        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            characterController.enabled = false;

            transform.position = targetPosition;
            transform.rotation = targetRotation;

            characterController.enabled = true;
        }

        public void AddMoveInput(Vector3 direction) {
            moveInput += direction;
        }

        public void AddYawInput(float value) {
            yaw += value;
            yaw = Mathf.Repeat(yaw, 360);
        }

        public void AddPitchInput(float value) {
            viewPitch += value;
            viewPitch = Mathf.Clamp(viewPitch, minViewPitch, maxViewPitch);
        }

        public void SetRun(bool run) {
            this.run = run;
        }

        public void Jump() {
            if (!IsGrounded)
                return;

            jump = true;
        }

        public void OnEnterLadder() {
            onLadder = true;
        }

        public void OnExitLadder() {
            onLadder = false;
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            if (replica.Owner == ctx.Observer.connection)
                return;

            bs.Write(transform.position);
            bs.WriteLossyFloat(yaw, 0, 360, 2);
            bs.WriteLossyFloat(viewPitch, minViewPitch, maxViewPitch, 5);
        }

        public override void Deserialize(BitStream bs) {
            if (isOwner)
                return;

            var pos = bs.ReadVector3();
            var yaw = bs.ReadLossyFloat(0, 360, 2);
            viewPitch = bs.ReadLossyFloat(minViewPitch, maxViewPitch, 5);

            var t = Time.time + interpolationDelayMs * 0.001f;
            history.Add(t, new Pose(pos, Quaternion.AngleAxis(yaw, Vector3.up)));
        }

        protected void Update() {
            UpdateGround();
            UpdatePlatform();

            if (isClient && !isOwner) {
                UpdateRemote();
                return;
            }

            character.view.localRotation = Quaternion.AngleAxis(viewPitch, Vector3.left);
            transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);

            var actualMovement = moveInput.normalized;
            if (onLadder) {
                var f = actualMovement.z;
                actualMovement.z = 0;
                actualMovement.y = f;
            }


            // Apply modifiers
            var speed = run ? settings.moveSpeed : settings.runSpeed;
            actualMovement *= speed;

            if (actualMovement.z < 0) {
                actualMovement.z *= settings.backwardSpeedModifier;
            }
            actualMovement.x *= settings.sideSpeedModifier;

            var modifier = IsGrounded ? settings.groundControl : settings.airControl;
            actualMovement = Vector3.Lerp(lastMovement, actualMovement, modifier);

            // Apply movement
            lastMovement = actualMovement;
            actualMovement = transform.rotation * actualMovement;

            // Landing
            if (IsGrounded) {
                if (lastGroundedTime < Time.time - 0.2f) {
                    OnLand?.Invoke(character);
                    jumpForce = 0;
                }

                var beginNewJump = jump && jumpForce < 0.1f;
                if (beginNewJump) {
                    jumpForce = 1;
                    OnJump?.Invoke(character);
                }

                lastGroundedTime = Time.time;
            }

            // Jump
            var isJumping = jumpForce > 0.1f;
            if (isJumping) {
                jumpForce = Mathf.Max(jumpForce - Time.deltaTime * 1.2f, 0);

                actualMovement += jumpForce * Vector3.up * settings.jumpForce2;
            }

            // Gravity
            if (settings.useGravity && !onLadder) {
                actualMovement += Physics.gravity;
            }

            // Move
            characterController.Move(actualMovement * Time.deltaTime);

            // Consume input
            moveInput = Vector3.zero;
            jump = false;

            if (isClient && Time.time >= nextSendTime) {
                nextSendTime = Time.time + minSendDelay;

                RpcServerMove(transform.position, yaw, viewPitch);
            }
        }

        Collider[] groundColliders;
        void UpdateGround() {
            var epsilon = 0.1f;

            var cc = characterController;
            var pos = transform.position + Vector3.up * (cc.radius - epsilon);
            var layerMask = isClient ? clientGroundMask : serverGroundMask;
            var num = Physics.OverlapSphereNonAlloc(pos, cc.radius * (1f + epsilon), groundColliders, layerMask);

            Platform newPlatform = null;
            IsGrounded = false;

            for (int i = 0; i < num; ++i) {
                var collider = groundColliders[i];

                if (collider.attachedRigidbody != null && collider.attachedRigidbody.gameObject.isStatic) {
                    newPlatform = collider.GetComponentInParent<Platform>();
                }

                GroundMaterial = collider.sharedMaterial;
                IsGrounded = true;
                break;
            }

            if (newPlatform != Platform) {
                Platform = newPlatform;
                hasNewPlatform = true;
            }
        }

        void UpdatePlatform() {
            if (Platform == null)
                return;

            if (!hasNewPlatform) {
                var playerNoMovePlatformWorldPos = Platform.referenceTransform.TransformPoint(platformLocalPoint);
                var playerMovementLastFrame = transform.position - platformGlobalPoint;

                var finalPos = (playerNoMovePlatformWorldPos + playerMovementLastFrame);
                Teleport(finalPos, transform.rotation);
            }
            else {
                hasNewPlatform = false;
            }

            platformLocalPoint = Platform.referenceTransform.InverseTransformPoint(transform.position);
            platformGlobalPoint = transform.position;
        }

        void UpdateRemote() {
            viewPitchLerp = Mathf.Lerp(viewPitchLerp, viewPitch, 5 * Time.deltaTime);
            character.view.localRotation = Quaternion.AngleAxis(viewPitchLerp, Vector3.left);

            history.Sample(Time.time, out Vector3 position, out Quaternion rotation);
            var diff = position - transform.position;
            if (diff.sqrMagnitude < 1) { // Physics might cause the client-side to become desynced
                characterController.Move(position - transform.position);
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

            this.yaw = yaw;
            this.viewPitch = viewPitch;
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
            groundColliders = new Collider[4];

            characterController = GetComponent<CharacterController>();
            character = GetComponent<Character>();
            Assert.IsNotNull(characterController);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(character);

            history = new TransformHistory(replica.settings.desiredUpdateRateMs, interpolationDelayMs);
        }
    }
}