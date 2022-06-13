using UnityEngine;

namespace GameFramework.FeelsSystem {
    public class ScreenShake : FeelBase {
        public AnimationCurve Intensity = AnimationCurve.Linear(0, 0.5f, 1, 0);


        public override void Do() => FeelsManager.Main.Add(this);

        static float s_trauma;

        static float PN(float x) => (Mathf.PerlinNoise(x * 12, 0) - 0.5f) * 2;

        public static Quaternion CalculateCurrentRotation() {
            var trauma = s_trauma * s_trauma;
            return Quaternion.Euler(
                6 * trauma * PN(Time.time),
                6 * trauma * PN(Time.time + 1),
                6 * trauma * PN(Time.time + 2));
        }

        public static Vector2 CalculateCurrentOffset() {
            var trauma = s_trauma * s_trauma;
            return new Vector2(
                0.05f * trauma * PN(Time.time + 3),
                0.05f * trauma * PN(Time.time + 4));
        }

        public override void Reset() {
            s_trauma = 0;
        }

        public override void Evaluate(float t) {
            var intensity = Intensity.Evaluate(t);
            s_trauma = Mathf.Clamp01(s_trauma + intensity);
        }
    }
}