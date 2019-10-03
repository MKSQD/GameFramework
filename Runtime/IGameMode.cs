namespace GameFramework {
    public interface IGameMode {
        void Tick();

        void HandleNewPlayer(PlayerController pc);
        void StartToLeaveMap();
        T GetGameState<T>() where T : GameState;
    }
}