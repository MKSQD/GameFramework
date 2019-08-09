using Cube.Replication;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework {
    public class Pawn : ReplicaBehaviour {
        public PawnController controller;

        static List<Pawn> all = new List<Pawn>();

       public void Tick() {
            if (controller != null) {
                controller.Tick();
            }

            TickImpl();
        }

        public virtual void OnPossession() { }
        public virtual void OnUnpossession() { }

        public virtual void Teleport(Vector3 targetPosition, Quaternion targetRotation) { }

        public static void TickAll() {
            foreach (var pawn in all) {
                pawn.Tick();
            }
        }

        protected virtual void Awake() {
            replica.onOwnership.AddListener(OnOwnership);
        }

        protected virtual void TickImpl() { }

        protected virtual void OnEnable() {
            all.Add(this);
        }

        protected virtual void OnDisable() {
            all.Remove(this);
        }

        void OnOwnership() {
            var world = GetComponentInParent<World>();
            var pc = world.playerControllers[0];
            pc.Possess(this);
        }
    }
}