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

    public class ClientGame {
        public CubeClient Client {
            get;
            internal set;
        }
        public World World {
            get;
            internal set;
        }

        public static bool CharacterInputEnabled = true;

        AsyncOperationHandle<SceneInstance> sceneHandle;
        byte currentLoadedSceneGeneration;

        ReplicaId currentReplicaPossess = ReplicaId.Invalid;
        byte pawnIdxToPossess;


        public ClientGame(ClientGameContext ctx) {
            Assert.IsNotNull(ctx.World);
            World = ctx.World;

            //var networkInterface = new LidgrenClientNetworkInterface(ctx.LagSettings);
            var networkInterface = new LiteNetClientNetworkInterface();
            Client = new CubeClient(ctx.World, networkInterface);

            Client.networkInterface.ConnectionRequestAccepted += OnConnectionRequestAccepted;
            Client.networkInterface.Disconnected += OnDisconnected;

            Client.reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
            Client.reactor.AddHandler((byte)MessageId.PossessPawn, OnPossessPawn);
        }

        public virtual void Update() {
            Client.Update();

            if (currentReplicaPossess != ReplicaId.Invalid) {
                var replica = Client.replicaManager.GetReplica(currentReplicaPossess);
                if (replica != null) {
                    var pawnsOnReplica = replica.GetComponentsInChildren<Pawn>();
                    if (pawnIdxToPossess >= pawnsOnReplica.Length)
                        return;

                    var pawn = pawnsOnReplica[pawnIdxToPossess];

                    var pc = World.playerControllers[0];
                    if (pawn.Controller != pc) {
                        pc.Possess(pawn);
                        Debug.Log("[Client] <b>Possessed Pawn</b> <i>" + pawn + "</i> idx=" + pawnIdxToPossess, pawn);
                    }
                }
            }
        }

        public virtual void Shutdown() {
            Client.Shutdown();
        }

        protected virtual PlayerController CreatePlayerController() {
            return new PlayerController(Connection.Invalid);
        }

        void OnConnectionRequestAccepted() {
            Debug.Log("[Client] Connection request to server accepted");

            var newPC = CreatePlayerController();
            World.playerControllers.Add(newPC);
        }

        void OnDisconnected(string reason) {
            Debug.Log("[Client] <b>Disconnected</b> (" + reason + ")");

            // Cleanup
            currentReplicaPossess = ReplicaId.Invalid;

            Client.replicaManager.Reset();

            if (sceneHandle.IsValid()) {
                Addressables.UnloadSceneAsync(sceneHandle);
            }
        }

        void OnLoadScene(BitStream bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            if (currentLoadedSceneGeneration == generation)
                return;

            currentLoadedSceneGeneration = generation;

            World.StartCoroutine(LoadScene(sceneName));
        }

        IEnumerator LoadScene(string sceneName) {
            Debug.Log($"[Client] <b>Loading level</b> '<i>{sceneName}</i>'");

            EventHub<StartedLoading>.Emit(new StartedLoading());

            // Cleanup
            currentReplicaPossess = ReplicaId.Invalid;

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

            Client.networkInterface.Send(bs, PacketPriority.High, PacketReliability.ReliableUnordered);
        }

        void OnPossessPawn(BitStream bs) {
            var replicaId = bs.ReadReplicaId();
            var pawnIdx = bs.ReadByte();

            currentReplicaPossess = replicaId;
            pawnIdxToPossess = pawnIdx;
        }
    }
}