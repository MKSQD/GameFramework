using Cube.Replication;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFramework {
    [SelectionBase]
    public abstract class Pawn : ReplicaBehaviour {
        public delegate void PawnEvent(Pawn pawn);

        public InputActionAsset InputMap;

        public PawnController Controller {
            get;
            internal set;
        }
        public bool HasController => Controller != null;

        public event PawnEvent OnPossession;
        public event PawnEvent OnUnpossession;

        public void HandlePossession(PawnController controller, Pawn previousPawn) {
            Controller = controller;
            HandlePossessionImpl(previousPawn);
            OnPossession?.Invoke(this);
        }

        public void HandleUnpossession() {
            try {
                OnUnpossession?.Invoke(this);
                HandleUnpossessionImpl();
            } finally {
                Controller = null;
            }
        }

        public virtual bool CanBePossessedBy(PawnController controller) {
            return true;
        }

        public abstract void SetupPlayerInputComponent(PawnInput input);

        protected abstract void HandlePossessionImpl(Pawn previousPawn);
        protected abstract void HandleUnpossessionImpl();

        protected virtual void Awake() {
        }

        protected virtual void Update() {
            Controller?.Update();
        }

        protected virtual void OnEnable() {
        }

        protected virtual void OnDisable() {
            Controller?.Unpossess();
        }
    }
}