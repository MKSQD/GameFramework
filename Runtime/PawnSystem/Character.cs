using Cinemachine;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;

namespace GameFramework {
    [AddComponentMenu("GameFramework/Character")]
    public class Character : Pawn {
        [Tooltip("The camera asset that gets spawned when entering")]
        public AssetReference Camera;

        public Transform view;

        public new ICharacterMovement Movement {
            get;
            internal set;
        }

        CinemachineVirtualCamera currentCamera;

        public override void SetupPlayerInputComponent(PawnInput input) {
            input.BindAxis("Gameplay/Look", OnLook);
            input.BindAxis("Gameplay/Move", OnMove);
            //input.BindAxis("Run", OnRun);
            input.BindStartedAction("Gameplay/Jump", OnJump);
        }

        protected override void Awake() {
            base.Awake();

            Movement = GetComponent<ICharacterMovement>();
            Assert.IsNotNull(Movement);
        }

        protected override void HandlePossessionImpl(Pawn previousPawn) {
            if (isClient) {
                Camera.InstantiateAsync(view).Completed += result => {
                    currentCamera = result.Result.GetComponent<CinemachineVirtualCamera>();
                };
            }
        }

        protected override void HandleUnpossessionImpl() {
            if (currentCamera != null) {
                Destroy(currentCamera);
            }
        }

        void OnLook(Vector2 value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetLook(new Vector2(value.x * 0.5f, value.y * 0.5f));
        }

        void OnMove(Vector2 value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetMove(value);
        }

        void OnRun(float value) {
#if UNITY_EDITOR || CLIENT
            if (isClient && Controller is PlayerController && !ClientGame.CharacterInputEnabled)
                return;
#endif

            Movement.SetRun(value > 0.5f);
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