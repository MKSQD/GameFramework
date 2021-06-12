using System;
using Cube.Replication;
using UnityEngine;
using UnityEngine.Assertions;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : ReplicaBehaviour, ICharacterMovement {
        public event Action Jumped;
        public event Action Landed;
        public event Action DeathByLanding;

        public CharacterMovementSettings settings;
        public CharacterMovementSettings Settings => settings;
        public LayerMask clientGroundMask, serverGroundMask;

        public Vector3 Velocity => characterController.velocity;
        public Vector3 LocalVelocity => transform.InverseTransformDirection(Velocity);

        public bool IsMoving => Velocity.sqrMagnitude > 0.02f;

        public bool IsSneaking {
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

        public float SpeedModifier {
            get;
            set;
        }

        CharacterController characterController;
        Character character;

        float lastGroundedTime;
        float nextJumpTime;
        Vector3 moveInput;
        Vector3 lastMovement;
        float y;
        float yaw;
        float viewPitch;
        float viewPitchLerp;
        bool jump;

        bool hasNewPlatform;
        Vector3 platformLocalPoint;
        Vector3 platformGlobalPoint;

        bool onLadder;

        // Interpolation
        struct RemoteState {
            internal double timestamp;
            internal Vector3 pos;
            internal Quaternion rot;
            internal bool sneak;
        }

        readonly RemoteState[] bufferedRemoteStates = new RemoteState[10];
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

        public void SetSneaking(bool value) {
            IsSneaking = value;
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
            bs.Write(IsSneaking);
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
            var sneak = bs.ReadBool();

            // Shift the buffer sideways, deleting state 20
            for (int i = bufferedRemoteStates.Length - 1; i >= 1; i--) {
                bufferedRemoteStates[i] = bufferedRemoteStates[i - 1];
            }

            // Record current state in slot 0
            RemoteState state;
            state.timestamp = Time.timeAsDouble;
            state.pos = pos;
            state.rot = Quaternion.AngleAxis(yaw, Vector3.up);
            state.sneak = sneak;
            bufferedRemoteStates[0] = state;

            timestampCount = Mathf.Min(timestampCount + 1, bufferedRemoteStates.Length);
        }

        public float Momentum {
            get;
            internal set;
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
                y = f * 2;
            }


            // Apply modifiers
            var speed = IsSneaking ? settings.SneakSpeed : settings.runSpeed;
            actualMovement *= speed;


            if (actualMovement.z <= 0) {
                actualMovement.z *= settings.backwardSpeedModifier;
            }
            actualMovement.x *= settings.sideSpeedModifier;
            actualMovement *= SpeedModifier;

            if (settings.GainMomentum) {
                if (IsGrounded) {
                    Momentum = Mathf.Min(Momentum + Time.deltaTime * 0.2f, 1);
                }
                if (actualMovement.z < -0.1f || !IsMoving) {
                    Momentum = Mathf.Max(Momentum - Time.deltaTime * 8, 0);
                }

                actualMovement.z *= (1 + Momentum * settings.Momentum);
            }

            actualMovement.y = 0;

            var modifier = IsGrounded ? settings.groundControl : settings.airControl;
            actualMovement = Vector3.Lerp(lastMovement, actualMovement, modifier);

            lastMovement = actualMovement;

            // Landing
            if (IsGrounded) {
                if (lastGroundedTime < Time.time - 2) {
                    DeathByLanding?.Invoke();
                }

                if (lastGroundedTime < Time.time - 0.2f) {
                    Momentum *= 0.8f;

                    Landed?.Invoke();
                }

                lastGroundedTime = Time.time;
            }

            // Gravity
            if (settings.useGravity && !onLadder) {
                y = Mathf.MoveTowards(y, Physics.gravity.y, Time.deltaTime * 10);
            }

            // Jump
            var wasRecentlyGrounded = lastGroundedTime >= Time.time - settings.JumpGroundedGraceTime;
            var beginJump = jump && Time.time >= nextJumpTime && wasRecentlyGrounded;
            if (beginJump) {
                nextJumpTime = Time.time + settings.JumpCooldown;
                y = settings.JumpForce;
                Jumped?.Invoke();
            }

            // Move
            characterController.Move(transform.rotation * actualMovement * Time.deltaTime + Vector3.up * y * Time.deltaTime);

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

            var pos = transform.position + Vector3.up * (characterController.radius - epsilon);
            var layerMask = isClient ? clientGroundMask : serverGroundMask;
            var num = Physics.OverlapSphereNonAlloc(pos, characterController.radius * (1f + epsilon), groundColliders, layerMask);

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
            double interpolationTime = Time.timeAsDouble - settings.InterpolationDelay;

            // Use interpolation if the target playback time is present in the buffer
            if (bufferedRemoteStates[0].timestamp > interpolationTime) {
                for (int i = 0; i < timestampCount; ++i) {
                    if (bufferedRemoteStates[i].timestamp <= interpolationTime || i == timestampCount - 1) {
                        RemoteState rhs = bufferedRemoteStates[Mathf.Max(i - 1, 0)];
                        RemoteState lhs = bufferedRemoteStates[i];

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

                        IsSneaking = lhs.sneak;

                        //Debug.DrawLine(transform.position + Vector3.up * 2, transform.position + Vector3.up * 3, Color.green);
                        return;
                    }
                }
            } else {
                // Use extrapolation
                RemoteState latest = bufferedRemoteStates[0];
                RemoteState latest2 = bufferedRemoteStates[Mathf.Min(1, timestampCount - 1)];

                float extrapolationLength = (float)(interpolationTime - latest.timestamp);
                if (extrapolationLength < settings.ExtrapolationLimit) {
                    var speed = latest.sneak ? settings.SneakSpeed : settings.runSpeed;
                    var posDiffLastStates = latest.pos - latest2.pos;
                    if (posDiffLastStates.sqrMagnitude > 0.3f) { // Movement?
                        var estimatedVelocity = posDiffLastStates.normalized * speed;
                        var extrapolatedPos = latest.pos + estimatedVelocity * extrapolationLength;
                        characterController.Move(extrapolatedPos - transform.position);

                        Debug.DrawLine(transform.position + Vector3.up * 2, transform.position + Vector3.up * 3, Color.blue);
                    }
                }
            }
        }

        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerMove(Vector3 finalPosition, float yaw, float viewPitch) {
            var diff = transform.position - finalPosition;
            if (diff.sqrMagnitude > 5) {
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

            RemoteState lastS = bufferedRemoteStates[0];
            for (int i = 1; i < timestampCount; ++i) {
                Gizmos.DrawLine(lastS.pos, bufferedRemoteStates[i].pos);
                lastS = bufferedRemoteStates[i];
            }

            Gizmos.color = Color.red;
            for (int i = 0; i < timestampCount; ++i) {
                Gizmos.DrawSphere(bufferedRemoteStates[i].pos, 0.05f);
            }
        }

        void Awake() {
            lastGroundedTime = Time.time;
            SpeedModifier = 1;

            groundColliders = new Collider[1];

            characterController = GetComponent<CharacterController>();
            character = GetComponent<Character>();
            Assert.IsNotNull(characterController);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(character);
        }
    }
}