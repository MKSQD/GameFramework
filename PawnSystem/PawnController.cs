using Cube.Transport;
using System;

namespace GameFramework {
    public abstract class PawnController {
        public Pawn pawn {
            get;
            internal set;
        }

        public abstract void Tick();

        protected abstract void OnPossess(Pawn pawn);
        protected abstract void OnUnpossess();

        public void Possess(Pawn newPawn) {
            if (newPawn == null)
                throw new ArgumentNullException("newPawn");

            Unpossess();

            if (newPawn.controller != null) {
                newPawn.controller.Unpossess();
            }

            pawn = newPawn;
            newPawn.controller = this;

            OnPossess(newPawn);
            newPawn.OnPossession();
        }
        public void Unpossess() {
            if (pawn == null)
                return;

            pawn.OnUnpossession();
            OnUnpossess();

            pawn.controller = null;
            pawn = null;
        }
    }
}