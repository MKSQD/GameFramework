using Cube.Replication;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    public abstract class Pawn : ReplicaBehaviour {
        public delegate void PawnEvent(Pawn pawn);

        static List<Pawn> all = new List<Pawn>();

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
        public PawnEvent onDestroy;

        static bool _isApplicationQuitting;

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

        public virtual bool CanBePossessedBy(PawnController controller) { return true; }

        public abstract void SetupPlayerInputComponent(PawnInput input);

        public virtual void Teleport(Vector3 targetPosition, Quaternion targetRotation) { }


        public static void TickAll() {
            foreach (var pawn in all) {
                pawn.Tick();
            }
        }

        public void Tick() {
            if (controller != null) {
                if (movement != null) {
                    movement.Tick();
                }
            }
            TickImpl();
        }

        protected virtual void TickImpl() { }


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
            all.Add(this);
        }

        protected virtual void OnDisable() {
            all.Remove(this);
            if (!_isApplicationQuitting) {
                onDestroy?.Invoke(this);
            }
        }

        void OnApplicationQuit() {
            _isApplicationQuitting = true;
        }
    }
}