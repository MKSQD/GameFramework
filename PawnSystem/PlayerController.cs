public class PlayerController : PawnController {
    public PlayerInput input {
        get;
        internal set;
    }
    public Character character {
        get;
        internal set;
    }

    public virtual PlayerInput CreateInputSystem() {
        return new PlayerInput();
    }

    public virtual void SetupInputComponent() {
    }

    public override void OnPossess(Pawn pawn) {
        character = (Character)pawn;

        input = CreateInputSystem();
        SetupInputComponent();
    }

    public override void OnUnpossess() {
    }

    public override void Tick() {
        input.Tick();
    }
}
