using UnityEngine;

namespace GameFramework.FeelsSystem {
    public class ScreenShake : FeelBase {
        public AnimationCurve IntensityOverTime;
        public float Frequency = 1;

        public override void Do() => FeelsManager.Main.Add(this);

        public static Vector2 CurrentOffset;
        public override void Reset() {
            CurrentOffset = Vector2.zero;
        }

        public override void Evaluate(float t) {
            var a = IntensityOverTime.Evaluate(t);

            var noiseX = Mathf.PerlinNoise(Time.time * Frequency, 0);
            noiseX += Mathf.PerlinNoise(Time.time * Frequency + 325325, 0) * 0.25f;
            noiseX = Mathf.Clamp01(noiseX);

            var noiseY = Mathf.PerlinNoise(Time.time * Frequency + 3463, 0);
            noiseY += Mathf.PerlinNoise(Time.time * Frequency + 85681, 0) * 0.25f;
            noiseY = Mathf.Clamp01(noiseY);

            CurrentOffset.x += (noiseX * 2 - 1) * a;
            CurrentOffset.y += (noiseY * 2 - 1) * a;
        }
    }
}