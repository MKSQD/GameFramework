using Cube.Replication;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    [SelectionBase]
    public abstract class Pawn : ReplicaBehaviour {
        public delegate void PawnEvent(Pawn pawn);

        static List<Pawn> All = new List<Pawn>();

        public PawnController controller {
            get;
            internal set;
        }
        public bool hasController {
            get { return controller != null; }
        }

        public IPawnMovement movement;

        public event PawnEvent onPossession;
        public event PawnEvent onUnpossession;

        public void OnPossession(PawnController controller, Pawn previousPawn) {
            this.controller = controller;
            OnPossessionImpl(previousPawn);
            onPossession?.Invoke(this);
        }

        public void OnUnpossession() {
            try {
                onUnpossession?.Invoke(this);
                OnUnpossessionImpl();
            }
            finally {
                controller = null;
            }
        }

        public virtual bool CanBePossessedBy(PawnController controller) {
            return true;
        }

        public abstract void SetupPlayerInputComponent(PawnInput input);

        protected abstract void OnPossessionImpl(Pawn previousPawn);
        protected abstract void OnUnpossessionImpl();

        protected virtual void Awake() {
            movement = GetComponent<IPawnMovement>();
        }

        protected virtual void Update() {
            if (controller != null) {
                controller.Update();
            }
        }

        protected virtual void OnEnable() {
            All.Add(this);
        }

        protected virtual void OnDisable() {
            if (controller != null) {
                controller.Unpossess();
            }

            All.Remove(this);
        }
    }
}