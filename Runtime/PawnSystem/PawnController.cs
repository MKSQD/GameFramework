using System;
using UnityEngine;

namespace GameFramework {
    public abstract class PawnController {
        public Pawn pawn {
            get;
            internal set;
        }

        public abstract void Update();

        protected abstract void OnPossess(Pawn pawn);
        protected abstract void OnUnpossess();

        public void Possess(Pawn newPawn) {
            if (newPawn == null)
                throw new ArgumentNullException("newPawn");

            if (!newPawn.CanBePossessedBy(this))
                return;

            var previousPawn = pawn;
            Debug.Log("  previousPawn = " + previousPawn);

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
    }
}