using GameCore;
using UnityEngine;

namespace GameFramework.FeelsSystem {
    [CreateAssetMenu(menuName = "GameFramework/FeelsType")]
    public class FeelsType : ScriptableObject {
        [SerializeReference]
        [SelectImplementation(typeof(IFeel))]
        public IFeel[] Feels;

        public void Trigger() {
            foreach (var feel in Feels) {
                feel.Do();
            }
        }

         public void Trigger(Vector3 position, Quaternion rotation) {
            foreach (var feel in Feels) {
                feel.Do(position, rotation);
            }
        }

        [ContextMenu("Trigger")]
        protected void ContextTrigger() => Trigger();
    }
}