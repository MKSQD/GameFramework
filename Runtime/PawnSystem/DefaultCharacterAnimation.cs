using UnityEngine;

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
            var isMoving = character.velocity.sqrMagnitude > 0.1f;
            animator.SetBool("Moving", isMoving);

            var velocity = transform.InverseTransformDirection(character.velocity);
            animator.SetFloat("ForwardSpeed", velocity.z);
        }
    }
}