using System;
using System.Collections.Generic;
using UnityEngine;

//TODO: Implement Clamp warpmode #feature
//TODO: Implement Stop and Rewind #feature
[ExecuteInEditMode]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class VertexAnimator : MonoBehaviour
{    
    [Serializable]
    public struct AnimationState
    {
        public int layer;
        public float normalizedTime;
        public float playbackSpeed;
        public bool looped;
        public bool enabled;
    }

    [Serializable]
    struct AttachementPoint
    {
        [NonSerialized]
        public int index;
        public string path;
        public Transform transform;
    }

#if UNITY_EDITOR
    [SerializeField]
#endif
    AnimationState[] m_AnimationStates;
    [SerializeField] VertexAnimatorData m_AnimatorData;
    [SerializeField] bool m_HasDefaultClip;
    [SerializeField] int m_DefaultClipIndex;
    [SerializeField] [Range(0f, 1f)] float m_OverlayOpacity;
    [SerializeField] AttachementPoint[] m_AttachementPoints = Array.Empty<AttachementPoint>();
#if UNITY_EDITOR
    [SerializeField]
#endif
    int[] m_SortedAnimationStatesIndices;
#if UNITY_EDITOR
    [SerializeField]
#endif
    ulong[] m_HashAnimationStateMapping;
    
    int m_CurrentAnimationStateIndex;
    float m_CurrentAnimationStateNormalizedTime;
    float m_CurrentAnimationStatePlaybackSpeed;
    float m_CurrentAnimationStateTime0;
    float m_CurrentAnimationStateScaledInverseDuration;
    bool m_Dirty;

    bool m_OverlayOpacityDirty;

    MeshRenderer m_MeshRenderer;
    MaterialPropertyBlock m_MaterialPropertyBlock;

    class AnimationStateIndicesComparer : IComparer<int>
    {
        public AnimationState[] animationStates;

        public int Compare(int x, int y)
        {
            return animationStates[x].layer.CompareTo(animationStates[y].layer);
        }
    }

    static readonly AnimationStateIndicesComparer s_Comparer = new AnimationStateIndicesComparer();

    public bool isPlaying => m_CurrentAnimationStateIndex != -1;

    public float overlayOpacity
    {
        get { return m_OverlayOpacity; }
        set
        {
            value = Mathf.Clamp01(value);
            if (!Mathf.Approximately(value, m_OverlayOpacity))
            {
                m_OverlayOpacity = value;
                m_OverlayOpacityDirty = true;
            }
        }
    }

    public int FindAnimationState(int hash)
    {
        int index = -1;
        for (int i = 0, length = m_HashAnimationStateMapping.Length; i < length; i++)
        {
            if ((int)m_HashAnimationStateMapping[i] == hash)
            {
                index = (int)(m_HashAnimationStateMapping[i] >> 32);
                break;
            }
        }
        return index;
    }

    public AnimationState this[int animationStateIndex]
    {
        get
        {
#if MAP_DEBUG
            if (animationStateIndex < 0 || animationStateIndex >= m_AnimationStates.Length)
            {
                throw new IndexOutOfRangeException();
            }
#endif
            return m_AnimationStates[animationStateIndex]; 
            
        }
    }

    public void UpdateAnimationState(int animationStateIndex, ref AnimationState state)
    {
#if MAP_DEBUG
            if (animationStateIndex < 0 || animationStateIndex >= m_AnimationStates.Length)
            {
                throw new IndexOutOfRangeException();
            }
#endif
        bool sort = m_AnimationStates[animationStateIndex].layer != state.layer;
        if (state.enabled && (sort || !m_AnimationStates[animationStateIndex].enabled))
        {
            for (int i = 0, length = m_AnimationStates.Length; i < length; i++)
            {
                if (m_AnimationStates[i].layer == state.layer)
                {
                    m_AnimationStates[i].enabled = i == animationStateIndex;
                }
            }
        }

        m_AnimationStates[animationStateIndex] = state;

        if (m_AnimationStates[animationStateIndex].normalizedTime < 0f)
        {
            Debug.LogWarning("Negative normalized time");
            m_AnimationStates[animationStateIndex].normalizedTime = 0f;
        }

        if (m_AnimationStates[animationStateIndex].playbackSpeed < 0f)
        {
            Debug.LogWarning("Negative playback speed");
            m_AnimationStates[animationStateIndex].playbackSpeed = 0f;
        }

        if (sort)
        {
            s_Comparer.animationStates = m_AnimationStates;
            Array.Sort(m_SortedAnimationStatesIndices, s_Comparer);
            s_Comparer.animationStates = null;
        }

        m_Dirty = true;
    }

    static readonly StringHash m_StringHash = new StringHash();

    public static int StringToHash(string name)
    {
        return m_StringHash.GetHash(name);
    }

    public void Play(int hash, PlayMode playMode)
    {
        int index = FindAnimationState(hash);
        if (index == -1)
        {
            Debug.LogWarningFormat(this, "Animation state (Hash = {0}, Name = {1}) not found", hash, m_StringHash.GetString(hash));
            return;
        }
        if (playMode == PlayMode.StopAll)
        {
            for (int i = 0, length = m_AnimationStates.Length; i < length; i++)
            {
                m_AnimationStates[i].enabled = i == index;
            }
        }
        else // playMode == PlayMode.StopSameLayer
        {
            int layer = m_AnimationStates[index].layer;
            for (int i = 0, length = m_AnimationStates.Length; i < length; i++)
            {
                if (m_AnimationStates[i].layer == layer)
                {
                    m_AnimationStates[i].enabled = i == index;
                }
            }
        }
        m_AnimationStates[index].normalizedTime = 0f;
        m_Dirty = true;
    }

    static bool s_PropertyIDsInitialized;
    static int s_ClipDataPropertyID;
    static int s_OverlayOpacityID;

    void OnEnable()
    {
        if (!s_PropertyIDsInitialized)
        {
            s_ClipDataPropertyID = Shader.PropertyToID("_ClipData");
            s_OverlayOpacityID = Shader.PropertyToID("_OverlayOpacity");
            s_PropertyIDsInitialized = true;
        }

        if (m_MaterialPropertyBlock == null)
        {
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
        }

        if (!m_AnimatorData)
        {
            return;
        }
        
        int length = m_AnimatorData.numClips;
        m_AnimationStates = new AnimationState[length];
        m_SortedAnimationStatesIndices = new int[length];
        m_HashAnimationStateMapping = new ulong[length];
        for (int i = 0; i < length; i++)
        {
            VertexAnimatorData.ClipInfo clipInfo = m_AnimatorData.GetClipInfo(i);
            m_AnimationStates[i].layer = 0;
            m_AnimationStates[i].playbackSpeed = clipInfo.playbackSpeed;
            m_AnimationStates[i].normalizedTime = 0f;
            m_AnimationStates[i].looped = clipInfo.loop;
            m_AnimationStates[i].enabled = m_HasDefaultClip && m_DefaultClipIndex == i;
            m_SortedAnimationStatesIndices[i] = i;
            m_HashAnimationStateMapping[i] = ((ulong)i << 32) | (uint)m_StringHash.GetHash(clipInfo.name);
        }
        for (int i = 0; i < m_AttachementPoints.Length; i++)
        {
            m_AttachementPoints[i].index = m_AnimatorData.FindAttachementPoint(m_AttachementPoints[i].path);
        }
        m_CurrentAnimationStateIndex = -1;
        m_Dirty = true;

        m_MeshRenderer = GetComponent<MeshRenderer>();
        if (m_MeshRenderer)
        {
            if (Application.isPlaying)
            {
                Material[] materials = m_MeshRenderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = VertexAnimatorMaterialCache.current.GetVertexAnimatedMaterial(materials[i], m_AnimatorData);
                }
                m_MeshRenderer.sharedMaterials = materials;
            }
        }
        else
        {
            Debug.LogWarning("No MeshRenderer component found", this);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (m_AttachementPoints != null)
        {
            var list = new List<AttachementPoint>();
            if (m_AnimatorData)
            {
                foreach (AttachementPoint item in m_AttachementPoints)
                {
                    if (!string.IsNullOrEmpty(item.path) && m_AnimatorData.FindAttachementPoint(item.path) != -1)
                    {
                        list.Add(item);
                    }
                }
            }
            m_AttachementPoints = list.ToArray();
        }
        m_OverlayOpacityDirty = true;
    }
    
    int m_AnimatorDataInstanceID;
#endif

    void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (m_AnimatorData && (m_AnimatorData.GetInstanceID() != m_AnimatorDataInstanceID || GetComponent<MeshRenderer>() && GetComponent<MeshRenderer>() != m_MeshRenderer))
            {
                m_AnimatorDataInstanceID = m_AnimatorData.GetInstanceID();
                OnEnable();
            }
            return;
        }
