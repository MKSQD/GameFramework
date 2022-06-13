using UnityEngine;

namespace GameFramework {
    [CreateAssetMenu(menuName = "GameFramework/Movement Settings")]
    public class CharacterMovementSettings : ScriptableObject {
        public enum GroundDetectionQuality { None, Ray, Volume }

        [Header("World")]
        public float WorldBoundsX = 10000, WorldBoundsY = 10000, WorldBoundsZ = 10000;

        [Header("Movement")]
        public LayerMask ClientGroundMask;
        public LayerMask ServerGroundMask;
        [Range(0, 1)]
        public float GroundControl = 1;
        [Range(0, 1)]
        public float AirControl = 0.2f;
        public float Gravity = -0.03f;
        public float WalkSpeed = 0.1f;
        public float RunSpeed = 1;
        public float BackwardSpeedModifier = 0.65f;
        public float SideSpeedModifier = 0.8f;

        [Header("Ground")]
        public GroundDetectionQuality GroundDetection = GroundDetectionQuality.Ray;

        [Header("Jumping")]
        [Range(0.01f, 5)]
        public float JumpForce = 0.3f;
        [Range(1, 20)]
        public byte JumpFrames = 8;


        [Header("Crouching")]
        [Range(0.1f, 0.99f)]
        public float CrouchRelativeHeight = 0.5f;
        public float CrouchSpeed = 2;
    }
}