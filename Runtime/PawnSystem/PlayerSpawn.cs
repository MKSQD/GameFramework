using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameFramework {
    [AddComponentMenu("GameFramework/PlayerSpawn")]
    public class PlayerSpawn : MonoBehaviour {
        public static List<PlayerSpawn> s_All = new();

        public Vector3 GetRandomizedPosition() {
            var offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            offset.Normalize();

            return transform.position + offset;
        }

        void OnEnable() {
            s_All.Add(this);
        }

        void OnDisable() {
            s_All.Remove(this);
        }

        [MenuItem("GameObject/GameFramework/Player Spawn", false, 10)]
        static void CreateCustomGameObject(MenuCommand menuCommand) {
            var go = new GameObject("Player Spawn");
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
            go.AddComponent<PlayerSpawn>();
        }
    }
}