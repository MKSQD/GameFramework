using System;
using UnityEngine;

namespace GameFramework {
    public class GameMode : GameModeBase {
        public enum MatchState {
            WaitingToStart,
            InProgress,
            WaitingPostMatch,
            LeavingMap
        }

        public MatchState matchState {
            get;
            internal set;
        }

        public bool hasMatchStarted {
            get { return matchState == MatchState.InProgress; }
        }

        public GameMode(ServerGame server) : base(server) {
            matchState = MatchState.WaitingToStart;
            HandleMatchIsWaitingToStart();
        }

        public void StartMatch() {
            if (matchState != MatchState.WaitingToStart) {
                Debug.LogWarning("StartMatch called in wrong MatchState " + matchState);
                return;
            }

            Debug.Log("[Server][Game] Match starting...");

            matchState = MatchState.InProgress;
            HandleMatchHasStarted();
        }

        public void EndMatch() {
            if (matchState != MatchState.InProgress) {
                Debug.LogWarning("EndMatch called in wrong MatchState " + matchState);
                return;
            }

            matchState = MatchState.WaitingPostMatch;
            HandleMatchHasEnded();

            Debug.Log("[Server][Game] Match has ended");
        }

        public override void StartToLeaveMap() {
            if (matchState != MatchState.WaitingPostMatch) {
                Debug.LogWarning("StartToLeaveMap called in wrong MatchState " + matchState);
                return;
            }

            matchState = MatchState.LeavingMap;
            HandleLeavingMap();
        }

        public override void Tick() {
            switch (matchState) {
                case MatchState.WaitingToStart:
                    if (ReadyToStartMatch()) {
                        StartMatch();
                    }
                    break;

                case MatchState.InProgress:
                    if (ReadyToEndMatch()) {
                        EndMatch();
                    }
                    break;
            }
        }

        public override void HandleNewPlayer(PlayerController pc) {
            if (hasMatchStarted) {
                var pawn = SpawnPlayer(pc);
                pc.Possess(pawn);
            }
        }

        protected virtual bool ReadyToStartMatch() {
            return true;
        }

        protected virtual bool ReadyToEndMatch() {
            return false;
        }

        protected virtual void HandleMatchIsWaitingToStart() {
        }

        protected virtual void HandleMatchHasStarted() {
            foreach (var pc in server.world.playerControllers) {
                var pawn = SpawnPlayer(pc);
                pc.Possess(pawn);
            }
        }

        protected virtual void HandleMatchHasEnded() {
        }

        protected virtual void HandleLeavingMap() {
        }

        protected virtual Pawn SpawnPlayer(PlayerController pc) {
            Debug.Log("[Server][Game] Spawning player " + pc.connection);

            var prefab = GetPlayerPrefab(pc);
            var go = server.server.replicaManager.InstantiateReplica(prefab);

            var spawnPosition = GetPlayerSpawnPosition();
            Debug.Log("   at pos " + spawnPosition);

            var pawn = go.GetComponent<Pawn>();
            pawn.Teleport(spawnPosition, Quaternion.identity);

            return pawn;
        }

        protected virtual GameObject GetPlayerPrefab(PlayerController pc) {
            throw new NotImplementedException();
        }

        protected virtual Vector3 GetPlayerSpawnPosition() {
            var spawn = PlayerSpawn.instance;
            if (spawn == null)
                return Vector3.zero;

            var spawnPosition = spawn.GetRandomizedPosition();
            return spawnPosition;
        }
    }
}