using Cube.Replication;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    public abstract class Pawn : ReplicaBehaviour {
        public PawnController controller;

        static List<Pawn> all = new List<Pawn>();

        public virtual bool CanBePossessedBy(PawnController controller) { return true; }
        public abstract void OnPossession(Pawn previousPawn);
        public abstract void OnUnpossession();

        public virtual void SetupPlayerInputComponent(PlayerInput input) { }

        public virtual void Teleport(Vector3 targetPosition, Quaternion targetRotation) { }


        public static void TickAll() {
            foreach (var pawn in all) {
                pawn.Tick();
            }
        }

        public void Tick() {
            if (controller != null) {
                controller.Tick();
            }

            TickImpl();
        }

        protected virtual void TickImpl() { }


        protected void Awake() {
            replica.onOwnership.AddListener(OnOwnership);
            replica.onOwnershipRemoved.AddListener(OnOwnershipRemoved);
            AwakeImpl();
        }

        protected virtual void AwakeImpl() { }


        protected virtual void OnEnable() {
            all.Add(this);
        }

        protected virtual void OnDisable() {
            all.Remove(this);
        }


        void OnOwnership() {
            var world = GetComponentInParent<World>(); // #todo
            var pc = world.playerControllers[0];
            pc.Possess(this);
        }

        void OnOwnershipRemoved() {
            var world = GetComponentInParent<World>(); // #todo
            var pc = world.playerControllers[0];

            var thisIsTheCurrentPawn = pc.pawn == this;
            if (thisIsTheCurrentPawn) {
                pc.Unpossess();
            }
        }
    }
}