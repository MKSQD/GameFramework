using UnityEngine;
using UnityEngine.Events;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : Pawn {
        public Transform view;
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

        public UnityEvent onJump;
        public UnityEvent onLand;

        public CharacterController characterController {
            get;
            internal set;
        }

        protected override void Awake() {
            base.Awake();
            characterController = GetComponent<CharacterController>();
            movement = GetComponent<CharacterMovement>();
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
            if (movement != null) {
                movement.Tick();
            }
        }

//         public override void Serialize(BitStream bs, ReplicaView view) {
//             bs.Write(transform.position);
//             bs.WriteNormalised(_movement);
//         }
// 
//         public override void Deserialize(BitStream bs) {
//             transform.position = bs.ReadVector3();
//             _movement = bs.ReadNormalisedVector3();
//         }

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
    }
}