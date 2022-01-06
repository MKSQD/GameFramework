﻿using Cube.Replication;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFramework {
    public interface IAuthorativePawnMovement {
        /// <summary>
        /// ConsumeMove creates a new move initialized with the current input values.
        /// Note that some input values should be reset here.
        /// </summary>
        IMove ConsumeMove();
        /// <summary>
        /// CreateMove creates a new, empty move.
        /// </summary>
        IMove CreateMove();
        /// <summary>
        /// ResetToState resets the instance to the *result* values of move.
        /// </summary>
        void ResetToState(IMove move);
        /// <summary>
        /// ExecuteMove simulates move relative to the current state *and* writes the results to move.
        /// </summary>
        void ExecuteMove(IMove move);

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

        public abstract void SetupPlayerInputComponent(PlayerInput input);
        public abstract IMove ConsumeMove();
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