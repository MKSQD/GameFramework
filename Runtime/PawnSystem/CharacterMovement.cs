using System;
using Cube.Replication;
using UnityEngine;
using UnityEngine.Assertions;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    [AddComponentMenu("GameFramework/CharacterMovement")]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : ReplicaBehaviour, ICharacterMovement, ICharacterStatProvider {
        public event Action Jumped;
        public event Action Landed;
        public event Action DeathByLanding;

        public CharacterMovementSettings settings;
        public CharacterMovementSettings Settings => settings;

        public Vector3 Velocity => characterController.velocity;
        public Vector3 LocalVelocity => transform.InverseTransformDirection(Velocity);

        public bool IsMoving => Velocity.sqrMagnitude > 0.02f;

        public bool IsWalking {
            get;
            internal set;
        }

        public bool IsCrouching {
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
        public float Height => characterController.height;

        public float Momentum {
            get;
            internal set;
        }
        public bool InProceduralMovement { get; set; }

        const float minViewPitch = -55;
        const float maxViewPitch = 60;

        float nextSendTime;
        const float minSendDelay = 1 / 60f;

        [ReadOnly]
        public Platform Platform;

        CharacterController characterController;
        Character character;

        float lastGroundedTime;
        float nextJumpTime;
        Vector2 inputMove;
        bool inputJump;
        Vector3 lastMovement;
        float y;
        float yaw;
        float viewPitch;
        float viewPitchLerp;

        bool hasNewPlatform;
        Vector3 platformLocalPoint;
        Vector3 platformGlobalPoint;

        public bool IsOnLadder {
            get;
            internal set;
        }

        // Interpolation
        struct RemoteState {
            internal double Timestamp;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal bool Walking;
            internal bool Crouching;
        }

        readonly RemoteState[] bufferedRemoteStates = new RemoteState[10];
        int timestampCount;

        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            characterController.enabled = false;

            transform.position = targetPosition;
            transform.rotation = targetRotation;
            yaw = targetRotation.eulerAngles.y;
            lastMovement = Vector3.zero;
            y = 0;

            characterController.enabled = true;
        }

        public void SetMove(Vector2 value) {
            inputMove = value;
        }

        public void AddLook(Vector2 value) {
            yaw = Mathf.Repeat(yaw + value.x, 360);
            viewPitch = Mathf.Clamp(viewPitch + value.y, minViewPitch, maxViewPitch);
        }

        public void SetIsWalking(bool value) {
            IsWalking = value;
        }

        public void SetIsCrouching(bool value) {
            if (value == IsCrouching)
                return;

            var pos = character.view.localPosition;

            IsCrouching = value;
            if (IsCrouching) {
                characterController.height *= Settings.CrouchRelativeHeight;
                pos.y *= Settings.CrouchRelativeViewHeight;

            } else {
                characterController.height /= Settings.CrouchRelativeHeight;
                pos.y /= Settings.CrouchRelativeViewHeight;
            }

            characterController.center = new Vector3(0, characterController.height * 0.5f, 0);
            character.view.localPosition = pos;
        }

        public void Jump() {
            if (!IsGrounded)
                return;

            inputJump = true;
        }

        public void Disable() {
            characterController.detectCollisions = false;
            characterController.enableOverlapRecovery = false;
        }

        public void Enable() {
            characterController.detectCollisions = true;
            characterController.enableOverlapRecovery = true;
        }

        public void OnEnterLadder() {
            IsOnLadder = true;
        }

        public void OnExitLadder() {
            IsOnLadder = false;
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            if (Replica.Owner == ctx.View.Connection)
                return;

            bs.WriteLossyFloat(transform.position.x, -5000, 5000, 0.01f);
            bs.WriteLossyFloat(transform.position.y, -100, 600, 0.01f);
            bs.WriteLossyFloat(transform.position.z, -5000, 5000, 0.01f);
            bs.WriteLossyFloat(yaw, 0, 360, 2);
            bs.WriteLossyFloat(viewPitch, minViewPitch, maxViewPitch, 1);
            bs.Write(IsWalking);
            bs.Write(IsCrouching);
        }

        public override void Deserialize(BitStream bs) {
            if (isOwner)
                return;

            var pos = Vector3.zero;
            pos.x = bs.ReadLossyFloat(-5000, 5000, 0.01f);
            pos.y = bs.ReadLossyFloat(-100, 600, 0.01f);
            pos.z = bs.ReadLossyFloat(-5000, 5000, 0.01f);

            var yaw = bs.ReadLossyFloat(0, 360, 2);
            viewPitch = bs.ReadLossyFloat(minViewPitch, maxViewPitch, 1);
            var walking = bs.ReadBool();
            var crouching = bs.ReadBool();

            // Shift the buffer sideways, deleting state 20
            for (int i = bufferedRemoteStates.Length - 1; i >= 1; i--) {
                bufferedRemoteStates[i] = bufferedRemoteStates[i - 1];
            }

            // Record current state in slot 0
            RemoteState state;
            state.Timestamp = Time.timeAsDouble;
            state.Position = pos;
            state.Rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            state.Walking = walking;
            state.Crouching = crouching;
            bufferedRemoteStates[0] = state;

            timestampCount = Mathf.Min(timestampCount + 1, bufferedRemoteStates.Length);
        }



        Vector3 lastVelocity;

        protected void Update() {
            UpdateGround();
            UpdatePlatform();

            if (isClient && !isOwner) {
                UpdateRemote();
                return;
            }

            UpdateLocal();

            if (isClient && Time.time >= nextSendTime) {
                nextSendTime = Time.time + minSendDelay;
                RpcServerMove(transform.position, yaw, viewPitch, IsCrouching);
            }
        }

        void UpdateLocal() {
            character.view.localRotation = Quaternion.AngleAxis(viewPitch, Vector3.left);
            transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);

            var actualMovement = new Vector3(inputMove.x, 0, inputMove.y);
            actualMovement.Normalize();

            if (IsOnLadder) {
                var f = actualMovement.z;
                actualMovement.z = 0;
                y = f * 2;
            }


            // Apply modifiers
            {
                var baseMoveSpeed = settings.RunSpeed;
                if (IsWalking) {
                    baseMoveSpeed = settings.WalkSpeed;
                }
                if (IsCrouching) {
                    baseMoveSpeed = settings.CrouchSpeed;
                }
                actualMovement *= baseMoveSpeed;

                if (actualMovement.z <= 0) {
                    actualMovement.z *= settings.BackwardSpeedModifier;
                }
                actualMovement.x *= settings.SideSpeedModifier;
            }
            {
                float speedBuff = 1;
                character.Stats.ModifyStat(CharacterStat.MovementSpeed, ref speedBuff);
                actualMovement *= speedBuff;
            }

            // Momentum
            if (settings.GainMomentum) {
                var gainMomentum = IsGrounded && !IsCrouching;
                if (gainMomentum) {
                    Momentum = Mathf.Min(Momentum + Time.deltaTime * 0.15f, 1);
                }

                var looseAllMomentum = actualMovement.z < -0.1f || !IsMoving || IsWalking;
                if (looseAllMomentum) {
                    Momentum = 0;
                }

                var angle = Vector2.Angle(new Vector2(characterController.velocity.x, characterController.velocity.z).normalized,
                                          new Vector2(lastVelocity.x, lastVelocity.z).normalized);
                Momentum *= 1 - Mathf.Clamp01(angle / 180);

                actualMovement.z *= (1 + Momentum * settings.Momentum);
            }

            //
            lastVelocity = characterController.velocity;

            actualMovement.y = 0;

            var modifier = IsGrounded ? settings.GroundControl : settings.AirControl;
            actualMovement = Vector3.Lerp(actualMovement, lastMovement, modifier);

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
            }

            // Last grounded
            if (IsGrounded || IsOnLadder || InProceduralMovement) {
                lastGroundedTime = Time.time;
            }

            // Gravity
            if (settings.UseGravity && !IsOnLadder) {
                y = Mathf.MoveTowards(y, Physics.gravity.y, Time.deltaTime * 20);
            }

            // Jump
            var wasRecentlyGrounded = lastGroundedTime >= Time.time - settings.JumpGroundedGraceTime;
            var beginJump = inputJump && Time.time >= nextJumpTime && wasRecentlyGrounded;
            if (beginJump) {
                nextJumpTime = Time.time + settings.JumpCooldown;
                y = settings.JumpForce;

                if (IsCrouching) {
                    SetIsCrouching(false);
                }

                Jumped?.Invoke();
            }

            // Move
            characterController.Move(transform.rotation * actualMovement * Time.deltaTime + Vector3.up * y * Time.deltaTime);

            // Consume input
            inputMove = Vector2.zero;
            inputJump = false;
        }

        Collider[] groundColliders;

        void UpdateGround() {
            var epsilon = 0.1f;

            var pos = transform.position + Vector3.up * (characterController.radius - epsilon);
            var layerMask = isClient ? Settings.ClientGroundMask : Settings.ServerGroundMask;
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

            if (IsOnLadder) {
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
            if (bufferedRemoteStates[0].Timestamp > interpolationTime) {
                for (int i = 0; i < timestampCount; ++i) {
                    if (bufferedRemoteStates[i].Timestamp <= interpolationTime || i == timestampCount - 1) {
                        RemoteState newState = bufferedRemoteStates[Mathf.Max(i - 1, 0)];
                        RemoteState oldState = bufferedRemoteStates[i];

                        // Use the time between the two slots to determine if interpolation is necessary
                        double length = newState.Timestamp - oldState.Timestamp;
                        float t = 0f;
                        if (length > 0.0001) {
                            t = (float)((interpolationTime - oldState.Timestamp) / length);
                        }

                        if (Vector3.Distance(oldState.Position, newState.Position) < 2) {
                            var position = Vector3.Lerp(oldState.Position, newState.Position, t);
                            transform.localRotation = Quaternion.Slerp(oldState.Rotation, newState.Rotation, t);

                            var diff = position - transform.position;
                            if (diff.sqrMagnitude < 2) { // Physics might cause the client-side to become desynced
                                characterController.Move(position - transform.position);
                            } else {
                                Teleport(position, transform.rotation);
                            }
                        } else {
                            Teleport(newState.Position, newState.Rotation);
                        }

                        IsWalking = oldState.Walking;
                        IsCrouching = oldState.Crouching;

                        //Debug.DrawLine(transform.position + Vector3.up * 2, transform.position + Vector3.up * 3, Color.green);
                        return;
                    }
                }
            } else if (timestampCount > 1) {
                // Use extrapolation
                RemoteState newestState = bufferedRemoteStates[0];
                RemoteState oldState = bufferedRemoteStates[Mathf.Min(1, timestampCount - 1)];

                float extrapolationLength = (float)(interpolationTime - newestState.Timestamp);
                if (extrapolationLength < settings.ExtrapolationLimit) {
                    var speed = newestState.Walking ? settings.WalkSpeed : settings.RunSpeed;
                    var posDiffLastStates = newestState.Position - oldState.Position;
                    if (posDiffLastStates.sqrMagnitude > 0.1f) { // Movement?
                        var extrapolatedPos = newestState.Position + posDiffLastStates * extrapolationLength;
                        characterController.Move(extrapolatedPos - transform.position);

                        Debug.DrawLine(transform.position + Vector3.up * 2, transform.position + Vector3.up * 3, Color.blue);
                    }
                }
            } else {
                RemoteState latest = bufferedRemoteStates[0];
                Teleport(latest.Position, latest.Rotation);
            }
        }

        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerMove(Vector3 finalPosition, float yaw, float viewPitch, bool crouching) {
            var diff = transform.position - finalPosition;
            if (diff.sqrMagnitude > 3) {
                RpcOwnerResetPosition(transform.position, transform.rotation.eulerAngles.y);
                return;
            }

            SetIsCrouching(crouching);
            this.yaw = yaw;
            this.viewPitch = viewPitch;
            characterController.Move(finalPosition - transform.position);
        }

        [ReplicaRpc(RpcTarget.Owner)]
        void RpcOwnerResetPosition(Vector3 finalPos, float yaw) {
            Teleport(finalPos, Quaternion.AngleAxis(yaw, Vector3.up));
        }

        protected virtual void Start() {
            if (isServer) {
                RpcOwnerResetPosition(transform.position, transform.rotation.eulerAngles.y);
            }
        }

        public void ModifyStat(CharacterStat stat, ref float value) {
            if (stat == CharacterStat.PerceptionSighted && IsCrouching) {
                value *= Settings.CrouchAIPerceptionSightModifier;
            }
        }

        void Awake() {
            lastGroundedTime = Time.time;
            groundColliders = new Collider[1];

            characterController = GetComponent<CharacterController>();
            character = GetComponent<Character>();
            Assert.IsNotNull(characterController);
            Assert.IsNotNull(settings);
            Assert.IsNotNull(character);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() {
            Gizmos.color = Color.green;

            RemoteState lastS = bufferedRemoteStates[0];
            for (int i = 1; i < timestampCount; ++i) {
                Gizmos.DrawLine(lastS.Position, bufferedRemoteStates[i].Position);
                lastS = bufferedRemoteStates[i];
            }

            Gizmos.color = Color.red;
            for (int i = 0; i < timestampCount; ++i) {
                Gizmos.DrawSphere(bufferedRemoteStates[i].Position, 0.05f);
            }
        }
#endif
    }
}