using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    public class Character : Pawn {
        [Tooltip("The camera asset that gets enabled when entering")]
        public GameObject Camera;

        public Transform view;

        public ICharacterMovement Movement {
            get;
            internal set;
        }

        public CharacterHealth Health {
            get;
            internal set;
        }

        public CharacterStats Stats {
            get;
            internal set;
        }

        protected bool IsInputEnabled => !isClient || !(Controller is PlayerController) || ClientGame.CharacterInputEnabled;

        public override void SetupPlayerInputComponent(PawnInput input) {
            input.BindAxis2("Gameplay/Look", OnLook);
            input.BindAxis2("Gameplay/Move", OnMove);
            input.BindAxis("Gameplay/Walk", OnWalk);
            input.BindStartedAction("Gameplay/ToggleCrouch", OnToggleCrouch);
            input.BindStartedAction("Gameplay/Jump", OnJump);
        }

        protected override void Awake() {
            base.Awake();

            if (Camera != null) {
                Camera.SetActive(false);
            }

            Health = GetComponent<CharacterHealth>();
            Assert.IsNotNull(Health);

            Stats = GetComponent<CharacterStats>();
            Assert.IsNotNull(Stats);

            Movement = GetComponent<ICharacterMovement>();
            Assert.IsNotNull(Movement);

            Movement.DeathByLanding += () => Health.Kill(new DamageInfo(255, DamageType.Physical, null));
        }

        protected override void HandlePossessionImpl(Pawn previousPawn) {
            if (isClient) {
                Camera.SetActive(true);
            }
        }

        protected override void HandleUnpossessionImpl() {
            if (Camera != null) {
                Camera.SetActive(false);
            }
        }

        void OnLook(Vector2 value) {
            if (!IsInputEnabled)
                return;

            Movement.AddLook(value);
        }

        void OnMove(Vector2 value) {
            if (!IsInputEnabled)
                return;

            Movement.SetMove(value);
        }

        void OnWalk(float value) {
            if (!IsInputEnabled)
                return;

            Movement.SetIsWalking(value > 0.5f);
        }

        void OnToggleCrouch() {
            if (!IsInputEnabled)
                return;

            Movement.SetIsCrouching(!Movement.IsCrouching);
        }

        void OnJump() {
            if (!IsInputEnabled)
                return;

            Movement.Jump();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() {
            if (view != null) {
                Debug.DrawLine(view.transform.position, view.transform.position + view.transform.forward * 0.5f, Color.yellow);
            }
        }
#endif
    }
}