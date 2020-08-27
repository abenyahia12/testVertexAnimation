using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VertexAnimatorData))]
public class VertexAnimatorDataEditor : Editor
{
    new VertexAnimatorData target
    {
        get { return (VertexAnimatorData)base.target; }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);
        var clipInfoArray = serializedObject.FindProperty("m_ClipInfoArray");
        for (int i = 0; i < target.numClips; i++)
        {
            VertexAnimatorData.ClipInfo clipInfo = target.GetClipInfo(i);
            SerializedProperty property = clipInfoArray.GetArrayElementAtIndex(i);
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, clipInfo.name + " (" + clipInfo.duration + "s)");
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("playbackSpeed"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("loop"));
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.Space();
        var attachementPointsArrayProperty = serializedObject.FindProperty("m_AttachementPointFrames");
        if (attachementPointsArrayProperty.arraySize != 0)
        {
            EditorGUILayout.LabelField("Attachement Points", EditorStyles.boldLabel);
        }
        for (int i = 0; i < attachementPointsArrayProperty.arraySize; i++)
        {
            SerializedProperty property = attachementPointsArrayProperty.GetArrayElementAtIndex(i);
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.FindPropertyRelative("name").stringValue);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(property.FindPropertyRelative("name"));
                string path = property.FindPropertyRelative("path").stringValue;
                bool tooLong = path.Length > 25;
                EditorGUILayout.LabelField(new GUIContent("Path"), new GUIContent(tooLong ? path.Substring(0, 25) + ".." : path, tooLong ? path : null));
                EditorGUI.indentLevel--;
            }
        }
        serializedObject.ApplyModifiedProperties();
    }

    Editor m_Inspector;

    void OnEnable()
    {
        m_Inspector = CreateEditor(target.mesh, typeof(Editor).Assembly.GetType("UnityEditor.ModelInspector"));
    }

    void OnDisable()
    {
        DestroyImmediate(m_Inspector);
    }

    public override bool HasPreviewGUI()
    {
        return m_Inspector.HasPreviewGUI();
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        m_Inspector.OnPreviewGUI(r, background);
    }

    public override string GetInfoString()
    {
        return m_Inspector.GetInfoString();
    }
}
