using System;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;


namespace GameFramework {
    public struct StartedSneakingEvent : IEvent { }
    public struct StoppedSneakingEvent : IEvent { }

    [AddComponentMenu("CharacterSystem/CharacterMovement")]
    public class CharacterMovement : ReplicaBehaviour {
        public event Action Jumped, Landed;

        public const float MinViewPitch = -65, MaxViewPitch = 70;

        static public float MouseSensitivity = 1;

        public CharacterMovementSettings Settings;

        public Vector3 Velocity => _motor.Velocity;
        public Vector3 LocalVelocity => transform.InverseTransformDirection(Velocity);

        public bool IsMoving => LastStick.sqrMagnitude > 0.01f;//Velocity.sqrMagnitude > 0.01f;
        public bool IsWalking => _walkInput;
        public bool IsCrouching { get; private set; }
        public bool Crouch => _crouchInput;
        public bool IsGrounded { get; private set; }
        public PhysicMaterial GroundMaterial { get; private set; }
        public float Height => _motor.Height;
        public bool InProceduralMovement { get; set; }
        public Vector2 LastStick { get; private set; }
        public bool IsOnLadder { get; private set; }

        public float Yaw { get; private set; }
        public float ViewPitch { get; private set; }

        IMotor _motor;
        Character _character;

        Vector2 _stickInput;
        bool _walkInput, _crouchInput, _jumpInput;

        Vector3 _velocity;
        byte _jumpFrames;

        RemoteInterp _remoteInterp;

        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            _motor.Disable();

            transform.position = targetPosition;
            transform.rotation = targetRotation;
            Yaw = targetRotation.eulerAngles.y;

            _motor.Enable();
        }

        public void SetMove(Vector2 value) {
            //if (_character.Health.IsDowned)
            //    return;

            // Respect deadzone because Unitys InputSystem ones is broken
            _stickInput.x = Mathf.Abs(value.x) > 0.15f ? value.x : 0;
            _stickInput.y = Mathf.Abs(value.y) > 0.15f ? value.y : 0;
        }

        public void AddLook(Vector2 value) => SetLook(new Vector2(Yaw + value.x * MouseSensitivity, ViewPitch + value.y * MouseSensitivity));

        public void SetLook(Vector2 value) {
            //if (_character.Health.IsDowned)
            //    return;

            Yaw = Mathf.Repeat(value.x, 360);
            ViewPitch = Mathf.Clamp(value.y, MinViewPitch, MaxViewPitch);
        }

        public void SetWalk(bool value) => _walkInput = value;
        public void SetCrouch(bool value) => _crouchInput = value;
        public void Jump() {
            //if (_character.Health.IsDowned)
            //    return;

            _jumpInput = true;
        }

        public void Disable() => _motor.SetCollisionDetection(false);
        public void Enable() => _motor.SetCollisionDetection(true);

        public void OnEnterLadder() => IsOnLadder = true;
        public void OnExitLadder() => IsOnLadder = false;


        bool _jumpNotch;
        bool _wasGrounded;
        Vector3 _lastPos;
        protected void Update() {
            if (IsGrounded != _wasGrounded) {
                _wasGrounded = IsGrounded;
                if (IsGrounded) {
                    Landed?.Invoke();
                }
            }

            if (isClient) {
                if (!isOwner) {
                    UpdateRemote();
                    return;
                }

                _character.View.localRotation = Quaternion.AngleAxis(ViewPitch, Vector3.left);
                transform.localRotation = Quaternion.AngleAxis(Yaw, Vector3.up);

                if (_jumpFrames > 0) {
                    if (!_jumpNotch) {
                        Jumped?.Invoke();
                        _jumpNotch = true;
                    }
                } else {
                    if (_jumpNotch) {
                        _jumpNotch = false;
                    }
                }
            }
        }

        public void ConsumeCommand(ref CharacterCommand cmd) {
            cmd.Stick = _stickInput;
            cmd.Walk = _walkInput;
            cmd.Crouch = _crouchInput;
            cmd.Jump = _jumpInput;
            cmd.Yaw = Yaw;
            cmd.ViewPitch = ViewPitch;

            _jumpInput = false;
            _stickInput = Vector2.zero;
        }

        public void GetState(ref CharacterState state) {
            state.Position = transform.position;
            state.Velocity = _velocity;
            state.IsGrounded = IsGrounded;
            state.JumpFrames = _jumpFrames;
        }

        public void ResetToState(CharacterState move) {
            _motor.Disable();
            transform.position = move.Position;
            _velocity = move.Velocity;
            IsGrounded = move.IsGrounded;
            _jumpFrames = move.JumpFrames;
            _motor.Enable();
        }

