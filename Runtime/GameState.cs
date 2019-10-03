using Cube.Replication;
using System.Collections.Generic;

namespace GameFramework {
    public class GameState : ReplicaBehaviour {
        public List<PlayerState> players;
    }
}