using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    public class World : MonoBehaviour, IWorld {
        public List<PlayerController> playerControllers = new List<PlayerController>();

        public PlayerController GetPlayerController(Connection connection) {
            foreach (var pc in playerControllers) {
                if (pc.Connection == connection)
                    return pc;
            }
            return null;
        }
    }
}