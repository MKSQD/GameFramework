namespace GameFramework {
    public interface IGameMode {
        void Update();

        void HandleNewPlayer(ServerPlayerController pc);
        void StartToLeaveMap();
    }
}