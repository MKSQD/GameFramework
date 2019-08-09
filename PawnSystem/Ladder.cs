using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Ladder")]
    public class Ladder : MonoBehaviour {
        void OnTriggerEnter(Collider other) {
            var movement = other.GetComponent<IPawnMovement>();
            if (movement == null)
                return;

            movement.OnEnterLadder();
        }

        void OnTriggerExit(Collider other) {
            var movement = other.GetComponent<IPawnMovement>();
            if (movement == null)
                return;

            movement.OnExitLadder();
        }

        void OnDrawGizmos() {
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawCube(transform.position, transform.lossyScale);
        }
    }
}