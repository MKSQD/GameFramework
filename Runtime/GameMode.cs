using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Cube.Transport;
using UnityEngine;

namespace GameFramework {
    public class GameMode : GameModeBase {
        public enum MatchState {
            WaitingToStart,
            InProgress,
            WaitingPostMatch,
            LeavingMap
        }

        public bool HasMatchStarted => CurrentMatchState == MatchState.InProgress;
        public bool HasMatchEnded => CurrentMatchState == MatchState.WaitingPostMatch;

        public MatchState CurrentMatchState { get; private set; }
        public ReadOnlyCollection<Pawn> Players => _players.AsReadOnly();
        protected readonly List<Pawn> _players = new();

        readonly Queue<(float, Connection)> _respawnQueue = new();

        public GameMode(ServerGame server) : base(server) {
            CurrentMatchState = MatchState.WaitingToStart;
            OnMatchIsWaitingToStart();
        }

        public void StartMatch() {
            if (CurrentMatchState != MatchState.WaitingToStart) {
                Debug.LogWarning("StartMatch called in wrong MatchState " + CurrentMatchState);
                return;
            }

            Debug.Log("[Server] <b>Match starting...</b>");

            CurrentMatchState = MatchState.InProgress;
            OnMatchHasStarted();
        }

        public void EndMatch() {
            if (CurrentMatchState != MatchState.InProgress) {
                Debug.LogWarning("EndMatch called in wrong MatchState " + CurrentMatchState);
                return;
            }

            CurrentMatchState = MatchState.WaitingPostMatch;
            OnMatchHasEnded();

            Debug.Log("[Server] <b>Match has ended</b>");
        }

        public override void StartToLeaveMap() {
            if (CurrentMatchState == MatchState.InProgress) {
                EndMatch();
            }

            if (CurrentMatchState != MatchState.WaitingPostMatch) {
                Debug.LogWarning("StartToLeaveMap called in wrong MatchState " + CurrentMatchState);
                return;
            }

            CurrentMatchState = MatchState.LeavingMap;
            OnLeavingMap();
        }

        public override void Update() {
            switch (CurrentMatchState) {
                case MatchState.WaitingToStart:
                    if (ReadyToStartMatch()) {
                        StartMatch();
                    }
                    break;

                case MatchState.InProgress:
                    foreach (var pc in Server.PlayerControllers) {
                        if (pc.Pawn == null && !_respawnQueue.Any(pair => pair.Item2 == pc.Connection)) {
                            _respawnQueue.Enqueue((Time.time + 5, pc.Connection));
                        }
                    }

                    if (_respawnQueue.Count > 0) {
                        var timeControllerPair = _respawnQueue.Peek();
                        var respawnPlayer = Time.time >= timeControllerPair.Item1;
                        if (respawnPlayer) {
                            _respawnQueue.Dequeue();

                            var pc = Server.GetPlayerControllerForConnection(timeControllerPair.Item2);
                            if (pc != null && pc.Pawn == null) { // Queued player for respawn but he's already alive
                                SpawnPlayer(pc);
                            }
                        }
                    }

                    if (ReadyToEndMatch()) {
                        EndMatch();
                    }
                    break;
            }
        }

        public override void HandleNewPlayer(ServerPlayerController pc) {
            if (HasMatchStarted) {
                SpawnPlayer(pc);
            }
        }

        protected virtual bool ReadyToStartMatch() {
            return Server.Connections.Count > 0 && !Server.IsLoadingScene;
        }

        protected virtual bool ReadyToEndMatch() {
            return Server.Connections.Count == 0 && !Server.IsLoadingScene;
        }

        protected virtual void OnMatchIsWaitingToStart() { }

        protected virtual void OnMatchHasStarted() {
            foreach (var pc in Server.PlayerControllers) {
                SpawnPlayer(pc);
            }
        }

        protected virtual void OnMatchHasEnded() { }

        protected virtual void OnLeavingMap() { }

        protected virtual void OnPlayerSpawned(Pawn player) { }

        protected virtual void SpawnPlayer(ServerPlayerController pc) {
            Debug.Log($"[Server] <b>Spawning player</b> <i>{pc.Connection}</i>");

            var prefabAddress = GetPlayerPrefabAddress(pc);
            var go = Server.ReplicaManager.InstantiateReplicaAsync(prefabAddress);
            go.Completed += ctx => {
                var spawnPose = GetPlayerSpawnPosition(pc);

                var newPawn = ctx.Result.GetComponent<Pawn>();
                newPawn.Teleport(spawnPose.position, spawnPose.rotation);

                _players.Add(newPawn);
                OnPlayerSpawned(newPawn);

                var result = pc.Possess(newPawn);
                if (!result) {
                    Debug.LogError("[Server] AH FUCK, POSSESSION FAILED");
                }
            };
        }

        protected virtual object GetPlayerPrefabAddress(ServerPlayerController pc) {
            throw new NotImplementedException();
        }

        protected virtual Pose GetPlayerSpawnPosition(ServerPlayerController pc) {
            if (PlayerSpawn.s_All.Count == 0)
                return Pose.identity;

            var spawn = PlayerSpawn.s_All[UnityEngine.Random.Range(0, PlayerSpawn.s_All.Count)];
            var spawnPosition = spawn.GetRandomizedPosition();
            return new Pose(spawnPosition, spawn.transform.rotation);
        }
    }
}