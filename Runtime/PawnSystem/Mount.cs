using Cube.Replication;
using Cube.Transport;
using UnityEngine;


namespace GameFramework {
    /// <summary>
    /// Can be mounted by other Pawns. This works by possessing the Mount as a new Pawn and remembering the old Pawn
    /// as the Driver. On exit the old Pawn (Driver) is possessed again.
    /// </summary>
    public abstract class Mount : Pawn {
        [Tooltip("The camera asset that gets enabled when entering")]
        public GameObject Camera;
        [Tooltip("Player position/rotation while mounted")]
        public Transform DriverSeat;
        [ReadOnly]
        public Pawn Driver;
        /// <summary>
        /// Cached index of this Mount on the Replica. The first Mount has the index 0.
        /// </summary>
        [ReadOnly]
        public byte MountIdx;

        float _nextDriverExitTime;

        /// <summary>
        /// On the client, request to kick the driver (which has to be the calling player).
        /// On the server, kick the driver, making our current controller possess it again.
        /// </summary>
        public void KickDriver() {
            if (isClient) {
                if (IsOwned && Time.time >= _nextDriverExitTime) {
                    RpcServerKickDriver();
                }
            } else {
                if (!Controller.Possess(Driver)) {
                    Debug.LogWarning("Possession failed");
                    return;
                }
                Driver = null;
            }
        }

        public override bool CanBePossessedBy(PawnController pc) => Driver == null && pc.Pawn != null;

        public override void Serialize(IBitWriter bs, SerializeContext ctx) {
            if (Driver != null) {
                bs.WriteBool(true);
                bs.WriteReplicaId(Driver.Replica);
            } else {
                bs.WriteBool(false);
            }
        }

        public override void Deserialize(BitReader bs) {
            var hasDriver = bs.ReadBool();
            if (!hasDriver) {
                Driver = null;
                return;
            }

            var driverReplicaId = bs.ReadReplicaId();

            var driverReplica = client.ReplicaManager.GetReplica(driverReplicaId);
            if (driverReplica == null) {
                Driver = null;
                return;
            }

            var newDriver = driverReplica.GetComponent<Pawn>();
            if (newDriver == null) {
                Debug.LogError("New driver missing Pawn component");
                return;
            }

            if (newDriver != Driver) {
                // #todo undo old Driver?

                Driver = newDriver;
                if (Driver.Controller != null) {
                    Driver.Controller.Possess(this);
                }
            }
        }

        protected override void OnPossession(Pawn previousPawn) {
            var newDriver = previousPawn.GetComponent<Pawn>();
            if (newDriver == null) {
                Debug.LogError("New driver missing Pawn component");
                return;
            }

            Driver = newDriver;

            if (isClient && Camera != null) {
                Camera.SetActive(true);
            }

            _nextDriverExitTime = Time.time + 1;
        }

        protected override void OnUnpossession() {
            if (isClient && Camera != null) {
                Camera.SetActive(false);
            }
        }

        protected virtual void Awake() {
            // Make sure the camera is disabled from the start
            if (Camera != null) {
                Camera.SetActive(false);
            }

            var isPrimaryMount = MountIdx == 0;
            if (isPrimaryMount) {
                MountIdx = 0;

                var idx = 1;
                foreach (var other in GetComponentsInChildren<Mount>()) {
                    if (other == this)
                        continue;

                    other.MountIdx = (byte)idx;
                    ++idx;
                }
            }
        }

        [ReplicaRpc(RpcTarget.Server)]
        void RpcServerKickDriver() {
            KickDriver();
        }

        protected void LateUpdate() {
            if (Driver != null && DriverSeat != null) {
                Driver.transform.position = DriverSeat.position;
                Driver.transform.rotation = DriverSeat.rotation;
            }
        }
    }
}