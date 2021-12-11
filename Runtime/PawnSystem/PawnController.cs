using UnityEngine.Assertions;

namespace GameFramework {
    public abstract class PawnController {
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

            newPawn.HandlePossession(this, previousPawn);
            OnPossessed(newPawn);
            return true;
        }

        public void Unpossess() {
            if (Pawn == null)
                return;

            try {
                Pawn.HandleUnpossession();
                OnUnpossessed();
            } finally {
                Pawn = null;
            }
        }

        public abstract void Update();

        protected abstract void OnPossessed(Pawn pawn);
        protected abstract void OnUnpossessed();
    }
}