using System;
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

    /// <summary>
    /// Generic, client or AI controllable Thing.
    /// </summary>
    [SelectionBase]
    public abstract class Pawn : ReplicaBehaviour, IAuthorativePawnMovement {
        public InputActionAsset InputMap;

        public PawnController Controller { get; private set; }
        public bool HasController => Controller != null;

        public event Action Possessed, Unpossessed;

        public void NotifyPossessed(PawnController controller, Pawn previousPawn) {
            Controller = controller;
            OnPossession(previousPawn);
            Possessed?.Invoke();
        }

        public void NotifyUnpossessed() {
            try {
                Unpossessed?.Invoke();
                OnUnpossession();
            } finally {
                Controller = null;
            }
        }

        public virtual bool CanBePossessedBy(PawnController controller) => true;

        /// <summary>
        /// Called on the client when it possesses this Pawn.
        /// Connects the InputSystem to actions on the Pawn.
        /// Server-side AI doesn't use this string-based mapping
        /// but is supposed to call methods on the Pawn directly.
        /// </summary>
        public abstract void SetupPlayerInput(PlayerInput input);

        public abstract IBitSerializable ConsumeCommand();
        public abstract IBitSerializable CreateCommand();
        public abstract void ExecuteCommand(IBitSerializable move);

        public abstract IBitSerializable CreateState();
        public abstract void GetState(ref IBitSerializable state);
        public virtual void InterpState(IBitSerializable oldState, IBitSerializable newState, float a) { }
        public abstract void ResetToState(IBitSerializable state);

        public abstract void Teleport(Vector3 targetPosition, Quaternion targetRotation);

        protected virtual void OnPossession(Pawn previousPawn) { }
        protected virtual void OnUnpossession() { }
    }
}