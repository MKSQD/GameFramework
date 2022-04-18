using UnityEngine;

namespace GameFramework.FeelsSystem {
    public class ScreenShake : IFeel {
        public float Duration = 1;
        public AnimationCurve IntensityOverTime;
        public float Frequency = 1;

        public void Do() => FeelsManager.Main.ActiveShakes.Add((Time.time, this));
    }
}