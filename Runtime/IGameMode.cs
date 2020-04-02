namespace GameFramework {
    public interface IGameMode {
        void Update();

        void HandleNewPlayer(PlayerController pc);
        void StartToLeaveMap();
        T GetGameState<T>() where T : GameState;
    }
}