using System;
using UnityEngine;

namespace GameFramework {
    public interface ICharacterMovement : IPawnMovement {
        event Action Jumped, Landed, DeathByLanding;

        bool IsMoving { get; }
        bool IsSneaking { get; }
        bool IsGrounded { get; }
        Vector3 Velocity { get; }
        Vector3 LocalVelocity { get; }
        PhysicMaterial GroundMaterial { get; }
        CharacterMovementSettings Settings { get; }

        void SetLook(Vector2 value);
        void SetMove(Vector2 value);
        void SetSneaking(bool value);
        void Jump();
        void Teleport(Vector3 targetPosition, Quaternion targetRotation);
        void Disable();
        void Enable();
    }
}