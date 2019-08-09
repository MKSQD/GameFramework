using UnityEngine;

namespace GameFramework {
    public interface IPawnMovement {
        void AddMoveInput(Vector3 worldDirection);
        void AddYawInput(float value);
        void AddPitchInput(float value);
        void OnEnterLadder();
        void OnExitLadder();

        void Tick();
    }
}