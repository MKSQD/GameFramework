using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    public class World : MonoBehaviour, IWorld {
        public GameObject GameState;
        public List<PlayerController> PlayerControllers = new List<PlayerController>();

        public PlayerController GetPlayerController(Connection connection) {
            foreach (var pc in PlayerControllers) {
                if (pc.Connection == connection)
                    return pc;
            }
            return null;
        }
    }
}