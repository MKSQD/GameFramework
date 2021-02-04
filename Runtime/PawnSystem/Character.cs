using Cinemachine;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : Pawn {
        public Transform view;
        public CinemachineVirtualCamera Camera;
        
        public new CharacterMovement Movement {
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

            Movement = GetComponent<CharacterMovement>();

            if (Camera != null) {
                Camera.enabled = false;
            }
        }

        protected override void HandlePossessionImpl(Pawn previousPawn) {
            if (Camera != null) {
                Camera.enabled = true;
            }
        }

        protected override void HandleUnpossessionImpl() {
            if (Camera != null) {
                Camera.enabled = false;
            }
        }

        void OnMouseX(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.AddYawInput(value * 0.5f);
        }

        void OnMouseY(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.AddPitchInput(value * 0.5f);
        }

        void OnHorizontalInput(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.AddMoveInput(Vector3.right * value);
        }

        void OnVerticalInput(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.AddMoveInput(Vector3.forward * value);
        }

        void OnRun(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetRun(value > 0.5f);
        }

        void OnJump() {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.Jump();
        }

        void OnDrawGizmosSelected() {
            if (view != null) {
                Debug.DrawLine(view.transform.position, view.transform.position + view.transform.forward * 0.5f, Color.yellow);
            }
        }
    }
}