        public void InterpState(CharacterState oldState, CharacterState newState, float a) {
            Assert.IsTrue(a >= 0 && a <= 1);

            var pos = Vector3.Lerp(oldState.Position, newState.Position, a);

            transform.position = pos;
        }

        public void ExecuteCommand(CharacterCommand cmd) {
            if (isServer && !isOwner) { // For Serialize
                Yaw = cmd.Yaw;
                ViewPitch = cmd.ViewPitch;
            }

            //if (_character.Health.IsDowned) {
            //    LastStick = Vector2.zero;
            //    return;
            //}

            LastStick = cmd.Stick;
            _character.View.localRotation = Quaternion.AngleAxis(cmd.ViewPitch, Vector3.left);
            transform.localRotation = Quaternion.AngleAxis(cmd.Yaw, Vector3.up);

            // Gravity
            if (_jumpFrames == 0) {
                _velocity.y += Settings.Gravity;
            }

            // Jump
            if (_jumpFrames > 0) {
                --_jumpFrames;
                var percent = _jumpFrames / (float)Settings.JumpFrames;
                _velocity.y += Settings.JumpForce * (percent * percent);
            }

            if (cmd.Jump && IsGrounded && _jumpFrames == 0) {
                _jumpFrames = Settings.JumpFrames;
                _velocity.y = Settings.JumpForce;
                SetCrouch(false);
            }

            // Move
            var newDesiredMovement = cmd.Stick;
            if (newDesiredMovement.sqrMagnitude > 1) {
                newDesiredMovement.Normalize();
            }

            if (IsOnLadder) {
                _velocity.y = newDesiredMovement.y * 2;
                newDesiredMovement.y = 0;
            }

            newDesiredMovement = ApplyMovementModifiers(newDesiredMovement, cmd);

            _velocity.x = Mathf.LerpUnclamped(_velocity.x, newDesiredMovement.x, 0.2f);
            _velocity.z = Mathf.LerpUnclamped(_velocity.z, newDesiredMovement.y, 0.2f);

            _motor.Move(transform.rotation * _velocity);

            //
            UpdateCrouch(cmd.Crouch);
            UpdateGround();
        }

        static readonly RaycastHit[] _groundHits = new RaycastHit[3];
        static readonly Collider[] _groundColliders = new Collider[3];

        void UpdateGround() {
            var layerMask = isClient ? Settings.ClientGroundMask : Settings.ServerGroundMask;

            IsGrounded = IsOnLadder || InProceduralMovement;

            switch (Settings.GroundDetection) {
                case CharacterMovementSettings.GroundDetectionQuality.None:
                    break;

                case CharacterMovementSettings.GroundDetectionQuality.Ray: {
                        var epsilon = 0.1f;
                        var pos = transform.position + Vector3.up * (_motor.Radius - epsilon);

                        var num = Physics.RaycastNonAlloc(transform.position + Vector3.up * epsilon, Vector3.down, _groundHits, 2, layerMask);
                        for (int i = 0; i < num; ++i) {
                            var groundHit = _groundHits[i];
                            if (groundHit.distance > epsilon * 5)
                                continue;

                            GroundMaterial = groundHit.collider.sharedMaterial;
                            IsGrounded = true;
                            break;
                        }


                        break;
                    }
                case CharacterMovementSettings.GroundDetectionQuality.Volume: {
                        var pos = transform.position + Vector3.up * (_motor.Radius - 0.06f);
                        var radius = _motor.Radius - 0.005f;
                        var num = Physics.OverlapSphereNonAlloc(pos, radius, _groundColliders, layerMask);
                        for (int i = 0; i < num; ++i) {
                            var collider = _groundColliders[i];
                            GroundMaterial = collider.sharedMaterial;
                            IsGrounded = true;
                        }
                        break;
                    }
            }
        }

        void UpdateCrouch(bool crouch) {
            if (!IsCrouching && crouch) {
                // We can always crouch, no checks needed
                if (isClient && isOwner) {
                    EventHub<StartedSneakingEvent>.EmitDefault();
                }

                ResizeCC(Settings.CrouchRelativeHeight);
                IsCrouching = true;
                return;
            }
            if (IsCrouching && !crouch) {
                if (CanStandUp()) {
                    if (isClient && isOwner) {
                        EventHub<StoppedSneakingEvent>.EmitDefault();
                    }

                    ResizeCC(1f / Settings.CrouchRelativeHeight);
                    IsCrouching = false;
                }
                return;
            }
        }

