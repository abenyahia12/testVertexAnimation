using System;
using UnityEngine;

public class VertexAnimatorData : ScriptableObject
{
    [Serializable]
    public struct ClipInfo
    {
        public string name;
        public float playbackSpeed;
        public bool loop;
        public float duration;
        public float inverseDuration;
        public float normalizedBlockWidth;
        public float encodedBlockUV;
        public int numFrames;
        public int frameOffset;
    }

    [Serializable]
    public struct AttachementPointFrames
    {
        public string name;
        public string path;
        public Vector3[] positions;
        public Quaternion[] quaternions;
    }

    [SerializeField] Texture m_AnimationDataTexture;
    [SerializeField] Mesh m_Mesh;
    [SerializeField] ClipInfo[] m_ClipInfoArray;
    [SerializeField] AttachementPointFrames[] m_AttachementPointFrames;

    public int numClips
    {
        get { return m_ClipInfoArray.Length; }
    }

    public int numAttachementPoints
    {
        get { return m_AttachementPointFrames.Length; }
    }

    public ClipInfo GetClipInfo(int index)
    {
        if (index < 0 || index >= m_ClipInfoArray.Length)
        {
            throw new IndexOutOfRangeException();
        }
        return m_ClipInfoArray[index];
    }
    
    public void GetClipAnimationDataBlockInfo(int index, out float duration, out float inverseDuration, out float normalizedBlockWidth, out float encodedBlockUV)
    {
        if (index < 0 || index >= m_ClipInfoArray.Length)
        {
            throw new IndexOutOfRangeException();
        }
        duration = m_ClipInfoArray[index].duration;
        inverseDuration = m_ClipInfoArray[index].inverseDuration;
        normalizedBlockWidth = m_ClipInfoArray[index].normalizedBlockWidth;
        encodedBlockUV = m_ClipInfoArray[index].encodedBlockUV;
    }

    public void GetClipFramesInfo(int index, out int numFrames, out int frameOffset)
    {
        if (index < 0 || index >= m_ClipInfoArray.Length)
        {
            throw new IndexOutOfRangeException();
        }
        numFrames = m_ClipInfoArray[index].numFrames;
        frameOffset = m_ClipInfoArray[index].frameOffset;
    }

    public int FindAttachementPoint(string path)
    {
        int length = m_AttachementPointFrames.Length;
        for (int i = 0; i < length; i++)
        {
            if (m_AttachementPointFrames[i].path == path)
            {
                return i;
            }
        }
        return -1;
    }

    public void GetAttachementPointFrame(int attachementPointIndex, int frame, out Vector3 position, out Quaternion quaternion)
    {
        if (attachementPointIndex < 0 || attachementPointIndex >= m_AttachementPointFrames.Length)
        {
            throw new IndexOutOfRangeException();
        }
        if (frame < 0 || frame >= m_AttachementPointFrames[0].positions.Length)
        {
            throw new IndexOutOfRangeException();
        }
        position = m_AttachementPointFrames[attachementPointIndex].positions[frame];
        quaternion = m_AttachementPointFrames[attachementPointIndex].quaternions[frame];
    }

    public void GetAttachementPointInfo(int index, out string outName, out string path)
    {
        if (index < 0 || index >= m_AttachementPointFrames.Length)
        {
            throw new IndexOutOfRangeException();
        }
        outName = m_AttachementPointFrames[index].name;
        path = m_AttachementPointFrames[index].path;
    }

    public Texture animationDataTexture
    {
        get { return m_AnimationDataTexture; }
    }

    public Mesh mesh
    {
        get { return m_Mesh; }
    }

    public static VertexAnimatorData Create(Texture animationDataTexture, Mesh preProcessedMesh, ClipInfo[] clipInfoArray, AttachementPointFrames[] attachementPoints)
    {
        if (!animationDataTexture || !preProcessedMesh || clipInfoArray == null)
        {
            throw new ArgumentException();
        }
        var data = CreateInstance<VertexAnimatorData>();
        data.m_AnimationDataTexture = animationDataTexture;
        data.m_Mesh = preProcessedMesh;
        data.m_ClipInfoArray = (ClipInfo[])clipInfoArray.Clone();
        data.m_AttachementPointFrames = (AttachementPointFrames[])attachementPoints.Clone();
        for (int i = 0; i < data.m_AttachementPointFrames.Length; i++)
        {
            data.m_AttachementPointFrames[i].positions = (Vector3[])data.m_AttachementPointFrames[i].positions.Clone();
            data.m_AttachementPointFrames[i].quaternions = (Quaternion[])data.m_AttachementPointFrames[i].quaternions.Clone();
        }
        return data;
    }
}
