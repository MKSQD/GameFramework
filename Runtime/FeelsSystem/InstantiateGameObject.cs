using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GameFramework.FeelsSystem {
    public class InstantiateGameObject : IFeel {
        public AssetReferenceGameObject Prefab;

        public void ExecAtPosition(Vector3 position) => Prefab.InstantiateAsync(position, Quaternion.identity);
    }
}