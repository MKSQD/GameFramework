using System.Collections.Generic;

namespace GameFramework {
    public delegate void AxisHandler(float value);
    public delegate void ActionHandler();

    public abstract class PawnInput {
        protected Dictionary<string, AxisHandler> _axisHandlers = new Dictionary<string, AxisHandler>();
        protected Dictionary<string, ActionHandler> _actionHandlers = new Dictionary<string, ActionHandler>();

        public void BindAction(string actionName, ActionHandler handler) {
            if (_actionHandlers.ContainsKey(actionName)) {
                _actionHandlers[actionName] += handler;
            }
            else {
                _actionHandlers[actionName] = handler;
            }
        }

        public void BindAxis(string axisName, AxisHandler handler) {
            if (_axisHandlers.ContainsKey(axisName)) {
                _axisHandlers[axisName] += handler;
            }
            else {
                _axisHandlers[axisName] = handler;
            }
        }

        public abstract void Update();
    }
}