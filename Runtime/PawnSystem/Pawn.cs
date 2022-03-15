using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFramework {
    public interface IAuthorativePawnMovement {
        /// <summary>
        /// ConsumeMove creates a new move initialized with the current input values.
        /// Note that some input values should be reset here.
        /// </summary>
        IBitSerializable ConsumeCommand();
        /// <summary>
        /// CreateMove creates a new, empty move.
        /// </summary>
        IBitSerializable CreateCommand();
        /// <summary>
        /// ExecuteMove simulates move relative to the current state.
        /// </summary>
        void ExecuteCommand(IBitSerializable move);

        IBitSerializable CreateState();
        void GetState(ref IBitSerializable state);
        /// <summary>
        /// For client display.
        /// </summary>
        void InterpState(IBitSerializable oldState, IBitSerializable newState, float a);
        /// <summary>
        /// ResetToState resets the instance to the RESULT values of move.
        /// </summary>
        void ResetToState(IBitSerializable move);


        void Teleport(Vector3 targetPosition, Quaternion targetRotation);
    }

    [SelectionBase]
    public abstract class Pawn : ReplicaBehaviour, IAuthorativePawnMovement {
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

        public abstract void SetupPlayerInput(PlayerInput input);

        public abstract IBitSerializable ConsumeCommand();
        public abstract IBitSerializable CreateCommand();
        public abstract void ExecuteCommand(IBitSerializable move);

        public abstract IBitSerializable CreateState();
        public abstract void GetState(ref IBitSerializable state);
        public virtual void InterpState(IBitSerializable oldState, IBitSerializable newState, float a) { }
        public abstract void ResetToState(IBitSerializable state);

        public abstract void Teleport(Vector3 targetPosition, Quaternion targetRotation);

        protected abstract void HandlePossessionImpl(Pawn previousPawn);
        protected abstract void HandleUnpossessionImpl();

        protected virtual void OnEnable() { }
        protected virtual void OnDisable() => Controller?.Unpossess();
    }
}