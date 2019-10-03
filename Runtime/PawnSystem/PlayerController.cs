using Cube.Replication;
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

        ReplicaView _replicaView;

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

            if (_replicaView != null && pawn != null) {
                _replicaView.transform.position = pawn.transform.position;
            }
        }

        protected override void OnPossess(Pawn pawn) {
            if (pawn.isServer) {
                pawn.replica.AssignOwnership(connection);
                _replicaView = pawn.server.replicaManager.GetReplicaView(connection);
            }
            if (pawn.isClient) {
                input = CreatePlayerInput();
                SetupInputComponent(input);
                pawn.SetupPlayerInputComponent(input);
            }
        }

        protected override void OnUnpossess() {
            if (pawn.isServer) {
                pawn.replica.TakeOwnership();
            }
        }

        protected virtual void SetupInputComponent(PlayerInput input) {
        }
    }
}