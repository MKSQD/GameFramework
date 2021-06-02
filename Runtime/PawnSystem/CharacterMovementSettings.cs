using UnityEngine;

namespace GameFramework {
    [CreateAssetMenu(menuName = "GameFramework/CharacterMovementSettings")]
    public class CharacterMovementSettings : ScriptableObject {

        public bool useGravity = true;


        [Header("Movement")]
        public float SneakSpeed = 2;
        public float runSpeed = 5;
        public float backwardSpeedModifier = 0.7f;
        public float sideSpeedModifier = 0.9f;

        public float groundControl = 0.9f;
        public float airControl = 0.1f;


        [Header("Jumping")]
        [Range(0.01f, 20)]
        public float JumpForce = 0.5f;
        public float JumpGroundedGraceTime = 0.08f;
        [Range(0.1f, 1f)]
        public float JumpCooldown = 0.5f;


        [Header("Interpolation/Extrapolation")]
        [Range(0, 1)]
        public float InterpolationDelay = 0.15f;
        [Range(0, 1)]
        public float ExtrapolationLimit = 0.3f;
    }
}