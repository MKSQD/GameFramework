using UnityEngine;

namespace GameFramework.FeelsSystem {
    [AddComponentMenu("GameFramework/TriggerFeelsOnAwake")]
    public class TriggerFeelsOnAwake : MonoBehaviour {
        public FeelsType Type;

        protected void Awake() {
            Type.Trigger();
        }
    }
}