using Cinemachine;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : Pawn {
        public Transform view;
        public new CinemachineVirtualCamera camera;
        public float moveSpeed = 1;
        public float runSpeed = 1;
        public float jumpForce = 12;
        public float groundControl = 0.95f;
        public float airControl = 0.1f;
        public bool gravity = true;
        public float pushPower = 2f;

        public new CharacterMovement movement {
            get;
            internal set;
        }

        public Vector3 velocity {
            get { return characterController.velocity; }
        }

        public Vector3 localVelocity {
            get { return transform.InverseTransformDirection(characterController.velocity); }
        }

        public bool isGrounded {
            get { return characterController.isGrounded; }
        }

        public CharacterController characterController {
            get;
            internal set;
        }

        public override void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            if (characterController != null) {
                characterController.enabled = false;
            }
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            if (characterController != null) {
                characterController.enabled = true;
            }
        }

        public override void SetupPlayerInputComponent(PawnInput input) {
            input.BindAxis("Mouse X", movement.AddYawInput);
            input.BindAxis("Mouse Y", movement.AddPitchInput);
            input.BindAxis("Horizontal", OnHorizontalInput);
            input.BindAxis("Vertical", OnVerticalInput);
            input.BindAxis("Run", OnRun);
            input.BindAction("Jump", movement.Jump);
        }

        protected override void Awake() {
            base.Awake();

            characterController = GetComponent<CharacterController>();
            movement = GetComponent<CharacterMovement>();

            if (camera != null) {
                camera.enabled = false;
            }
        }

        protected override void TickImpl() {
            if (controller != null && movement != null) {
                movement.Tick();
            }
        }

        protected override void OnPossessionImpl(Pawn previousPawn) {
            if (camera != null) {
                camera.enabled = true;
            }
        }

        protected override void OnUnpossessionImpl() {
            if (camera != null) {
                camera.enabled = false;
            }
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

            body.velocity = pushDir * pushPower;
        }

        void OnHorizontalInput(float value) {
            movement.AddMoveInput(transform.right * value);
        }

        void OnVerticalInput(float value) {
            movement.AddMoveInput(transform.forward * value);
        }

        void OnRun(float value) {
            movement.SetRun(value > 0.5f);
        }

        void OnDrawGizmos() {
            if (view != null) {
                Debug.DrawLine(view.transform.position, view.transform.position + view.transform.forward * 0.25f, Color.yellow);
            }
        }
    }
}