using Cube.Replication;
using UnityEngine;
using UnityEngine.Assertions;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : ReplicaBehaviour, ICharacterMovement {
        
        public event CharacterEvent Jumped;
        public event CharacterEvent Landed;

        public CharacterMovementSettings settings;
        public CharacterMovementSettings Settings => settings;
        public LayerMask clientGroundMask, serverGroundMask;

        public Vector3 Velocity => characterController.velocity;
        public Vector3 LocalVelocity => transform.InverseTransformDirection(Velocity);

        public bool IsMoving => Velocity.sqrMagnitude > 0.02f;

        public bool IsRunning {
            get;
            internal set;
        }

        public bool IsGrounded {
            get;
            internal set;
        }

        public PhysicMaterial GroundMaterial {
            get;
            internal set;
        }

        const float minViewPitch = -55;
        const float maxViewPitch = 60;

        float nextSendTime;
        const float minSendDelay = 1 / 60f;

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

        bool hasNewPlatform;
        Vector3 platformLocalPoint;
        Vector3 platformGlobalPoint;

        bool onLadder;

        // Interpolation
        struct State {
            internal double timestamp;
            internal Vector3 pos;
            internal Quaternion rot;
            internal bool run;
        }

        readonly State[] bufferedState = new State[10];
        int timestampCount;

        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            characterController.enabled = false;

            transform.position = targetPosition;
            transform.rotation = targetRotation;

            characterController.enabled = true;
        }

        public void SetMove(Vector2 value) {
            moveInput.x += value.x;
            moveInput.z += value.y;
        }

        public void SetLook(Vector2 value) {
            yaw = Mathf.Repeat(yaw + value.x, 360);
            viewPitch = Mathf.Clamp(viewPitch + value.y, minViewPitch, maxViewPitch);
        }

        public void SetRun(bool run) {
            IsRunning = run;
        }

        public void Jump() {
            if (!IsGrounded)
                return;

            jump = true;
        }

        public void Disable() {
            characterController.detectCollisions = false;
        }

        public void Enable() {
            characterController.detectCollisions = true;
        }

        public void AddVelocity(Vector3 velocity) {
            // #todo
        }

        public void OnEnterLadder() {
            onLadder = true;
        }

        public void OnExitLadder() {
            onLadder = false;
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            if (Replica.Owner == ctx.Observer.Connection)
                return;

            bs.WriteLossyFloat(transform.position.x, -5000, 5000, 0.01f);
            bs.WriteLossyFloat(transform.position.y, -1000, 1000, 0.01f);
            bs.WriteLossyFloat(transform.position.z, -5000, 5000, 0.01f);
            bs.WriteLossyFloat(yaw, 0, 360, 2);
            bs.WriteLossyFloat(viewPitch, minViewPitch, maxViewPitch, 5);
            bs.Write(IsRunning);
        }

        public override void Deserialize(BitStream bs) {
            if (isOwner)
                return;

            var pos = Vector3.zero;
            pos.x = bs.ReadLossyFloat(-5000, 5000, 0.01f);
            pos.y = bs.ReadLossyFloat(-1000, 1000, 0.01f);
            pos.z = bs.ReadLossyFloat(-5000, 5000, 0.01f);

            var yaw = bs.ReadLossyFloat(0, 360, 2);
            viewPitch = bs.ReadLossyFloat(minViewPitch, maxViewPitch, 5);
            var run = bs.ReadBool();

            // Shift the buffer sideways, deleting state 20
            for (int i = bufferedState.Length - 1; i >= 1; i--) {
                bufferedState[i] = bufferedState[i - 1];
            }

            // Record current state in slot 0
            State state;
            state.timestamp = Time.timeAsDouble;
            state.pos = pos;
            state.rot = Quaternion.AngleAxis(yaw, Vector3.up);
            state.run = run;
            bufferedState[0] = state;

            timestampCount = Mathf.Min(timestampCount + 1, bufferedState.Length);
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
            var speed = IsRunning ? settings.moveSpeed : settings.runSpeed;
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
                    Landed?.Invoke(character);
                    jumpForce = 0;
                }

                var beginNewJump = jump && jumpForce < 0.1f;
                if (beginNewJump) {
                    jumpForce = 1;
                    Jumped?.Invoke(character);
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
            } else {
                hasNewPlatform = false;
            }

            platformLocalPoint = Platform.referenceTransform.InverseTransformPoint(transform.position);
            platformGlobalPoint = transform.position;
        }

        void UpdateRemote() {
            viewPitchLerp = Mathf.Lerp(viewPitchLerp, viewPitch, 5 * Time.deltaTime);
            character.view.localRotation = Quaternion.AngleAxis(viewPitchLerp, Vector3.left);

            // This is the target playback time of the rigid body
            double interpolationTime = Time.timeAsDouble - settings.InterpolationBackTime;

            // Use interpolation if the target playback time is present in the buffer
            if (bufferedState[0].timestamp > interpolationTime) {
                for (int i = 0; i < timestampCount; ++i) {
                    if (bufferedState[i].timestamp <= interpolationTime || i == timestampCount - 1) {
                        State rhs = bufferedState[Mathf.Max(i - 1, 0)];
                        State lhs = bufferedState[i];

                        // Use the time between the two slots to determine if interpolation is necessary
                        double length = rhs.timestamp - lhs.timestamp;
                        float t = 0.0f;
                        if (length > 0.0001) {
                            t = (float)((interpolationTime - lhs.timestamp) / length);
                        }

                        var position = Vector3.Lerp(lhs.pos, rhs.pos, t);
                        transform.localRotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);

                        var diff = position - transform.position;
                        if (diff.sqrMagnitude < 1) { // Physics might cause the client-side to become desynced
                            characterController.Move(position - transform.position);
                        } else {
                            Teleport(position, transform.rotation);
                        }

                        IsRunning = lhs.run;

                        //Debug.DrawLine(transform.position + Vector3.up * 2, transform.position + Vector3.up * 3, Color.green);
                        return;
                    }
                }
            } else {
                // Use extrapolation
                State latest = bufferedState[0];
                State latest2 = bufferedState[Mathf.Min(1, timestampCount - 1)];

                float extrapolationLength = (float)(interpolationTime - latest.timestamp);
                if (extrapolationLength < settings.ExtrapolationLimit) {
                    var speed = latest.run ? settings.moveSpeed : settings.runSpeed;
                    var velocity = (latest.pos - latest2.pos).normalized * speed;


                    var position = latest.pos + velocity * extrapolationLength;

                    var diff = position - transform.position;
                    if (diff.sqrMagnitude < 1) { // Physics might cause the client-side to become desynced
                        characterController.Move(position - transform.position);
                    } else {
                        Teleport(position, transform.rotation);
                    }

                    //Debug.DrawLine(transform.position + Vector3.up * 2, transform.position + Vector3.up * 3, Color.red);
                }
            }
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

        void OnDrawGizmosSelected() {
            Gizmos.color = Color.green;

            State lastS = bufferedState[0];
            for (int i = 1; i < timestampCount; ++i) {
                Gizmos.DrawLine(lastS.pos, bufferedState[i].pos);
                lastS = bufferedState[i];
            }

            Gizmos.color = Color.red;
            for (int i = 0; i < timestampCount; ++i) {
                Gizmos.DrawSphere(bufferedState[i].pos, 0.05f);
            }
        }

        void Awake() {
            groundColliders = new Collider[1];

            characterController = GetComponent<CharacterController>();
            character = GetComponent<Character>();
            Assert.IsNotNull(characterController);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(character);
        }
    }
}