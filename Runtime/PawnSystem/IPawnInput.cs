using UnityEngine;

namespace GameFramework {
    public delegate void AxisHandler(float value);
    public delegate void Axis2Handler(Vector2 value);
    public delegate void ActionHandler();

    public interface IPawnInput {
        void BindStartedAction(string actionName, ActionHandler handler);
        void BindCanceledAction(string actionName, ActionHandler handler);
        void BindAxis(string axisName, AxisHandler handler);
        void BindAxis2(string axisName, Axis2Handler handler);

        void Update();
    }
}