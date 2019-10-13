using System;

namespace GameFramework {
    public abstract class PawnController {
        public Pawn pawn {
            get;
            internal set;
        }

        public void Possess(Pawn newPawn) {
            if (newPawn == null)
                throw new ArgumentNullException("newPawn");

            if (newPawn.isServer && !newPawn.CanBePossessedBy(this))
                return;

            var previousPawn = pawn;

            Unpossess();

            if (newPawn.controller != null) {
                newPawn.controller.Unpossess();
            }

            pawn = newPawn;

            newPawn.OnPossession(this, previousPawn);
            OnPossess(newPawn);
        }

        public void Unpossess() {
            if (pawn == null)
                return;

            try {
                pawn.OnUnpossession();
                OnUnpossess();
            }
            finally {
                pawn = null;
            }
        }

        public abstract void Update();

        protected abstract void OnPossess(Pawn pawn);
        protected abstract void OnUnpossess();
    }
}