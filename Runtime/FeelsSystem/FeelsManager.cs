using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework.FeelsSystem {
    public class FeelsManager : MonoBehaviour {
        public static FeelsManager Main;

        readonly List<(float, FeelBase)> _activeFeels = new();

        public void Add(FeelBase feel) {
            _activeFeels.Add((Time.time, feel));
        }

        protected void Update() {
            for (int i = 0; i < _activeFeels.Count; i++) {
                var timeAndFeel = _activeFeels[i];
                var feel = timeAndFeel.Item2;
                feel.ResetFrame();
            }

            for (int i = 0; i < _activeFeels.Count; i++) {
                var timeAndFeel = _activeFeels[i];
                var feel = timeAndFeel.Item2;

                var t = Time.time - timeAndFeel.Item1;
                if (t > feel.Duration) {
                    _activeFeels.RemoveAt(i);
                    continue;
                }

                t /= feel.Duration;
                Assert.IsTrue(t >= 0 && t <= 1);

                feel.Evaluate(t);
            }
        }

        protected void Start() {
            Main = this;
        }
    }
}