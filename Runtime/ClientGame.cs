using System.Collections;
using Cube.Networking;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    public class StartedLoading : IEvent { }
    public class EndedLoading : IEvent { }

    public struct ClientGameContext {
        public World World;
        public SimulatedLagSettings LagSettings;
    }

    public class ClientGame : MonoBehaviour, IWorld {
        public static ClientGame Main;

        public CubeClient Client {
            get;
            internal set;
        }
        [ReadOnly]
        public GameObject GameState;

        AsyncOperationHandle<SceneInstance> sceneHandle;
        byte currentLoadedSceneGeneration;

        PlayerController localPlayerController;
        ReplicaId currentReplicaToPossess = ReplicaId.Invalid;
        byte pawnIdxToPossess;

        public virtual bool PawnInputEnabled => true;


        protected virtual void Awake() {
            //var networkInterface = new LidgrenClientNetworkInterface(ctx.LagSettings);
            var networkInterface = new LiteNetClientNetworkInterface();

            Client = new CubeClient(this, networkInterface);

            Client.NetworkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;
            Client.NetworkInterface.Disconnected += OnDisconnected;

            Client.Reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            Client.Reactor.AddHandler((byte)MessageId.PossessPawn, OnPossessPawn);

            Main = this;
        }

        protected virtual void Update() {
            Client.Update();
            PossessReplica();
        }

        void PossessReplica() {
            if (currentReplicaToPossess == ReplicaId.Invalid)
                return;

            var replica = Client.replicaManager.GetReplica(currentReplicaToPossess);
            if (replica == null)
                return;

            var pawnsOnReplica = replica.GetComponentsInChildren<Pawn>();
            if (pawnIdxToPossess >= pawnsOnReplica.Length) {
                Debug.LogWarning("Invalid Pawn prossession idx");
                return;
            }

            var pawn = pawnsOnReplica[pawnIdxToPossess];
            if (localPlayerController.Possess(pawn)) {
                Debug.Log($"[Client] <b>Possessed Pawn</b> <i>{pawn}</i> idx={pawnIdxToPossess}", pawn);

                currentReplicaToPossess = ReplicaId.Invalid;
                // Note: If we ever loose the Pawn we will NOT repossess it! This should be OK since we never timeout owned Replicas
            }
        }

        protected virtual void OnApplicationQuit() {
            Client.Shutdown();
        }

        protected virtual PlayerController CreatePlayerController() {
            return new PlayerController(Connection.Invalid);
        }

        void OnConnectionRequestAccepted() {
            Debug.Log("[Client] Connection request to server accepted");

            localPlayerController = CreatePlayerController();
        }

        void OnDisconnected(string reason) {
            Debug.Log($"[Client] <b>Disconnected</b> ({reason})");

            // Cleanup
            localPlayerController = null;
            currentReplicaToPossess = ReplicaId.Invalid;

            Client.replicaManager.Reset();

            if (sceneHandle.IsValid()) {
                Addressables.UnloadSceneAsync(sceneHandle);
            }
        }

        void OnLoadScene(BitStream bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            if (currentLoadedSceneGeneration != generation) {
                currentLoadedSceneGeneration = generation;

                StartCoroutine(LoadScene(sceneName));
            }
        }

        IEnumerator LoadScene(string sceneName) {
            Debug.Log($"[Client] <b>Loading level</b> '<i>{sceneName}</i>'");

            EventHub<StartedLoading>.Emit(new StartedLoading());

            // Cleanup
            currentReplicaToPossess = ReplicaId.Invalid;

            Client.replicaManager.Reset();

            if (sceneHandle.IsValid())
                yield return Addressables.UnloadSceneAsync(sceneHandle);

            // New map
#if !UNITY_EDITOR
            sceneHandle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            sceneHandle.Completed += ctx => {
                SendLoadSceneDone();
                EventHub<EndedLoading>.Emit(new EndedLoading());
            };
#else
            SendLoadSceneDone();
            EventHub<EndedLoading>.Emit(new EndedLoading());
#endif
        }

        void SendLoadSceneDone() {
            var bs = new BitStream();
            bs.Write((byte)MessageId.LoadSceneDone);
            bs.Write(currentLoadedSceneGeneration);

            Client.NetworkInterface.Send(bs, PacketReliability.ReliableUnordered);
        }

        void OnPossessPawn(BitStream bs) {
            var replicaId = bs.ReadReplicaId();
            var pawnIdx = bs.ReadByte();

            currentReplicaToPossess = replicaId;
            pawnIdxToPossess = pawnIdx;
        }
    }
}