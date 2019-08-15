using Cube.Transport;

namespace GameFramework {
    public class PlayerController : PawnController {
        public PlayerInput input {
            get;
            internal set;
        }
        public Connection connection {
            get;
            internal set;
        }

        public PlayerController(Connection connection) {
            this.connection = connection;
        }

        public virtual PlayerInput CreatePlayerInput() {
            return new PlayerInput();
        }

        public override void Update() {
            if (input != null) {
                input.Update();
            }
        }

        protected override void OnPossess(Pawn pawn) {
            pawn.replica.AssignOwnership(connection);

            if (pawn.isClient) {
                input = CreatePlayerInput();
                SetupInputComponent(input);
                pawn.SetupPlayerInputComponent(input);
            }
        }

        protected override void OnUnpossess() {
            pawn.replica.TakeOwnership();
        }

        protected virtual void SetupInputComponent(PlayerInput input) {
        }
    }
}