#endif

        if (!m_AnimatorData || m_AnimationStates.Length == 0)
        {
            return;
        }

        bool updateClipData = false;
        bool updatePropertyBlock = false;

        do
        {
            if (m_Dirty)
            {
                int topAnimationStateIndex = -1;
                for (int i = m_AnimationStates.Length - 1; i >= 0; i--)
                {
                    int animationStateIndex = m_SortedAnimationStatesIndices[i];
                    if (m_AnimationStates[animationStateIndex].enabled)
                    {
                        topAnimationStateIndex = animationStateIndex;
                        break;
                    }
                }

                if (m_CurrentAnimationStateIndex != topAnimationStateIndex)
                {
                    m_CurrentAnimationStateIndex = topAnimationStateIndex;
                    if (topAnimationStateIndex != -1)
                    {
                        m_CurrentAnimationStateTime0 = Time.timeSinceLevelLoad;
                        m_CurrentAnimationStateScaledInverseDuration = 0f;
                        m_CurrentAnimationStateNormalizedTime = m_AnimationStates[topAnimationStateIndex].normalizedTime;
                        m_CurrentAnimationStatePlaybackSpeed = m_AnimationStates[topAnimationStateIndex].playbackSpeed;
                    }
                    updateClipData = true;
                }
                else if (m_CurrentAnimationStateIndex != -1)
                {
                    if (Mathf.Abs(m_CurrentAnimationStateNormalizedTime - m_AnimationStates[m_CurrentAnimationStateIndex].normalizedTime) > 0.001f)
                    {
                        m_CurrentAnimationStateNormalizedTime = m_AnimationStates[m_CurrentAnimationStateIndex].normalizedTime;
                        m_CurrentAnimationStateTime0 = Time.timeSinceLevelLoad - (m_CurrentAnimationStateNormalizedTime - Mathf.Floor(m_CurrentAnimationStateNormalizedTime)) / m_CurrentAnimationStateScaledInverseDuration;
                        updateClipData = true;
                    }
                    if (Mathf.Abs(m_CurrentAnimationStatePlaybackSpeed - m_AnimationStates[m_CurrentAnimationStateIndex].playbackSpeed) > 0.001f)
                    {
                        m_CurrentAnimationStatePlaybackSpeed = m_AnimationStates[m_CurrentAnimationStateIndex].playbackSpeed;
                        updateClipData = true;
                    }
                }

                m_Dirty = false;
            }

            if (m_CurrentAnimationStateIndex != -1)
            {
                m_AnimationStates[m_CurrentAnimationStateIndex].normalizedTime = m_CurrentAnimationStateNormalizedTime = (Time.timeSinceLevelLoad - m_CurrentAnimationStateTime0) * m_CurrentAnimationStateScaledInverseDuration;
                if (!m_AnimationStates[m_CurrentAnimationStateIndex].looped)
                {
                    if (m_CurrentAnimationStateNormalizedTime >= 1f)
                    {
                        m_AnimationStates[m_CurrentAnimationStateIndex].enabled = false;
                        m_AnimationStates[m_CurrentAnimationStateIndex].normalizedTime = 0f;
                        m_CurrentAnimationStateIndex = -1;
                        updateClipData = true;
                        m_Dirty = true;
                    }
                }
            }
        } while (m_Dirty);

        if (updateClipData)
        {
            float duration, inverseDuration, normalizedBlockWidth, encodedBlockUV;
            m_AnimatorData.GetClipAnimationDataBlockInfo(m_CurrentAnimationStateIndex != -1 ? m_CurrentAnimationStateIndex : m_DefaultClipIndex, 
                out duration, 
                out inverseDuration, 
                out normalizedBlockWidth, 
                out encodedBlockUV);
            m_CurrentAnimationStateTime0 = 0f;
            m_CurrentAnimationStateScaledInverseDuration = 0f;
            if (m_CurrentAnimationStateIndex != -1)
            {
                float playbackSpeed = Mathf.Max(m_CurrentAnimationStatePlaybackSpeed, 0.0001f);
                m_CurrentAnimationStateTime0 = Time.timeSinceLevelLoad - duration * (m_CurrentAnimationStateNormalizedTime - Mathf.Floor(m_CurrentAnimationStateNormalizedTime)) / playbackSpeed;
                m_CurrentAnimationStateScaledInverseDuration = inverseDuration * playbackSpeed;
            }
            m_MaterialPropertyBlock.SetVector(s_ClipDataPropertyID,
                new Vector4(
                    m_CurrentAnimationStateTime0,
                    m_CurrentAnimationStateScaledInverseDuration,
                    normalizedBlockWidth,
                    encodedBlockUV));
            updatePropertyBlock = true;
        }

        if (m_OverlayOpacityDirty)
        {
            m_OverlayOpacityDirty = false;
            //m_MaterialPropertyBlock.SetFloat(s_OverlayOpacityID, m_OverlayOpacity);
            //updatePropertyBlock = true;
        }

        if (updatePropertyBlock && m_MeshRenderer)
        {
            m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock);
        }


        if (m_CurrentAnimationStateIndex != -1)
        {
            int numFrames, frameOffset;
            m_AnimatorData.GetClipFramesInfo(m_CurrentAnimationStateIndex, out numFrames, out frameOffset);
            int length = m_AttachementPoints.Length;
            for (int i = 0; i < length; i++)
            {
                int index = m_AttachementPoints[i].index;
                Transform attachementPointTransform = m_AttachementPoints[i].transform;
                if (!ReferenceEquals(attachementPointTransform, null) & index != -1)
                {
                    float warpedNormalizedTime = m_CurrentAnimationStateNormalizedTime - Mathf.Floor(m_CurrentAnimationStateNormalizedTime);
                    float interpolant = warpedNormalizedTime * numFrames;
                    interpolant = interpolant - Mathf.Floor(interpolant);
                    int frame0 = Mathf.FloorToInt(warpedNormalizedTime * numFrames);
                    int frame1 = Mathf.Min(frame0 + 1, numFrames - 1);
                    Vector3 position0, position1;
                    Quaternion quaternion0, quaternion1;
                    m_AnimatorData.GetAttachementPointFrame(index, frame0 + frameOffset, out position0, out quaternion0);
                    m_AnimatorData.GetAttachementPointFrame(index, frame1 + frameOffset, out position1, out quaternion1);
                    attachementPointTransform.localPosition = Vector3.LerpUnclamped(position0, position1, interpolant);
                    attachementPointTransform.localRotation = Quaternion.LerpUnclamped(quaternion0, quaternion1, interpolant);   
                }
            }
        }
    }
}
