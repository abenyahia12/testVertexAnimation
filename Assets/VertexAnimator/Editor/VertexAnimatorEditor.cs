using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VertexAnimator))]
public class VertexAnimatorEditor : Editor
{
    static SerializedProperty GetAttachementPointProperpty(SerializedProperty arrayProperty, string path)
    {
        for (int i = 0; i < arrayProperty.arraySize; i++)
        {
            var element = arrayProperty.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("path").stringValue == path)
            {
                return element;
            }
        }
        var newElement = arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize++);
        newElement.FindPropertyRelative("path").stringValue = path;
        return newElement;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        var animatorDataProperty = serializedObject.FindProperty("m_AnimatorData");
        var overlayOpacityProperty = serializedObject.FindProperty("m_OverlayOpacity");
        var hasDefaultClipProperty = serializedObject.FindProperty("m_HasDefaultClip");

        EditorGUILayout.PropertyField(animatorDataProperty);
        EditorGUILayout.PropertyField(overlayOpacityProperty);
        EditorGUILayout.PropertyField(hasDefaultClipProperty);

        if (animatorDataProperty.objectReferenceValue && hasDefaultClipProperty.boolValue)
        {
            var animatorData = (VertexAnimatorData)animatorDataProperty.objectReferenceValue;
            var defaultClipIndexProperty = serializedObject.FindProperty("m_DefaultClipIndex");
            defaultClipIndexProperty.intValue = 
                EditorGUILayout.Popup("Default Clip", defaultClipIndexProperty.intValue, Enumerable.Range(0, animatorData.numClips).Select(i => animatorData.GetClipInfo(i).name).ToArray());

            var attachementPointsArrayProperty = serializedObject.FindProperty("m_AttachementPoints");
            for (int i = 0; i < animatorData.numAttachementPoints; i++)
            {
                string attachementPointName, path;
                animatorData.GetAttachementPointInfo(i, out attachementPointName, out path);
                var attachementPointProperty = GetAttachementPointProperpty(attachementPointsArrayProperty, path);
                EditorGUILayout.PropertyField(
                    attachementPointProperty.FindPropertyRelative("transform"),
                    new GUIContent(string.IsNullOrEmpty(attachementPointName) ? "Attachement Point " + i : attachementPointName, path));
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}
