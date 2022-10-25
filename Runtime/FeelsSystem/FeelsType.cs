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
                feel.Exec();
            }
        }

        public void TriggerAtPosition(Vector3 position) {
            foreach (var feel in Feels) {
                feel.ExecAtPosition(position);
            }
        }

        [ContextMenu("Trigger")]
        protected void ContextTrigger() => Trigger();
    }
}