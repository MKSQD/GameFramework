using UnityEngine;

namespace GameFramework {
    public class RigidbodyMotor : MonoBehaviour, IMotor {
        public Vector3 Velocity => _rb.velocity * 0.01f;

        public float Height {
            get => _cc.height;
            set => _cc.height = value;
        }

        public float Radius => _cc.radius;

        public Vector3 Center {
            get => _cc.center;
            set => _cc.center = value;
        }

        [SerializeField]
        Rigidbody _rb;
        [SerializeField]
        CapsuleCollider _cc;

        public void Disable() {
            _rb.isKinematic = true;
        }

        public void Enable() {
            _rb.isKinematic = false;
        }

        public void Move(Vector3 offset) {
            _rb.velocity = offset * 50;
        }

        public void MoveAbs(Vector3 pos) {
            _rb.MovePosition(pos);
        }

        public void SetCollisionDetection(bool enabled) {
        }

        void OnValidate() {
            _cc = GetComponent<CapsuleCollider>();
            _rb = GetComponent<Rigidbody>();
        }

        void Start() {
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.drag = 1;
        }
    }
}