﻿using Cinemachine;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : Pawn {
        public Transform view;
        public new CinemachineVirtualCamera camera;
        
        public new CharacterMovement movement {
            get;
            internal set;
        }

        public Vector3 velocity {
            get { return characterController.velocity; }
        }

        public Vector3 localVelocity {
            get { return transform.InverseTransformDirection(characterController.velocity); }
        }

        public bool isMoving {
            get { return characterController.velocity.sqrMagnitude > 0.1f; }
        }

        public bool isGrounded {
            get { return characterController.isGrounded; }
        }

        public CharacterController characterController {
            get;
            internal set;
        }

        public override void Teleport(Vector3 targetPosition, Quaternion targetRotation) {
            if (characterController != null) {
                characterController.enabled = false;
            }
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            if (characterController != null) {
                characterController.enabled = true;
            }
        }

        public override void SetupPlayerInputComponent(PawnInput input) {
            input.BindAxis("Mouse X", OnMouseX);
            input.BindAxis("Mouse Y", OnMouseY);
            input.BindAxis("Horizontal", OnHorizontalInput);
            input.BindAxis("Vertical", OnVerticalInput);
            input.BindAxis("Run", OnRun);
            input.BindAction("Jump", OnJump);
        }

        protected override void Awake() {
            base.Awake();

            characterController = GetComponent<CharacterController>();
            movement = GetComponent<CharacterMovement>();

            if (camera != null) {
                camera.enabled = false;
            }
        }

        protected override void OnPossessionImpl(Pawn previousPawn) {
            if (camera != null) {
                camera.enabled = true;
            }
        }

        protected override void OnUnpossessionImpl() {
            if (camera != null) {
                camera.enabled = false;
            }
        }

        void OnMouseX(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && controller is PlayerController && !GameInstance.main.CharacterInputEnabled)
                return;
#endif

            movement.AddYawInput(value * 0.5f);
        }

        void OnMouseY(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && controller is PlayerController && !GameInstance.main.CharacterInputEnabled)
                return;
#endif

            movement.AddPitchInput(value * 0.5f);
        }

        void OnHorizontalInput(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && controller is PlayerController && !GameInstance.main.CharacterInputEnabled)
                return;
#endif

            movement.AddMoveInput(Vector3.right * value);
        }

        void OnVerticalInput(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && controller is PlayerController && !GameInstance.main.CharacterInputEnabled)
                return;
#endif

            movement.AddMoveInput(Vector3.forward * value);
        }

        void OnRun(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && controller is PlayerController && !GameInstance.main.CharacterInputEnabled)
                return;
#endif

            movement.SetRun(value > 0.5f);
        }

        void OnJump() {
#if UNITY_EDITOR || CLIENT
            if (isClient && controller is PlayerController && !GameInstance.main.CharacterInputEnabled)
                return;
#endif

            movement.Jump();
        }

        void OnDrawGizmos() {
            if (view != null) {
                Debug.DrawLine(view.transform.position, view.transform.position + view.transform.forward * 0.25f, Color.yellow);
            }
        }
    }
}