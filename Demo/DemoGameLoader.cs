using System.Collections;
using Cube.Replication;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class DemoGameLoader : MonoBehaviour {
    protected IEnumerator Start() {
        yield return NetworkObjectLookup.Load();
        yield return NetworkPrefabLookup.Load();
        yield return Addressables.InstantiateAsync("Server Game");
        yield return Addressables.InstantiateAsync("Client Game");
    }
}
