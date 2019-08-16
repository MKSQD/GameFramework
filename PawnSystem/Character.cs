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
        public float jumpForce = 12;
        public float groundControl = 0.95f;
        public float airControl = 0.1f;
        public bool useGravity = true;
        public float pushPower = 2f;

        public CharacterMovement movement {
            get;
            internal set;
        }

        public Vector3 velocity {
            get { return characterController.velocity; }
        }

        public bool isGrounded {
            get { return characterController.isGrounded; }
        }

        public CharacterController characterController {
            get;
            internal set;
        }

        protected override void AwakeImpl() {
            characterController = GetComponent<CharacterController>();
            movement = GetComponent<CharacterMovement>();

            if (camera != null) {
                camera.enabled = false;
            }
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

        protected override void TickImpl() {
            if (controller != null) {
                movement.Tick();
            }
        }

        void OnControllerColliderHit(ControllerColliderHit hit) {
            var body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic)
                return;

            if (hit.moveDirection.y < -0.3f)
                return;

            var pushDir = hit.moveDirection;
            pushDir.y = 0;

            body.velocity = pushDir * pushPower;
        }

        public override void SetupPlayerInputComponent(PlayerInput input) {
            input.BindAxis("Mouse X", movement.AddYawInput);
            input.BindAxis("Mouse Y", movement.AddPitchInput);
            input.BindAxis("Horizontal", OnHorizontalInput);
            input.BindAxis("Vertical", OnVerticalInput);
            input.BindAction("Jump", movement.Jump);
        }

        void OnHorizontalInput(float value) {
            movement.AddMoveInput(transform.right * value);
        }

        void OnVerticalInput(float value) {
            movement.AddMoveInput(transform.forward * value);
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
    }
}