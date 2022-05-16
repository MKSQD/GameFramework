using GameFramework;

public class DemoServerGame : ServerGame {
    public string InitialScene;

    protected void Start() {
        LoadMap(InitialScene);
    }

    public override IGameMode CreateGameModeForScene(string sceneName) => new DemoGameMode(this);
}
