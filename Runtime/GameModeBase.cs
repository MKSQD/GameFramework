using Cube.Replication;
using UnityEngine;

namespace GameFramework {
    public abstract class GameModeBase : IGameMode {
        public ServerGame Server;

        public GameModeBase(ServerGame server) {
            Server = server;
        }

        public abstract void Update();

        public abstract void StartToLeaveMap();

        public abstract void HandleNewPlayer(ServerPlayerController pc);
    }
}