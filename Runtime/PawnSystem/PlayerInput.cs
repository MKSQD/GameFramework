using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

namespace GameFramework {
    public sealed class PlayerInput : IDisposable {
        public delegate void AxisHandler(float value);
        public delegate void Axis2Handler(Vector2 value);
        public delegate void ActionHandler();

        readonly InputActionAsset _inputActionMap;
        readonly List<(InputAction Action, AxisHandler Handler)> _floatAxisActions = new();
        readonly List<(InputAction Action, Axis2Handler Handler)> _vector2AxisActions = new();
        readonly List<(InputAction, Action<CallbackContext>)> _removeStarted = new();
        readonly List<(InputAction, Action<CallbackContext>)> _removeCanceled = new();

        public PlayerInput(InputActionAsset inputActionMap) {
            Assert.IsNotNull(inputActionMap);

            _inputActionMap = inputActionMap;
        }

        public void BindStartedAction(string actionName, ActionHandler handler) {
            void Wrapper(CallbackContext _) {
                try {
                    handler();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }

            var inputAction = _inputActionMap.FindAction(actionName, true);
            inputAction.started += Wrapper;

            _removeStarted.Add((inputAction, Wrapper));
        }

        public void BindCanceledAction(string actionName, ActionHandler handler) {
            void Wrapper(CallbackContext _) {
                try {
                    handler();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }

            var inputAction = _inputActionMap.FindAction(actionName, true);
            inputAction.canceled += Wrapper;

            _removeCanceled.Add((inputAction, Wrapper));
        }

        public void BindFloatAxis(string axisName, AxisHandler handler) {
            var inputAction = _inputActionMap.FindAction(axisName, true);
            _floatAxisActions.Add((inputAction, handler));
        }

        public void BindVector2Axis(string axisName, Axis2Handler handler) {
            var inputAction = _inputActionMap.FindAction(axisName, true);
            _vector2AxisActions.Add((inputAction, handler));
        }

        public void Update() {
            if (!ClientGame.Main.PawnInputEnabled)
                return;

            foreach (var pair in _floatAxisActions) {
                var value = pair.Action.ReadValue<float>();
                pair.Handler(value);
            }
            foreach (var pair in _vector2AxisActions) {
                var value = pair.Action.ReadValue<Vector2>();
                pair.Handler(value);
            }
        }

        public void Dispose() {
            foreach (var pair in _removeStarted) {
                pair.Item1.started -= pair.Item2;
            }

            foreach (var pair in _removeCanceled) {
                pair.Item1.canceled -= pair.Item2;
            }
        }
    }
}