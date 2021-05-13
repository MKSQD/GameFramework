using UnityEngine;

namespace GameFramework {
    public delegate void AxisHandler(Vector2 value);
    public delegate void ActionHandler();

    public interface PawnInput {
        void BindStartedAction(string actionName, ActionHandler handler);
        void BindCanceledAction(string actionName, ActionHandler handler);
        void BindAxis(string axisName, AxisHandler handler);

        void Update();
    }
}