using UnityEngine;

namespace GameFramework.FeelsSystem {
    public class CameraOffset : FeelBase {
        public AnimationCurve X, Y, Z;

        public override void Do() => FeelsManager.Main.Add(this);

        public static Vector3 CurrentOffset;
        public override void Reset() {
            CurrentOffset = Vector3.zero;
        }

        public override void Evaluate(float t) {
            CurrentOffset.x += X.Evaluate(t);
            CurrentOffset.y += Y.Evaluate(t);
            CurrentOffset.z += Z.Evaluate(t);
        }
    }
}