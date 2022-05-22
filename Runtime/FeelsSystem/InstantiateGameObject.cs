using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GameFramework.FeelsSystem {
    public class InstantiateGameObject : IFeel {
        public AssetReferenceGameObject Prefab;

        public void Do(Vector3 position, Quaternion rotation) => Prefab.InstantiateAsync(position, rotation);
    }
}