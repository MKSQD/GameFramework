using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

namespace GameFramework {
    public delegate void AxisHandler(float value);
    public delegate void Axis2Handler(Vector2 value);
    public delegate void ActionHandler();

    public sealed class PlayerInput : IDisposable {
        readonly InputActionAsset _inputActionMap;
        readonly List<(InputAction Action, AxisHandler Handler)> _axisActions = new();
        readonly List<(InputAction Action, Axis2Handler Handler)> _axis2Actions = new();
        readonly List<(InputAction, Action<CallbackContext>)> _removeStarted = new();
        readonly List<(InputAction, Action<CallbackContext>)> _removeCanceled = new();

        public PlayerInput(InputActionAsset inputActionMap) {
            Assert.IsNotNull(inputActionMap);

            _inputActionMap = inputActionMap;
        }

        public void BindStartedAction(string actionName, ActionHandler handler) {
            Action<CallbackContext> wrapper = ctx => {
                if (!ClientGame.Main.PawnInputEnabled)
                    return;

                try {
                    handler();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            };

            var inputAction = _inputActionMap.FindAction(actionName, true);
            inputAction.started += wrapper;

            _removeStarted.Add((inputAction, wrapper));
        }

        public void BindCanceledAction(string actionName, ActionHandler handler) {
            Action<CallbackContext> wrapper = ctx => {
                if (!ClientGame.Main.PawnInputEnabled)
                    return;

                try {
                    handler();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            };

            var inputAction = _inputActionMap.FindAction(actionName, true);
            inputAction.canceled += wrapper;

            _removeCanceled.Add((inputAction, wrapper));
        }

        public void BindAxis(string axisName, AxisHandler handler) {
            var inputAction = _inputActionMap.FindAction(axisName, true);
            _axisActions.Add((inputAction, handler));
        }

        public void BindAxis2(string axisName, Axis2Handler handler) {
            var inputAction = _inputActionMap.FindAction(axisName, true);
            _axis2Actions.Add((inputAction, handler));
        }

        public void Update() {
            if (!ClientGame.Main.PawnInputEnabled)
                return;

            foreach (var pair in _axisActions) {
                var value = pair.Action.ReadValue<float>();
                pair.Handler(value);
            }
            foreach (var pair in _axis2Actions) {
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