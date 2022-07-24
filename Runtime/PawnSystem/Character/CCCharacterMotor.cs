using UnityEngine;

namespace GameFramework {
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("GameFramework/Character/CharacterController Motor")]
    public sealed class CCCharacterMotor : MonoBehaviour, ICharacterMotor {
        public Vector3 Velocity => _cc.velocity;

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
        CharacterController _cc;

        public void Disable() => _cc.enabled = false;
        public void Enable() => _cc.enabled = true;

        public void Move(Vector3 offset) => _cc.Move(offset);

        public void SetCollisionDetection(bool enabled) {
            _cc.detectCollisions = enabled;
            _cc.enableOverlapRecovery = enabled;
        }

        void OnValidate() {
            _cc = GetComponent<CharacterController>();
        }
    }
}