using GameFramework;

public class DemoGameMode : GameMode {
    public DemoGameMode(ServerGame server) : base(server) {
    }

    protected override object GetPlayerPrefabAddress(ServerPlayerController pc) => "Server_DemoPlayer";
}
