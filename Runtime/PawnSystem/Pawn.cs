using Cube.Replication;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFramework {
    public interface IPawnMove {
        IMove GetCurrentMove();
        void ResetCurrentMove();
        IMove CreateMove();
        void ResetToState(IMove move);
        void ExecuteMove(IMove move);

        void Teleport(Vector3 targetPosition, Quaternion targetRotation);
    }

    [SelectionBase]
    public abstract class Pawn : ReplicaBehaviour, IPawnMove {
        public delegate void PawnEvent(Pawn pawn);

        public InputActionAsset InputMap;

        public PawnController Controller { get; private set; }
        public bool HasController => Controller != null;

        public event PawnEvent Possessed, Unpossessed;

        public void NotifyPossessed(PawnController controller, Pawn previousPawn) {
            Controller = controller;
            HandlePossessionImpl(previousPawn);
            Possessed?.Invoke(this);
        }

        public void NotifyUnpossessed() {
            try {
                Unpossessed?.Invoke(this);
                HandleUnpossessionImpl();
            } finally {
                Controller = null;
            }
        }

        public virtual bool CanBePossessedBy(PawnController controller) => true;

        public abstract void SetupPlayerInputComponent(PlayerInput input);
        public abstract IMove GetCurrentMove();
        public abstract void ResetCurrentMove();
        public abstract IMove CreateMove();
        public abstract void ResetToState(IMove move);
        public abstract void ExecuteMove(IMove move);
        public abstract void Teleport(Vector3 targetPosition, Quaternion targetRotation);

        protected abstract void HandlePossessionImpl(Pawn previousPawn);
        protected abstract void HandleUnpossessionImpl();

        protected virtual void OnEnable() { }

        protected virtual void OnDisable() => Controller?.Unpossess();
    }
}