using UnityEngine;

namespace GameFramework {
    public interface IGameMode {
        void Update();

        void HandleNewPlayer(PlayerController pc);
        void StartToLeaveMap();
    }
}