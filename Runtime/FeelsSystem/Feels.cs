using UnityEngine;

namespace GameFramework.FeelsSystem {
    [AddComponentMenu("GameFramework/Feels")]
    public class Feels : MonoBehaviour {
        public FeelsType Type;

        public void Trigger() => Type.Trigger();
    }
}