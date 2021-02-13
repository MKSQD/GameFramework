using UnityEngine;

namespace GameFramework {
    [CreateAssetMenu(menuName = "GameFramework/CharacterMovementSettings")]
    public class CharacterMovementSettings : ScriptableObject {
        public float moveSpeed = 2;
        public float runSpeed = 5;
        public float backwardSpeedModifier = 0.7f;
        public float sideSpeedModifier = 0.9f;
        public float jumpForce2 = 18;
        public float groundControl = 0.9f;
        public float airControl = 0.1f;
        public bool useGravity = true;
        public float pushPower = 2f;

        [Header("Interpolation/Extrapolation")]
        [Range(0, 1)]
        public float InterpolationBackTime = 0.15f;
        [Range(0, 1)]
        public float ExtrapolationLimit = 0.3f;
    }
}