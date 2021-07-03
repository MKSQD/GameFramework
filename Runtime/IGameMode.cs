using UnityEngine;

namespace GameFramework {
    public interface IGameMode {
        GameObject GameState { get; }

        void Update();

        void HandleNewPlayer(PlayerController pc);
        void StartToLeaveMap();
    }
}