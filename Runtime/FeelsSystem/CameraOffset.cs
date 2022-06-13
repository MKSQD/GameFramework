using UnityEngine;

namespace GameFramework.FeelsSystem {
    public class CameraOffset : FeelBase {
        public AnimationCurve TranslationX = AnimationCurve.Linear(0, 0, 1, 0), TranslationY = AnimationCurve.Linear(0, 0, 1, 0), TranslationZ = AnimationCurve.Linear(0, 0, 1, 0);
        public AnimationCurve RotationX = AnimationCurve.Linear(0, 0, 1, 0), RotationY = AnimationCurve.Linear(0, 0, 1, 0), RotationZ = AnimationCurve.Linear(0, 0, 1, 0);

        public override void Do() => FeelsManager.Main.Add(this);

        public static Quaternion CurrentRotation;
        public static Vector3 CurrentOffset;
        public override void Reset() {
            CurrentOffset = Vector3.zero;
            CurrentRotation = Quaternion.identity;
        }

        public override void Evaluate(float t) {
            CurrentOffset.x += TranslationX.Evaluate(t);
            CurrentOffset.y += TranslationY.Evaluate(t);
            CurrentOffset.z += TranslationZ.Evaluate(t);

            CurrentRotation *= Quaternion.Euler(RotationX.Evaluate(t), RotationY.Evaluate(t), RotationZ.Evaluate(t));
        }
    }
}