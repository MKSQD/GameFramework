using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework {
    public class RemoteInterp {
        public enum Mode {
            Interpolate,
            Extrapolate
        }

        public struct SampleResult {
            public bool ShouldTeleport;
            public Vector2 Move;
            public float ViewPitch;
            public bool Walking;
            public bool IsCrouching;
            public bool Jumped;
        }

        struct RemoteState {
            public double Timestamp;
            public Vector3 Position;
            public Vector2 Move;
            public Quaternion Rotation;
            public float ViewPitch;
            public bool Walking;
            public bool Crouching;
            public bool Jumped;
        }

        readonly RemoteState[] _states;
        int _numStates;
        readonly Mode _mode;

        public RemoteInterp(Mode mode) {
            _mode = mode;
            if (mode == Mode.Interpolate) {
                _states = new RemoteState[6];
            } else {
                _states = new RemoteState[3];
            }
        }
        public void AddState(Vector3 pos, Vector2 move, float yaw, float viewPitch, bool walking, bool crouching, bool jumped) {
            RemoteState state;
            state.Timestamp = Time.timeAsDouble;
            state.Position = pos;
            state.Move = move;
            state.Rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            state.ViewPitch = viewPitch;
            state.Walking = walking;
            state.Crouching = crouching;
            state.Jumped = jumped;

            for (int i = _states.Length - 1; i >= 1; i--) {
                _states[i] = _states[i - 1];
            }
            _states[0] = state;

            _numStates = Mathf.Min(_numStates + 1, _states.Length);
        }

        public SampleResult Sample(ref Vector3 position, ref Quaternion rotation) {
            double t;
            if (_mode == Mode.Interpolate) {
                t = Time.timeAsDouble - 0.1;
            } else {
                t = Time.timeAsDouble + 0.1; // FactionsClientGame.Main.NetworkInterface.Ping
            }

            var result = new SampleResult();

            if (_numStates == 0) {
                position = Vector3.down * 100;
                result.ShouldTeleport = true;
                return result;
            }
            RemoteState newestState = _states[0];
            if (_numStates == 1) {
                result.ShouldTeleport = (newestState.Position - position).sqrMagnitude > 1;
                position = newestState.Position;
                rotation = newestState.Rotation;
                result.Move = newestState.Move;
                result.Walking = newestState.Walking;
                result.IsCrouching = newestState.Crouching;
                return result;
            }

            Assert.IsTrue(_numStates > 1);

            // Extrapolation
            if (_mode == Mode.Extrapolate || t >= _states[0].Timestamp) {
                // Predict current
                Vector3 currentPredicted;
                float currentA;
                {
                    var actualTimeDiff = Mathf.Max(0, (float)(t - _states[0].Timestamp));
                    var posDiff = _states[0].Position - _states[1].Position;
                    var timeDiff = (float)(_states[0].Timestamp - _states[1].Timestamp);
                    var a = Mathf.Min(actualTimeDiff / timeDiff, 3);
                    currentPredicted = _states[0].Position + posDiff * (float)a;
                    currentA = (float)a;
                }


                // Predict old
                Vector3 prevPredicted = currentPredicted;
                if (_numStates > 2) {
                    var actualTimeDiff = (float)(t - _states[1].Timestamp);
                    var posDiff = _states[1].Position - _states[2].Position;
                    var timeDiff = (float)(_states[1].Timestamp - _states[2].Timestamp);
                    var a = Mathf.Min(actualTimeDiff / timeDiff, 3);
                    prevPredicted = _states[1].Position + posDiff * (float)a;
                }

                var extrapolatedPos = Vector3.Lerp(prevPredicted, currentPredicted, currentA);

                result.ShouldTeleport = (extrapolatedPos - position).sqrMagnitude > 1;
                position = extrapolatedPos;
                rotation = newestState.Rotation;
                result.Move = newestState.Move;
                result.Walking = newestState.Walking;
                result.IsCrouching = newestState.Crouching;
                result.ViewPitch = newestState.ViewPitch;
                return result;
            }

            for (int stateIdx = 1; stateIdx < _numStates; ++stateIdx) {
                if (t >= _states[stateIdx].Timestamp) {
                    RemoteState newState = _states[stateIdx - 1];
                    RemoteState oldState = _states[stateIdx];

                    double length = newState.Timestamp - oldState.Timestamp;
                    float a = 0f;
                    if (length > 0.0001) {
                        a = (float)((t - oldState.Timestamp) / length);
                        a = Mathf.Max(a, 0);
                    }

                    result.ShouldTeleport = (newState.Position - position).sqrMagnitude > 1;

                    position = Vector3.LerpUnclamped(oldState.Position, newState.Position, a);
                    rotation = Quaternion.SlerpUnclamped(oldState.Rotation, newState.Rotation, a);

                    result.Move = Vector2.LerpUnclamped(oldState.Move, newState.Move, a);
                    result.ViewPitch = Mathf.LerpUnclamped(oldState.ViewPitch, newState.ViewPitch, a);
                    result.Walking = newState.Walking;
                    result.IsCrouching = newState.Crouching;
                    result.Jumped = newState.Jumped;
                    return result;
                }
            }

            // The requested time is *older* than any state we have, show oldest
            RemoteState oldestState = _states[_numStates - 1];
            result.ShouldTeleport = (oldestState.Position - position).sqrMagnitude > 1;
            position = oldestState.Position;
            rotation = oldestState.Rotation;
            result.Move = oldestState.Move;
            result.Walking = oldestState.Walking;
            result.IsCrouching = oldestState.Crouching;
            result.ViewPitch = oldestState.ViewPitch;
            return result;
        }

        public void DrawDebugGizmos() {
            Gizmos.color = Color.green;

            RemoteState lastS = _states[0];
            for (int i = 1; i < _numStates; ++i) {
                Gizmos.DrawLine(lastS.Position, _states[i].Position);
                lastS = _states[i];
            }

            Gizmos.color = Color.red;
            for (int i = 0; i < _numStates; ++i) {
                Gizmos.DrawSphere(_states[i].Position, 0.05f);
            }
        }
    }

}