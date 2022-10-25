using UnityEngine;

namespace GameFramework.FeelsSystem {
    public class ScreenShake : FeelBase {
        public AnimationCurve IntensityPosition = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve IntensityRotation = AnimationCurve.Linear(0, 0.5f, 1, 0);


        public override void Exec() => FeelsManager.Main.Add(this);

        static float s_traumaPosition;
        static float s_traumaRotation;

        static float PN(float x) => (Mathf.PerlinNoise(x * 12, 0) - 0.5f) * 2;

        public static Quaternion CalculateCurrentRotation() {
            var trauma = s_traumaRotation * s_traumaRotation;
            return Quaternion.Euler(
                60 * trauma * PN(Time.time * 5),
                60 * trauma * PN(Time.time * 5 + 3),
                0);
        }

        public static Vector2 CalculateCurrentOffset() {
            var trauma = s_traumaPosition * s_traumaPosition;
            return new Vector2(
                0.05f * trauma * PN(Time.time * 5 + 3),
                0.05f * trauma * PN(Time.time * 5 + 7));
        }

        public override void ResetFrame() {
            s_traumaPosition = 0;
            s_traumaRotation = 0;
        }

        public override void Evaluate(float t) {
            s_traumaPosition = Mathf.Clamp01(s_traumaPosition + IntensityPosition.Evaluate(t));
            s_traumaRotation = Mathf.Clamp01(s_traumaRotation + IntensityRotation.Evaluate(t));
        }
    }
}