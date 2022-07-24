using Cube.Transport;
using UnityEditor;
using UnityEngine;

namespace GameFramework {
    public class CharacterCommand : IBitSerializable {
        public Vector2 Stick;
        public float Yaw, ViewPitch;
        public bool Walk, Crouch, Jump;

        public virtual void Serialize(IBitWriter bs) {
            bs.WriteLossyFloat(Stick.x, -1, 1);
            bs.WriteLossyFloat(Stick.y, -1, 1);
            bs.WriteLossyFloat(Yaw, 0, 360, 0.25f);
            bs.WriteLossyFloat(ViewPitch, CharacterMovement.MinViewPitch, CharacterMovement.MaxViewPitch, 2);
            bs.WriteBool(Walk);
            bs.WriteBool(Crouch);
            bs.WriteBool(Jump);
        }

        public virtual void Deserialize(BitReader bs) {
            Stick.x = bs.ReadLossyFloat(-1, 1);
            Stick.y = bs.ReadLossyFloat(-1, 1);
            Yaw = bs.ReadLossyFloat(0, 360, 0.25f);
            ViewPitch = bs.ReadLossyFloat(CharacterMovement.MinViewPitch, CharacterMovement.MaxViewPitch, 2);
            Walk = bs.ReadBool();
            Crouch = bs.ReadBool();
            Jump = bs.ReadBool();
        }
    }

    public class CharacterState : IBitSerializable {
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
            input.BindFloatAxis("Gameplay/Walk", value => Movement.SetWalk(value > 0.5f));
            input.BindStartedAction("Gameplay/ToggleCrouch", () => Movement.SetCrouch(!Movement.CrouchInput));
            input.BindStartedAction("Gameplay/Jump", Movement.Jump);
        }

        public virtual IBitSerializable CreateCommand() => new CharacterCommand();
        public virtual IBitSerializable ConsumeCommand() {
            var newMove = (CharacterCommand)CreateCommand();
            Movement.ConsumeCommand(ref newMove);
            return newMove;
        }
        public virtual void ExecuteCommand(IBitSerializable cmd) => Movement.ExecuteCommand((CharacterCommand)cmd);

        public IBitSerializable CreateState() => new CharacterState();
        public void GetState(ref IBitSerializable state) {
            var state2 = (CharacterState)state;
            Movement.GetState(ref state2);
        }
        public void ResetToState(IBitSerializable state) => Movement.ResetToState((CharacterState)state);


        public void Teleport(Vector3 targetPosition, Quaternion targetRotation) => Movement.Teleport(targetPosition, targetRotation);


        public void BeforeCommands() => Movement.BeforeCommands();
        public void AfterCommands() => Movement.AfterCommands();


        protected virtual void Awake() {
            Movement = GetComponent<CharacterMovement>();
        }

        protected virtual void OnLook(Vector2 value) => Movement.AddLook(value);

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