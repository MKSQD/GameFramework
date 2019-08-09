using Cube.Transport;

namespace GameFramework {
    public class PlayerController : PawnController {
        public PlayerInput input {
            get;
            internal set;
        }
        public Character character {
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

        public void ClientTravel() {

        }

        public virtual PlayerInput CreatePlayerInput() {
            return new PlayerInput();
        }

        public virtual void SetupInputComponent() {
        }

        public override void Tick() {
            input.Tick();
        }

        protected override void OnPossess(Pawn pawn) {
            character = (Character)pawn;

            pawn.replica.AssignOwnership(connection);

            input = CreatePlayerInput();
            SetupInputComponent();
        }

        protected override void OnUnpossess() {
            pawn.replica.TakeOwnership();
        }
    }
}