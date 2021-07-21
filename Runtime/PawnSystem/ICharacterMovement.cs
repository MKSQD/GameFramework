using System;
using UnityEngine;

namespace GameFramework {
    public interface ICharacterMovement : IPawnMovement {
        event Action Jumped, Landed, DeathByLanding;

        bool IsMoving { get; }
        bool IsWalking { get; }
        bool IsCrouching { get; }
        bool IsGrounded { get; }
        Vector3 Velocity { get; }
        Vector3 LocalVelocity { get; }
        PhysicMaterial GroundMaterial { get; }
        CharacterMovementSettings Settings { get; }
        float Height { get; }
        bool InProceduralMovement { get; set; }

        void AddLook(Vector2 value);
        void SetMove(Vector2 value);
        void SetIsWalking(bool value);
        void SetIsCrouching(bool value);
        void Jump();
        void Teleport(Vector3 targetPosition, Quaternion targetRotation);
        void Disable();
        void Enable();
    }
}