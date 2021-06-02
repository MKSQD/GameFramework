using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    public class Character : Pawn {
        [Tooltip("The camera asset that gets enabled when entering")]
        public GameObject Camera;

        public Transform view;

        public new ICharacterMovement Movement {
            get;
            internal set;
        }

        public CharacterHealth Health {
            get;
            internal set;
        }

        public override void SetupPlayerInputComponent(PawnInput input) {
            input.BindAxis2("Gameplay/Look", OnLook);
            input.BindAxis2("Gameplay/Move", OnMove);
            input.BindAxis("Gameplay/Sneak", OnSneak);
            input.BindStartedAction("Gameplay/Jump", OnJump);
        }

        protected override void Awake() {
            base.Awake();

            if (Camera != null) {
                Camera.SetActive(false);
            }

            Health = GetComponent<CharacterHealth>();

            Movement = GetComponent<ICharacterMovement>();
            Assert.IsNotNull(Movement);

            Movement.DeathByLanding += () => Health.Kill(new DamageInfo(255));
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
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetLook(new Vector2(value.x * 0.3f, value.y * 0.3f));
        }

        void OnMove(Vector2 value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetMove(value);
        }

        void OnSneak(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetSneaking(value > 0.5f);
        }

        void OnJump() {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.Jump();
        }

        void OnDrawGizmosSelected() {
            if (view != null) {
                Debug.DrawLine(view.transform.position, view.transform.position + view.transform.forward * 0.5f, Color.yellow);
            }
        }
    }
}