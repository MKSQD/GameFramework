﻿using Cube.Transport;
using UnityEditor;
using UnityEngine;

namespace GameFramework {
    public class CharacterCommand : IPawnCommand {
        public Vector2 Stick;
        public float Yaw, ViewPitch;
        public bool Run, Crouch, Jump;

        public virtual void Serialize(IBitWriter bs) {
            bs.WriteLossyFloat(Stick.x, -1, 1);
            bs.WriteLossyFloat(Stick.y, -1, 1);
            bs.WriteLossyFloat(Yaw, 0, 360, 0.25f);
            bs.WriteLossyFloat(ViewPitch, CharacterMovement.MinViewPitch, CharacterMovement.MaxViewPitch, 1);
            bs.WriteBool(Run);
            bs.WriteBool(Crouch);
            bs.WriteBool(Jump);
        }

        public virtual void Deserialize(BitReader bs) {
            Stick.x = bs.ReadLossyFloat(-1, 1);
            Stick.y = bs.ReadLossyFloat(-1, 1);
            Yaw = bs.ReadLossyFloat(0, 360, 0.25f);
            ViewPitch = bs.ReadLossyFloat(CharacterMovement.MinViewPitch, CharacterMovement.MaxViewPitch, 1);
            Run = bs.ReadBool();
            Crouch = bs.ReadBool();
            Jump = bs.ReadBool();
        }
    }

    public class CharacterState : IPawnState {
        public Vector3 Position;
        public Vector3 Velocity;
        public bool IsGrounded;
        public byte JumpFrames;

        public void Deserialize(BitReader bs) {
            Position = bs.ReadVector3();
            Velocity = bs.ReadVector3();
            IsGrounded = bs.ReadBool();
            JumpFrames = bs.ReadByte();
        }

        public void Serialize(IBitWriter bs) {
            bs.WriteVector3(Position);
            bs.WriteVector3(Velocity);
            bs.WriteBool(IsGrounded);
            bs.WriteByte(JumpFrames);
        }
    }

    [AddComponentMenu("GameFramework/Character")]
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : Pawn, IAuthorativePawnMovement {
        public Transform View;
        public CharacterMovement Movement { get; private set; }

        public override void SetupPlayerInput(PlayerInput input) {
            input.BindVector2Axis("Gameplay/Look", OnLook);
            input.BindVector2Axis("Gameplay/Move", Movement.SetMove);
            input.BindFloatAxis("Gameplay/Walk", value => Movement.SetIsRunning(value > 0.5f));
            input.BindStartedAction("Gameplay/ToggleCrouch", () => Movement.SetIsCrouching(!Movement.CrouchInput));
            input.BindStartedAction("Gameplay/Jump", Movement.Jump);
        }

        public virtual IPawnCommand CreateCommand() => new CharacterCommand();
        public virtual IPawnCommand ConsumeCommand() {
            var newMove = (CharacterCommand)CreateCommand();
            Movement.ConsumeCommand(ref newMove);
            return newMove;
        }
        public virtual void ExecuteCommand(IPawnCommand cmd) => Movement.ExecuteCommand((CharacterCommand)cmd);

        public IPawnState CreateState() => new CharacterState();
        public void GetState(ref IPawnState state) {
            var state2 = (CharacterState)state;
            Movement.GetState(ref state2);
        }
        public void ResetToState(IPawnState state) => Movement.ResetToState((CharacterState)state);


        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) => Movement.Teleport(targetPosition, targetRotation);


        protected virtual void Awake() {
            Movement = GetComponent<CharacterMovement>();
        }

        protected virtual void Update() {
            View.localPosition = Vector3.up * 1.1f + View.localRotation * Vector3.up * 0.45f;
        }

        protected virtual void OnLook(Vector2 value) {
            if (Cursor.visible)
                return;

            Movement.AddLook(value);
        }

        protected override void OnPossession(Pawn previousPawn) { }
        protected override void OnUnpossession() { }

#if UNITY_EDITOR
        protected void OnDrawGizmosSelected() {
            if (View != null) {
                Debug.DrawLine(View.transform.position, View.transform.position + View.transform.forward * 0.5f, Color.yellow);
            }
        }

        [MenuItem("GameObject/GameFramework/Character", false, 10)]
        static protected void CreateCustomGameObject(MenuCommand menuCommand) {
            var go = new GameObject("Character");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
            var cc = go.AddComponent<CharacterController>();
            cc.center = Vector3.up;
            go.AddComponent<CCCharacterMotor>();
            go.AddComponent<Character>();
        }
#endif
    }
}