﻿using UnityEngine;

namespace GameFramework {
    [RequireComponent(typeof(Character))]
    public class DefaultCharacterAnimation : MonoBehaviour {
        public Animator animator;

        public Character character {
            get;
            internal set;
        }

        protected virtual void Start() {
            character = GetComponent<Character>();
        }

        protected virtual void Update() {
            var isMoving = character.movement.IsMoving;
            animator.SetBool("Moving", isMoving);

            var velocity = character.movement.LocalVelocity;
            animator.SetFloat("ForwardSpeed", velocity.z);
        }
    }
}