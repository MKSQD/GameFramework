using Cube.Transport;

namespace GameFramework {
    public interface IPawnMovement {
        ISerializable CreateMove();
        void WriteMoveResult(BitWriter bs);
        void ApplyMoveResult(BitReader bs);
        void ExecuteMove(ISerializable move);
    }
}