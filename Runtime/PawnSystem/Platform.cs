using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Platform")]
    public class Platform : MonoBehaviour {
        /// <summary>
        /// For something standing on the platform, which Transform should be used for any Platform calculations.
        /// </summary>
        public Transform referenceTransform;
    }
}