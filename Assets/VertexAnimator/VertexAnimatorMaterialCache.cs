using System.Collections.Generic;
using UnityEngine;

//TODO: Verify that it cleans up correctly created material on scene change
public class VertexAnimatorMaterialCache : MonoBehaviour
{
    static VertexAnimatorMaterialCache s_Current;

    public static VertexAnimatorMaterialCache current
    {
        get
        {
            if (s_Current)
            {
                return s_Current;
            }
            var gameObject =
                new GameObject(typeof(VertexAnimatorMaterialCache).Name, typeof(VertexAnimatorMaterialCache))
                {
                    hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild |
                                HideFlags.DontSaveInEditor | HideFlags.NotEditable
                };
            return s_Current = gameObject.GetComponent<VertexAnimatorMaterialCache>();
        }
    }

    readonly Dictionary<ulong, Material> m_Materials = new Dictionary<ulong, Material>();

    static bool s_PropertyIDsInitialized;
    static int s_AnimDataTexPropertyID;
    static int s_AnimDataInfoPropertyID;

    public Material GetVertexAnimatedMaterial(Material originalMaterial, VertexAnimatorData vertexAnimatorData)
    {
        if (!s_PropertyIDsInitialized)
        {
            s_AnimDataTexPropertyID = Shader.PropertyToID("_AnimDataTex");
            s_AnimDataInfoPropertyID = Shader.PropertyToID("_AnimDataInfo");
            s_PropertyIDsInitialized = true;
        }

        ulong key = ((ulong)vertexAnimatorData.GetInstanceID() << 32) | (uint)originalMaterial.GetInstanceID();
        Material material;
        if (!m_Materials.TryGetValue(key, out material))
        {
            Texture animationDataTexture = vertexAnimatorData.animationDataTexture;
            material = new Material(originalMaterial) {
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.NotEditable
            };
            material.SetTexture(s_AnimDataTexPropertyID, animationDataTexture);
            material.SetVector(s_AnimDataInfoPropertyID, new Vector4(0f, 0f, 1f / animationDataTexture.width / 2f + 0.0001f, 1f / animationDataTexture.height));
            material.EnableKeyword("VERTEX_ANIMATED");
            m_Materials.Add(key, material);
        }
        return material;
    }
}
