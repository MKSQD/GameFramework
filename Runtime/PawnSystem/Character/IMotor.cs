using UnityEngine;

namespace GameFramework {
    public interface IMotor {
        Vector3 Velocity { get; }
        float Height { get; set; }
        float Radius { get; }
        Vector3 Center { get; set; }

        void Enable();
        void Disable();

        void SetCollisionDetection(bool enabled);

        void Move(Vector3 offset);
        void MoveAbs(Vector3 pos);
    }
}