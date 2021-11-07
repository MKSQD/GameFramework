﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

namespace GameFramework {
    public sealed class PlayerInput : IPawnInput {
        readonly InputActionAsset inputActionMap;
        readonly List<(InputAction Action, AxisHandler Handler)> axisActions = new List<(InputAction, AxisHandler)>();
        readonly List<(InputAction Action, Axis2Handler Handler)> axis2Actions = new List<(InputAction, Axis2Handler)>();

        public PlayerInput(InputActionAsset inputActionMap) {
            Assert.IsNotNull(inputActionMap);
            this.inputActionMap = inputActionMap;
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

            var inputAction = inputActionMap.FindAction(actionName, true);
            inputAction.started += wrapper;
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

            var inputAction = inputActionMap.FindAction(actionName, true);
            inputAction.canceled += wrapper;
        }

        public void BindAxis(string axisName, AxisHandler handler) {
            var inputAction = inputActionMap.FindAction(axisName, true);
            axisActions.Add((inputAction, handler));
        }

        public void BindAxis2(string axisName, Axis2Handler handler) {
            var inputAction = inputActionMap.FindAction(axisName, true);
            axis2Actions.Add((inputAction, handler));
        }

        public void Update() {
            if (!ClientGame.Main.PawnInputEnabled)
                return;

            foreach (var pair in axisActions) {
                var value = pair.Action.ReadValue<float>();
                pair.Handler(value);
            }
            foreach (var pair in axis2Actions) {
                var value = pair.Action.ReadValue<Vector2>();
                pair.Handler(value);
            }
        }
    }
}