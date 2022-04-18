using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace GameFramework.FeelsSystem {
    public class FeelsManager : MonoBehaviour {
        public static FeelsManager Main;

        public List<(float, ScreenShake)> ActiveShakes = new();
        public Vector2 ScreenShakeOffset;

        protected void Start() {
            Main = this;
        }

        protected void Update() {
            ScreenShakeOffset = Vector2.zero;

            for (int i = 0; i < ActiveShakes.Count; i++) {
                var activeShake = ActiveShakes[i];
                var shake = activeShake.Item2;

                var t = Time.time - activeShake.Item1;
                if (t > shake.Duration) {
                    ActiveShakes.RemoveAt(i);
                    continue;
                }
                t /= shake.Duration;

                Assert.IsTrue(t >= 0 && t <= 1);

                var a = shake.IntensityOverTime.Evaluate(t);
                ScreenShakeOffset.x += (Mathf.PerlinNoise(Time.time * shake.Frequency, 0) * 2 - 1) * a;
                ScreenShakeOffset.y += (Mathf.PerlinNoise(Time.time * shake.Frequency + 12345, 0) * 2 - 1) * a;
            }
        }
    }
}