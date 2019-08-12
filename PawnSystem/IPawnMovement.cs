using UnityEngine;

namespace GameFramework {
    public interface IPawnMovement {
        void AddMoveInput(Vector3 direction);

        void OnEnterLadder();
        void OnExitLadder();

        void Tick();
    }
}