using UnityEngine;

namespace GameFramework {
    [CreateAssetMenu(menuName = "GameFramework/CharacterMovementSettings")]
    public class CharacterMovementSettings : ScriptableObject {
        [Header("Movement")]
        public LayerMask ClientGroundMask;
        public LayerMask ServerGroundMask;

        public bool UseGravity = true;
        public float WalkSpeed = 2;
        public float RunSpeed = 5;
        public float BackwardSpeedModifier = 0.65f;
        public float SideSpeedModifier = 0.8f;

        public float GroundControl = 0.5f;
        public float AirControl = 0.01f;


        [Header("Movement - Momentum")]
        public bool GainMomentum = false;
        public float Momentum = 2;


        [Header("Jumping")]
        [Range(0.01f, 20)]
        public float JumpForce = 0.5f;
        public float JumpGroundedGraceTime = 0.08f;
        [Range(0.1f, 1f)]
        public float JumpCooldown = 0.5f;


        [Header("Crouching")]
        [Range(0.1f, 0.9f)]
        public float CrouchRelativeHeight = 0.5f;
        [Range(0.1f, 0.9f)]
        public float CrouchRelativeViewHeight = 0.5f;
        public float CrouchSpeed = 2;
        [Range(0, 1)]
        public float CrouchAIPerceptionSightModifier = 0.5f;

        [Header("Interpolation/Extrapolation")]
        [Range(0, 1)]
        public float InterpolationDelay = 0.25f;
        [Range(0, 1)]
        public float ExtrapolationLimit = 0.3f;
    }
}