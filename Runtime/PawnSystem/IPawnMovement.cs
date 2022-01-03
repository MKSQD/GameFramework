using Cube.Transport;

namespace GameFramework {
    public interface IPawnMovement {
        IBitSerializable CreateMove();
        void WriteMoveResult(BitWriter bs);
        void ApplyMoveResult(BitReader bs);
        void ExecuteMove(IBitSerializable move);
    }
}