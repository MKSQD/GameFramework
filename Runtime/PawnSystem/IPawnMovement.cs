using UnityEngine;

namespace GameFramework {
    public interface IPawnMovement {
        void Teleport(Vector3 position, Quaternion rotation);
    }
}