        bool CanStandUp() {
            var uncrouchedHeight = _motor.Height / Settings.CrouchRelativeHeight;

            var pos = transform.position;
            var off = (uncrouchedHeight * 0.5f) * Vector3.up;
            var p0 = pos + _motor.Center;
            var p1 = pos + _motor.Center + off; // #todo not 100% correct, dont know why


            var check = Physics.CheckCapsule(p0, p1, _motor.Radius, (1 << 0));
            //DebugExt.DrawWireCapsule(p0, p1, _motor.Radius, !check ? Color.green : Color.red);

            return !check;
        }

        Vector2 ApplyMovementModifiers(Vector2 localMovement, CharacterCommand cmd) {
            if (cmd.Walk) {
                localMovement *= Settings.WalkSpeed;
            } else if (IsCrouching) {
                localMovement *= Settings.CrouchSpeed;
            } else {
                localMovement *= Settings.RunSpeed;
            }

            if (localMovement.y <= 0) {
                localMovement.y *= Settings.BackwardSpeedModifier;
            }
            localMovement.x *= Settings.SideSpeedModifier;

            // Buffs
            //float speedBuff = 1;
            //_character.Stats.ModifyStat(CharacterStat.MovementSpeed, ref speedBuff);
            //localMovement *= speedBuff;

            return localMovement;
        }

        void UpdateRemote() {
            Assert.IsTrue(isClient);

            if (_remoteInterp == null) {
                _remoteInterp = new RemoteInterp(Settings.InterpMode);
            }

            var pos = transform.position;
            var rot = transform.rotation;
            var result = _remoteInterp.Sample(ref pos, ref rot);
            transform.rotation = rot;
            if (result.Jumped) {
                Jumped?.Invoke();
            }
            SetWalk(result.Walking);
            SetCrouch(result.IsCrouching);

            _character.View.localRotation = Quaternion.AngleAxis(result.ViewPitch, Vector3.left);

            LastStick = Vector2.LerpUnclamped(LastStick, result.Move, Time.deltaTime * 5);

            if (!result.ShouldTeleport) {
                _motor.Move(pos - transform.position);
            } else {
                Teleport(pos, rot);
            }

            UpdateCrouch(result.IsCrouching);
            UpdateGround();
        }

        public override void Serialize(IBitWriter bs, SerializeContext ctx) {
            if (ctx.IsOwner)
                return;

            bs.WriteLossyFloat(transform.position.x, -5000, 5000, 0.01f);
            bs.WriteLossyFloat(transform.position.z, -5000, 5000, 0.01f);
            bs.WriteLossyFloat(transform.position.y, -50, 500, 0.01f);
            bs.WriteLossyFloat(Yaw, 0, 360, 1);
            bs.WriteLossyFloat(ViewPitch, MinViewPitch, MaxViewPitch, 1);
            bs.WriteLossyFloat(LastStick.x, -1, 1, 0.25f);
            bs.WriteLossyFloat(LastStick.y, -1, 1, 0.25f);
            bs.WriteBool(IsWalking);
            bs.WriteBool(IsCrouching);
            bs.WriteBool(_jumpInput);
        }

        public override void Deserialize(BitReader bs) {
            if (isOwner)
                return;

            if (_remoteInterp == null) {
                _remoteInterp = new RemoteInterp(Settings.InterpMode);
            }

            Vector3 pos;
            pos.x = bs.ReadLossyFloat(-5000, 5000, 0.01f);
            pos.z = bs.ReadLossyFloat(-5000, 5000, 0.01f);
            pos.y = bs.ReadLossyFloat(-50, 500, 0.01f);

            var yaw = bs.ReadLossyFloat(0, 360, 1);
            var viewPitch = bs.ReadLossyFloat(MinViewPitch, MaxViewPitch, 1);

            Vector2 move;
            move.x = bs.ReadLossyFloat(-1, 1, 0.25f);
            move.y = bs.ReadLossyFloat(-1, 1, 0.25f);

            var walking = bs.ReadBool();
            var crouching = bs.ReadBool();
            var jumped = bs.ReadBool();

            _remoteInterp.AddState(pos, move, yaw, viewPitch, walking, crouching, jumped);
        }



        public void ResizeCC(float val) {
            var pos = _character.View.localPosition;
            pos.y *= val;
            _character.View.localPosition = pos;

            _motor.Height *= val;
            _motor.Center = new Vector3(0, _motor.Height / 2, 0);
        }

        void Awake() {
            // Hack: Hide until the first Deserialize is called
            transform.position = Vector3.down * 50;

            _motor = GetComponent<IMotor>();
            _character = GetComponent<Character>();
            Assert.IsNotNull(_motor);
            Assert.IsNotNull(Settings);
            Assert.IsNotNull(_character);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() {
            _remoteInterp?.DrawDebugGizmos();
        }
#endif
    }
}