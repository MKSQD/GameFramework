using UnityEngine.AddressableAssets;

namespace GameFramework.FeelsSystem {
    public class InstantiateGameObject : IFeel {
        public AssetReferenceGameObject Prefab;

        public void Do() => Prefab.InstantiateAsync();
    }
}