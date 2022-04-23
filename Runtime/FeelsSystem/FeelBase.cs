namespace GameFramework.FeelsSystem {
    public abstract class FeelBase : IFeel {
        public float Duration = 1;

        public abstract void Do();

        public abstract void Reset();
        public abstract void Evaluate(float t);
    }
}