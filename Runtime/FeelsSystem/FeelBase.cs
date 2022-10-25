namespace GameFramework.FeelsSystem {
    public abstract class FeelBase : IFeel {
        public float Duration = 1;

        public abstract void Exec();

        public abstract void ResetFrame();
        public abstract void Evaluate(float t);
    }
}