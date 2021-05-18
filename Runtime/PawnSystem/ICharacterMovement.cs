using UnityEngine;

namespace GameFramework {
    public delegate void CharacterEvent(Character character);

    public interface ICharacterMovement : IPawnMovement {
        event CharacterEvent Jumped, Landed;

        void SetLook(Vector2 value);
        void SetMove(Vector2 value);
        void SetRun(bool run);
        void Jump();
        void Teleport(Vector3 targetPosition, Quaternion targetRotation);
        void Disable();
        void Enable();
        void AddVelocity(Vector3 velocity);

        bool IsMoving { get; }
        bool IsRunning { get; }
        bool IsGrounded { get; }
        Vector3 Velocity { get; }
        Vector3 LocalVelocity { get; }
        PhysicMaterial GroundMaterial { get; }
        CharacterMovementSettings Settings { get; }
    }
}