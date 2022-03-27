using System;
using UnityEngine.Assertions;

namespace GameFramework {
    public abstract class PawnController {
        public Action<Pawn> Possessed;
        public Action Unpossessed;

        public Pawn Pawn { get; private set; }

        public bool Possess(Pawn newPawn) {
            Assert.IsNotNull(newPawn);

            if (newPawn.isServer && !newPawn.CanBePossessedBy(this))
                return false;

            if (Pawn == newPawn)
                return true;

            var previousPawn = Pawn;

            Unpossess();

            newPawn.Controller?.Unpossess();

            Pawn = newPawn;

            newPawn.NotifyPossessed(this, previousPawn);
            OnPossessed(newPawn);
            Possessed?.Invoke(newPawn);
            return true;
        }

        public void Unpossess() {
            if (Pawn == null)
                return;

            try {
                Pawn.NotifyUnpossessed();
                OnUnpossessed();
                Unpossessed?.Invoke();
            } finally {
                Pawn = null;
            }
        }

        public abstract void Update();
        public abstract void Tick();

        protected abstract void OnPossessed(Pawn pawn);
        protected abstract void OnUnpossessed();
    }
}