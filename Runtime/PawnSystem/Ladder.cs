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
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.color = new Color(1, 1, 0, 0.5f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
        }
    }
}