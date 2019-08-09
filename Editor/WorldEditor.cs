using UnityEditor;

namespace GameFramework {
    [CustomEditor(typeof(World))]
    public class WorldEditor : Editor {
        public override void OnInspectorGUI() {
            var myTarget = (World)target;

            DrawDefaultInspector();

            EditorGUILayout.LabelField(myTarget.playerControllers.Count + " PlayerControllers");
        }
    }
}