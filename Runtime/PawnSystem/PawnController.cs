using System;

namespace GameFramework {
    public abstract class PawnController {
        public Pawn Pawn {
            get;
            internal set;
        }

        public bool Possess(Pawn newPawn) {
            if (newPawn == null)
                throw new ArgumentNullException("newPawn");

            if (newPawn.isServer && !newPawn.CanBePossessedBy(this))
                return false;

            var previousPawn = Pawn;

            Unpossess();

            if (newPawn.Controller != null) {
                newPawn.Controller.Unpossess();
            }

            Pawn = newPawn;

            newPawn.HandlePossession(this, previousPawn);
            OnPossess(newPawn);
            return true;
        }

        public void Unpossess() {
            if (Pawn == null)
                return;

            try {
                Pawn.HandleUnpossession();
                OnUnpossess();
            } finally {
                Pawn = null;
            }
        }

        public abstract void Update();

        protected abstract void OnPossess(Pawn pawn);
        protected abstract void OnUnpossess();
    }
}