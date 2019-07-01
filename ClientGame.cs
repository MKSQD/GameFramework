using Cube.Networking;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace GameFramework {
    public class ClientGame {
        public ClientSimulatedLagSettings lagSettings;

        public bool connectInEditor = true;
        public ushort portInEditor = 60000;

        public CubeClient client;

        public UnityEvent onConnectionRequestAccepted = new UnityEvent();
        public UnityEvent onConnectionRequestFailed = new UnityEvent();
        public UnityEvent onSceneLoadStart = new UnityEvent();

        public ClientGame(Transform transform) {
            client = new CubeClient(transform, lagSettings);

#if UNITY_EDITOR
            if (connectInEditor) {
                client.networkInterface.Connect("127.0.0.1", portInEditor);
            }
#endif

            client.reactor.AddHandler((byte)MessageId.ConnectionRequestAccepted, OnConnectionRequestAccepted);
            client.reactor.AddHandler((byte)MessageId.ConnectionRequestFailed, OnConnectionRequestFailed);
            client.reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
        }

        public virtual void Update() {
            client.Update();
        }

        public virtual void Shutdown() {
            client.Shutdown();
        }

        void OnConnectionRequestAccepted(BitStream bs) {
            Debug.Log("[Client] Connection request to server accepted");

            onConnectionRequestAccepted.Invoke();
        }

        void OnConnectionRequestFailed(BitStream bs) {
            Debug.Log("Connection request to server failed");

            onConnectionRequestFailed.Invoke();
        }

        void OnLoadScene(BitStream bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            Debug.Log("[Client] Loading level: " + sceneName + " generation=" + generation);

            client.replicaManager.Reset();

            onSceneLoadStart.Invoke();

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
                return; // See log for errors

            op.completed += _ => {
                var bs2 = new BitStream();
                bs2.Write((byte)MessageId.LoadSceneDone);
                bs2.Write(generation);

                client.networkInterface.Send(bs2, PacketPriority.High, PacketReliability.Reliable);
            };
        }
    }
}