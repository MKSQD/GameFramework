using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

namespace GameFramework {
    public sealed class PlayerInput : PawnInput {
        readonly InputActionAsset inputActionMap;
        readonly List<(InputAction Action, AxisHandler Handler)> axisActions = new List<(InputAction, AxisHandler)>();

        public PlayerInput(InputActionAsset inputActionMap) {
            Assert.IsNotNull(inputActionMap);
            this.inputActionMap = inputActionMap;
        }

        public void BindStartedAction(string actionName, ActionHandler handler) {
            var inputAction = inputActionMap.FindAction(actionName, true);
            inputAction.started += ctx => {
                try {
                    handler();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            };
        }

        public void BindCanceledAction(string actionName, ActionHandler handler) {
            var inputAction = inputActionMap.FindAction(actionName, true);
            inputAction.canceled += ctx => {
                try {
                    handler();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            };
        }

        public void BindAxis(string axisName, AxisHandler handler) {
            var inputAction = inputActionMap.FindAction(axisName, true);
            axisActions.Add((inputAction, handler));
        }

        public void Update() {
            foreach (var pair in axisActions) {
                var value = pair.Action.ReadValue<Vector2>();
                pair.Handler(value);
            }
        }
    }
}