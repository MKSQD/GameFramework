using Cinemachine;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : Pawn {
        public Transform view;
        public new CinemachineVirtualCamera camera;
        
        public new CharacterMovement movement {
            get;
            internal set;
        }

        public override void SetupPlayerInputComponent(PawnInput input) {
            input.BindAxis("Mouse X", OnMouseX);
            input.BindAxis("Mouse Y", OnMouseY);
            input.BindAxis("Horizontal", OnHorizontalInput);
            input.BindAxis("Vertical", OnVerticalInput);
            input.BindAxis("Run", OnRun);
            input.BindAction("Jump", OnJump);
        }

        protected override void Awake() {
            base.Awake();

            movement = GetComponent<CharacterMovement>();

            if (camera != null) {
                camera.enabled = false;
            }
        }

        protected override void HandlePossessionImpl(Pawn previousPawn) {
            if (camera != null) {
                camera.enabled = true;
            }
        }

        protected override void HandleUnpossessionImpl() {
            if (camera != null) {
                camera.enabled = false;
            }
        }

        void OnMouseX(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !GameInstance.Main.CharacterInputEnabled)
                return;
#endif

            movement.AddYawInput(value * 0.5f);
        }

        void OnMouseY(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !GameInstance.Main.CharacterInputEnabled)
                return;
#endif

            movement.AddPitchInput(value * 0.5f);
        }

        void OnHorizontalInput(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !GameInstance.Main.CharacterInputEnabled)
                return;
#endif

            movement.AddMoveInput(Vector3.right * value);
        }

        void OnVerticalInput(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !GameInstance.Main.CharacterInputEnabled)
                return;
#endif

            movement.AddMoveInput(Vector3.forward * value);
        }

        void OnRun(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !GameInstance.Main.CharacterInputEnabled)
                return;
#endif

            movement.SetRun(value > 0.5f);
        }

        void OnJump() {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !GameInstance.Main.CharacterInputEnabled)
                return;
#endif

            movement.Jump();
        }

        void OnDrawGizmos() {
            if (view != null) {
                Debug.DrawLine(view.transform.position, view.transform.position + view.transform.forward * 0.25f, Color.yellow);
            }
        }
    }
}