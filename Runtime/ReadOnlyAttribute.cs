using UnityEngine;
using System;

namespace GameFramework {
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyAttributeDrawer : UnityEditor.PropertyDrawer {
        public override void OnGUI(Rect rect, UnityEditor.SerializedProperty prop, GUIContent label) {
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(rect, prop);
            GUI.enabled = wasEnabled;
        }
    }
#endif
}