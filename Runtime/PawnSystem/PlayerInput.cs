using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    public sealed class PlayerInput : PawnInput {
        public override void Update() {
            TriggerActions();
            TriggerAxis();
        }

        void TriggerActions() {
            foreach (var actionHandler in _actionHandlers) {
                if (Input.GetAxis(actionHandler.Key) == 0)
                    continue;

                actionHandler.Value();
            }
        }

        void TriggerAxis() {
            foreach (var axisMapping in _axisHandlers) {
                var axisValue = Input.GetAxis(axisMapping.Key);
                axisMapping.Value(axisValue);
            }
        }
    }
}