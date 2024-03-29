﻿using System;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFramework {
    public interface IPawnCommand : IBitSerializable { }
    public interface IPawnState : IBitSerializable { }

    public interface IAuthorativePawnMovement {
        /// <summary>
        /// ConsumeMove creates a new move initialized with the current input values.
        /// Note that some input values should be reset here.
        /// </summary>
        IPawnCommand ConsumeCommand();
        /// <summary>
        /// CreateMove creates a new, empty move.
        /// </summary>
        IPawnCommand CreateCommand();
        /// <summary>
        /// ExecuteMove simulates move relative to the current state.
        /// </summary>
        void ExecuteCommand(IPawnCommand move);

        IPawnState CreateState();
        void GetState(ref IPawnState state);
        /// <summary>
        /// ResetToState resets the instance to the RESULT values of move.
        /// </summary>
        void ResetToState(IPawnState move);

        void SerializableInitialState(IBitWriter bs) { }
        void DeserializeInitialState(BitReader bs) { }

        void Teleport(Vector3 targetPosition, Quaternion targetRotation);
    }

    /// <summary>
    /// Generic, client or AI controllable Thing.
    /// </summary>
    [SelectionBase]
    public abstract class Pawn : ReplicaBehaviour {
        public delegate void PawnAction(Pawn previousPawn);

        public InputActionAsset InputMap;

        public PawnController Controller { get; private set; }
        public bool HasController => Controller != null;

        public event PawnAction Possessed;
        public event Action Unpossessed;

        public void NotifyPossessed(PawnController controller, Pawn previousPawn) {
            Controller = controller;
            OnPossession(previousPawn);
            Possessed?.Invoke(previousPawn);
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

        protected abstract void OnPossession(Pawn previousPawn);
        protected abstract void OnUnpossession();

        protected virtual void OnDestroy() {
            Controller?.Unpossess();
        }
    }
}