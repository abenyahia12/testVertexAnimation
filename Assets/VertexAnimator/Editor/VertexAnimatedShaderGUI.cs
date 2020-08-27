using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

[UsedImplicitly]
public class VertexAnimatedShaderGUI : ShaderGUI
{
    readonly Dictionary<Material, Material> m_PreviewMaterials = new Dictionary<Material, Material>();

    Material GetPreviewMaterial(Material target)
    {
        if (m_PreviewMaterials.ContainsKey(target))
        {
            Object.DestroyImmediate(m_PreviewMaterials[target]);
        }
        var previewMaterial = new Material(target) { hideFlags = HideFlags.HideAndDontSave };
        previewMaterial.EnableKeyword("MATERIAL_PREVIEW_GUI");
        m_PreviewMaterials[target] = previewMaterial;
        return previewMaterial;
    }

    static void HackSetTarget(Editor editor, Object target)
    {
        ((Object[]) typeof(Editor).GetField("m_Targets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor))
            [(int) typeof(Editor).GetProperty("referenceTargetIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor, null)]
            = target;
    }

    public override void OnMaterialPreviewGUI(MaterialEditor materialEditor, Rect r, GUIStyle background)
    {
        var realTarget = (Material)materialEditor.target;
        HackSetTarget(materialEditor, GetPreviewMaterial(realTarget));
        base.OnMaterialPreviewGUI(materialEditor, r, background);
        HackSetTarget(materialEditor, realTarget);
    }

    public override void OnMaterialInteractivePreviewGUI(MaterialEditor materialEditor, Rect r, GUIStyle background)
    {
        OnMaterialPreviewGUI(materialEditor, r, background);
    }
}
