using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

public class VertexAnimatorEditorWindow : EditorWindow, ISerializationCallbackReceiver
{
    const int MaxAnimationClipFrames = 1024;

    [MenuItem("Window/Vertex Animator")]
    static void ShowWindow()
    {
        var window = GetWindow<VertexAnimatorEditorWindow>(typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow"));
        window.titleContent = new GUIContent("VertexAnimator");
        window.minSize = new Vector2(300f, 360f);
        window.Show();
    }

    [SerializeField] GameObject m_GameObject;
    [SerializeField] int m_FrameRate = 30;
    [SerializeField] string m_PrefabNameSuffix = " Vertex Animated";

    [Serializable]
    class ClipSettings
    {
        public bool included;
        public bool addLoopFrame;
        public bool overrideFrameRate;
        public int frameRate;
    }

    [Serializable]
    class AttachementPoint
    {
        public string name;
        public string path;
    }

    readonly Dictionary<AnimationClip, ClipSettings> m_ClipSettingsDict = new Dictionary<AnimationClip, ClipSettings>();
    [SerializeField] AnimationClip[] m_Clips = new AnimationClip[0];
    [SerializeField] string[] m_ValidAttachementPointsPaths = new string[0];
    [SerializeField] List<AttachementPoint> m_AttachementPoints = new List<AttachementPoint>();

    [SerializeField] AnimationClip[] m_SerializedAnimationClips;
    [SerializeField] ClipSettings[] m_SerializedClipSettingsArray;

    ReorderableList m_AttachementPointsReorderableList;

    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
        m_SerializedAnimationClips = m_ClipSettingsDict.Keys.ToArray();
        m_SerializedClipSettingsArray = m_ClipSettingsDict.Values.ToArray();
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        if (m_SerializedAnimationClips != null && m_SerializedClipSettingsArray != null)
        {
            m_ClipSettingsDict.Clear();
            for (int i = 0; i < m_SerializedAnimationClips.Length; i++)
            {
                m_ClipSettingsDict.Add(m_SerializedAnimationClips[i], m_SerializedClipSettingsArray[i]);
            }
            m_SerializedAnimationClips = null;
            m_SerializedClipSettingsArray = null;
        }
    }

    static int ClampFrameRate(int value)
    {
        if (value < 5)
        {
            return 5;
        }
        if (value > 120)
        {
            return 120;
        }
        return value;
    }

    static string GetTransformPath(Transform rootTransform, Transform targetTransform)
    {
        var parts = new List<string>();
        Transform transform = targetTransform;
        do
        {
            parts.Add(transform.name);
            transform = transform.parent;
        } while (transform && transform != rootTransform);
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    static Transform[] GetTransformsRecrusive(Transform rootTransform)
    {
        var paths = new List<Transform> { rootTransform };
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform childTransform = rootTransform.GetChild(i);
            paths.AddRange(GetTransformsRecrusive(childTransform));
        }
        return paths.ToArray();
    }

    void OnGUI()
    {
        EditorGUIUtility.labelWidth = 230f;
        EditorGUILayout.Space();
        m_FrameRate = ClampFrameRate(EditorGUILayout.IntField("Default Frame Rate", m_FrameRate));
        m_PrefabNameSuffix = EditorGUILayout.TextField("Prefab Name Suffix", m_PrefabNameSuffix);
        EditorGUI.BeginChangeCheck();
        m_GameObject = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Game Object (Legacy Animated Model)"), m_GameObject, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (m_GameObject)
            {
                m_ClipSettingsDict.Clear();
                m_Clips = AnimationUtility.GetAnimationClips(m_GameObject);
                Array.Sort(m_Clips, (left, right) => EditorUtility.NaturalCompare(left.name, right.name));
                foreach (AnimationClip clip in m_Clips)
                {
                    m_ClipSettingsDict.Add(clip,
                        new ClipSettings {included = true, overrideFrameRate = false});
                }
                m_ValidAttachementPointsPaths =
                    GetTransformsRecrusive(m_GameObject.GetComponentInChildren<SkinnedMeshRenderer>().rootBone)
                    .Select(transform => GetTransformPath(m_GameObject.transform, transform)).ToArray();
                m_AttachementPoints.Clear();
                m_AttachementPointsReorderableList = null;
            }
        }

        if (m_GameObject)
        {
            if (m_Clips.Length == 0)
            {
                EditorGUILayout.HelpBox("No animation clips found on Game Object.", MessageType.Info);
            }
            else if (!m_GameObject.GetComponentInChildren<SkinnedMeshRenderer>())
            {
                EditorGUILayout.HelpBox("Game Object has no SkinnedMeshRenderer.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Clips:");
                EditorGUI.indentLevel++;
                for (int i = 0; i < m_Clips.Length; i++)
                {
                    AnimationClip clip = m_Clips[i];
                    ClipSettings clipSettings = m_ClipSettingsDict[clip];
                    int frames = Mathf.CeilToInt(clip.length * (clipSettings.overrideFrameRate ? clipSettings.frameRate : m_FrameRate));
                    clipSettings.included = EditorGUILayout.ToggleLeft(clip.name + " (" + clip.length + "s, " + frames + " frames)", clipSettings.included);
                    if (clipSettings.included)
                    {
                        EditorGUI.indentLevel += 2;
                        var originalLabelWidth = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth = 180f;
                        clipSettings.addLoopFrame = EditorGUILayout.Toggle("Add Loop Frame", clipSettings.addLoopFrame);
                        bool oldOverrideFrameRate = clipSettings.overrideFrameRate;
                        clipSettings.overrideFrameRate = EditorGUILayout.Toggle("Override Frame Rate", clipSettings.overrideFrameRate);
                        if (clipSettings.overrideFrameRate)
                        {
                            if (oldOverrideFrameRate != clipSettings.overrideFrameRate)
                            {
                                clipSettings.frameRate = m_FrameRate;
                            }
                            clipSettings.frameRate = ClampFrameRate(EditorGUILayout.IntField("Frame Rate", clipSettings.frameRate));
                        }
                        EditorGUIUtility.labelWidth = originalLabelWidth;
                        if (frames > MaxAnimationClipFrames)
                        {
                            EditorGUILayout.HelpBox("Number of frames too large (>" + MaxAnimationClipFrames +")", MessageType.Info);
                        }
                        EditorGUI.indentLevel -= 2;
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
                
                if (m_AttachementPointsReorderableList == null)
                {
                    m_AttachementPointsReorderableList = new ReorderableList(m_AttachementPoints,
                        typeof(AttachementPoint)) {
                        drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Attachement Points:"),
                        drawElementCallback = (rect, index, active, focused) =>
                        {
                            var textFieldRect = rect;
                            textFieldRect.width = EditorGUIUtility.labelWidth * 4 / 5;
                            var labelRect = rect;
                            labelRect.width = rect.width - textFieldRect.width;
                            textFieldRect.x += labelRect.width;
                            EditorGUI.LabelField(labelRect, m_AttachementPoints[index].path);
                            textFieldRect.height -= 2f;
                            m_AttachementPoints[index].name = EditorGUI.TextField(textFieldRect, m_AttachementPoints[index].name);
                        },
                        onAddDropdownCallback = (buttonRect, list) =>
                        {
                            var menu = new GenericMenu();
                            foreach (string path in m_ValidAttachementPointsPaths.Except(m_AttachementPoints.Select(item => item.path)))
                            {
                                menu.AddItem(new GUIContent(path), false, userData =>
                                {
                                    m_AttachementPoints.Add(new AttachementPoint { name = "New Attachement Point", path = (string)userData });
                                }, path);
                            }
                            menu.ShowAsContext();
                        },
                        elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing
                    };
                }
                Rect reorderableListRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth, m_AttachementPointsReorderableList.GetHeight());
                reorderableListRect.xMin += 4f;
                reorderableListRect.xMax -= 4f;
                m_AttachementPointsReorderableList.DoList(reorderableListRect);
            }
        }

        if (GUILayout.Button("Generate"))
        {
            Generate();
        }
    }

    static Vector4 QuaternionFromMatrix(ref Matrix4x4 m)
    {
        var q = Quaternion.LookRotation(m.GetColumn(2).normalized, m.GetColumn(1).normalized);
        return new Vector4(q.x, q.y, q.z, q.w);
    }

    static Vector4 PositionFromMatrix(ref Matrix4x4 m)
    {
        var p = m.GetColumn(3);
        p.w = 0;
        return p;
    }

    static Vector4 EncodeQuaternion(ref Vector4 quat)
    {
        return new Vector4((quat.x + 1.0f) / 2f, (quat.y + 1.0f) / 2f, (quat.z + 1.0f) / 2f, (quat.w + 1.0f) / 2f);
    }

    static float EncodeUV(Vector2 uv)
    {
        Debug.Assert(uv.x >= 0 && uv.y >= 0 && uv.x < 1 && uv.y < 1);
        return Mathf.Floor(uv.x * 2047f) + uv.y;
    }

    static void EncodeRG(float f, out float r, out float g)
    {
        f = Mathf.Clamp01(f);
        if (Mathf.Approximately(f, 1.0f))
        {
            g = 0;
            r = 1f;
        }
        else
        {
            g = f * 255.0f - Mathf.Floor(f * 255.0f);
            r = f - g / 255.0f;
        }
    }

    static void EncodePosition(ref Vector4 pos, out Vector4 encoded1, out Vector4 encoded2)
    {
        var magnitude = pos.magnitude;
        if (magnitude >= 8f)
        {
            Debug.LogWarning("Animation's bounding box too large to encode position values correctly (" + magnitude + ")");
        }
        var normalized = new Vector4(pos.x / magnitude, pos.y / magnitude, pos.z / magnitude, 0);
        var encodedPosition = EncodeQuaternion(ref normalized);
        encodedPosition.w = magnitude / 8f;
        EncodeRG(encodedPosition.x, out encoded1.x, out encoded2.x);
        EncodeRG(encodedPosition.y, out encoded1.y, out encoded2.y);
        EncodeRG(encodedPosition.z, out encoded1.z, out encoded2.z);
        EncodeRG(encodedPosition.w, out encoded1.w, out encoded2.w);
    }

    static float Cross(ref Vector4 left, ref Vector4 right)
    {
        return left.x * right.x + left.y * right.y + left.z * right.z + left.w * right.w;
    }

    static void EnsureShortestArc(ref Vector4 thisQuat, ref Vector4 targetQuat)
    {
        if (Cross(ref thisQuat, ref targetQuat) < 0f)
        {
            thisQuat.x *= -1f;
            thisQuat.y *= -1f;
            thisQuat.w *= -1f;
            thisQuat.z *= -1f;
        }
    }

    static Vector4 ToVector4(ref Quaternion quaternion)
    {
        return new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
    }

    static Quaternion ToQuaternion(ref Vector4 vec)
    {
        return new Quaternion(vec.x, vec.y, vec.z, vec.w);
    }

    struct PositionQuaternionPair
    {
        public Vector4 position;
        public Vector4 quaternion;
    }

    static Matrix4x4 LocalToRootMatrix(Transform transform, Transform rootTransform)
    {
        Matrix4x4 matrix = Matrix4x4.identity;
        do
        {
            matrix = Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale) * matrix;
            transform = transform.parent;
        } while (transform != rootTransform && transform);
        return matrix;
    }

    static int PackBinsRecursive(int[] bins, ref List<int> policy, int offset, int capacity)
    {
        if (bins.Length == 0 || capacity < bins[bins.Length - 1] || offset == bins.Length)
        {
            return capacity;
        }
        int bestPolicyCapacity = capacity;
        List<int> bestPolicy = policy;
        var workingPolicy = new List<int>();
        for (int i = offset; i < bins.Length; i++)
        {
            if (bins[i] <= capacity)
            {
                workingPolicy.AddRange(policy);
                workingPolicy.Add(i);
                int capacity2 = PackBinsRecursive(bins, ref workingPolicy, i + 1, capacity - bins[i]);
                if (capacity2 < bestPolicyCapacity)
                {
                    bestPolicyCapacity = capacity2;
                    List<int> temp = bestPolicy;
                    bestPolicy = workingPolicy;
                    workingPolicy = temp;
                }
                workingPolicy.Clear();
            }
        }
        policy = bestPolicy;
        return bestPolicyCapacity;
    }

    static bool PackBins(List<int> paramBins, int rows, int rowCapacity, List<int[]> perRowPolicy = null)
    {
        var indexedBins = Enumerable.Range(0, paramBins.Count).Select(i => new { index = i, bin = paramBins[i] }).ToList();
        indexedBins.Sort((left, right) => right.bin.CompareTo(left.bin));
        var policy = new List<int>();
        for (int i = 0; i < rows; i++)
        {
            PackBinsRecursive(indexedBins.Select(item => item.bin).ToArray(), ref policy, 0, rowCapacity);
            if (perRowPolicy != null)
            {
                perRowPolicy.Add(policy.Select(p => indexedBins[p].index).ToArray());
            }
            for (int j = policy.Count - 1; j >= 0; j--)
            {
                indexedBins.RemoveAt(policy[j]);
            }
            policy.Clear();
        }
        return indexedBins.Count == 0;
    }

    static int NextPowOfTwo(int value)
    {
        int pow2 = 1;
        while (pow2 < value)
        {
            pow2 <<= 1;
        }
        return pow2;
    }

    static Object CreateOrOverrideAsset(Object asset, string path)
    {
        Object asset1 = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset1)
        {
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (subAsset != asset1)
                {
                    DestroyImmediate(subAsset, true);
                }
            }
            EditorUtility.CopySerialized(asset, asset1);
        }
        else
        {
            AssetDatabase.CreateAsset(asset, path);
            asset1 = asset;
        }
        return asset1;
    }

    void Generate()
    {
        if (!m_GameObject)
        {
            Debug.Log("No Game Object set");
            return;
        }
        if (!m_GameObject.GetComponentInChildren<SkinnedMeshRenderer>())
        {
            Debug.Log("No SkinnedMeshRenderer component found attached to GameObject or one of its children");
            return;
        }

        GameObject gameObject = Instantiate(m_GameObject);
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        var skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
        Transform rootTransform = gameObject.transform;
        var bones = new List<Transform>(skinnedMeshRenderer.bones);
        MeshFilter[] subMeshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter meshFilter in subMeshFilters)
        {
            if (!bones.Contains(meshFilter.transform.parent))
            {
                bones.Add(meshFilter.transform.parent);
            }
        }
        Mesh originalMesh = skinnedMeshRenderer.sharedMesh;
        Matrix4x4[] bindPoses = originalMesh.bindposes.Concat(Enumerable.Repeat(Matrix4x4.identity, bones.Count - originalMesh.bindposes.Length)).ToArray();
        var scenePoses = new Matrix4x4[bones.Count];
        for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
        {
            scenePoses[boneIndex] = LocalToRootMatrix(bones[boneIndex], rootTransform) * bindPoses[boneIndex];
        }

        var clipInfoArray = new VertexAnimatorData.ClipInfo[m_Clips.Length];
        var animationDataClips = new List<PositionQuaternionPair[,]>();

        int totalNumFrames = 0;
        for (int clipIndex = 0; clipIndex < m_Clips.Length; clipIndex++)
        {
            AnimationClip clip = m_Clips[clipIndex];
            clipInfoArray[clipIndex].name = clip.name;
            int frameRate = m_ClipSettingsDict[clip].overrideFrameRate
                ? m_ClipSettingsDict[clip].frameRate
                : m_FrameRate;
            bool hasLoopFrame = m_ClipSettingsDict[clip].addLoopFrame;
            int numFrames = Mathf.CeilToInt(clip.length * frameRate) + (hasLoopFrame ? 1 : 0);
            clipInfoArray[clipIndex].loop = hasLoopFrame || clip.wrapMode == WrapMode.Loop;
            clipInfoArray[clipIndex].playbackSpeed = 1.0f;
            clipInfoArray[clipIndex].inverseDuration = 1.0f / clip.length;
            clipInfoArray[clipIndex].duration = clip.length;
            clipInfoArray[clipIndex].numFrames = numFrames;
            clipInfoArray[clipIndex].frameOffset = clipIndex > 0 ? clipInfoArray[clipIndex - 1].numFrames + clipInfoArray[clipIndex - 1].frameOffset : 0;
            totalNumFrames += numFrames;
        }

        VertexAnimatorData.AttachementPointFrames[] attachementPointsArray = m_AttachementPoints
            .Select(item => new VertexAnimatorData.AttachementPointFrames {
                name = item.name,
                path = item.path,
                positions = new Vector3[totalNumFrames],
                quaternions = new Quaternion[totalNumFrames]
            })
            .ToArray();
        Transform[] attachementPointTransforms = attachementPointsArray.Select(item => rootTransform.Find(item.path)).ToArray();

        for (int clipIndex = 0; clipIndex < m_Clips.Length; clipIndex++)
        {
            AnimationClip clip = m_Clips[clipIndex];
            bool hasLoopFrame = clipInfoArray[clipIndex].loop;
            int numFrames = clipInfoArray[clipIndex].numFrames;
            int frameOffset = clipInfoArray[clipIndex].frameOffset;
            var clipData = new PositionQuaternionPair[bones.Count, numFrames];
            for (int frame = 0; frame < numFrames; frame++)
            {
                float time;
                if (hasLoopFrame)
                {
                    time = frame < numFrames - 1 ? frame / (float) (numFrames - 2) * clip.length : 0f;
                }
                else
                {
                    time = frame / (float) (numFrames - 1) * clip.length;
                }
                clip.SampleAnimation(gameObject, time);
                for (int boneIndex = 0; boneIndex < bones.Count; boneIndex++)
                {
                    Matrix4x4 matrix = LocalToRootMatrix(bones[boneIndex], rootTransform) * bindPoses[boneIndex] * scenePoses[boneIndex].inverse;
                    
                    Vector4 quat = QuaternionFromMatrix(ref matrix);
                    if (frame > 0)
                    {
                        EnsureShortestArc(ref quat, ref clipData[boneIndex, frame - 1].quaternion);
                    }
                    clipData[boneIndex, frame] = new PositionQuaternionPair
                    {
                        position = PositionFromMatrix(ref matrix),
                        quaternion = quat
                    };
                }
                for (int i = 0; i < m_AttachementPoints.Count; i++)
                {
                    Matrix4x4 matrix = LocalToRootMatrix(attachementPointTransforms[i], rootTransform);
                    attachementPointsArray[i].positions[frame + frameOffset] = PositionFromMatrix(ref matrix);
                    Vector4 quat = QuaternionFromMatrix(ref matrix);
                    if (frame > 0)
                    {
                        var vec = ToVector4(ref attachementPointsArray[i].quaternions[frame - 1 + frameOffset]);
                        EnsureShortestArc(ref quat, ref vec);
                    }
                    attachementPointsArray[i].quaternions[frame + frameOffset] = ToQuaternion(ref quat);
                }
            }
            animationDataClips.Add(clipData);
        }

        List<int> clipFrames = clipInfoArray.Select(c => c.numFrames).ToList();
        int blockHeight = bones.Count * 3;
        int width = NextPowOfTwo(clipFrames.Max());
        int height = NextPowOfTwo(blockHeight);
        while (!PackBins(clipFrames, height / blockHeight, width))
        {
            width *= 2;
            height *= 2;
        }
        while (PackBins(clipFrames, height / blockHeight, width / 2))
        {
            width /= 2;
        }
        while (PackBins(clipFrames, height / blockHeight / 2, width))
        {
            height /= 2;
        }

        if (width > 1024 || height > 1024)
        {
            Debug.Log("Animation texture too large, consider reducing the frame rate or number of clips..");
            return;
        }

        var perRowPolicy = new List<int[]>();
        PackBins(clipFrames, height / blockHeight, width, perRowPolicy);

        Texture2D animationDataTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "AnimationDataTexture",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 0
        };

        for (int ii = 0; ii < perRowPolicy.Count; ii++)
        {
            int[] policy = perRowPolicy[ii];
            int xOffset = 0;
            int yOffset = ii * blockHeight;
            for (int jj = 0; jj < policy.Length; jj++)
            {
                int clipIndex = policy[jj];
                int blockWidth = clipFrames[clipIndex];
                var pixels = new Color[blockHeight * blockWidth];
                PositionQuaternionPair[,] clipData = animationDataClips[clipIndex];

                for (int j = 0; j < bones.Count; j++)
                {
                    for (int i = 0; i < blockWidth; i++)
                    {
                        var encodedQuaternion = EncodeQuaternion(ref clipData[j, i].quaternion);
                        Vector4 encodedPosition1, encodedPosition2;
                        EncodePosition(ref clipData[j, i].position, out encodedPosition1, out encodedPosition2);

                        pixels[j * 3 * blockWidth + i] = new Color(encodedQuaternion.x, encodedQuaternion.y, encodedQuaternion.z, encodedQuaternion.w);
                        pixels[(j * 3 + 1) * blockWidth + i] = new Color(encodedPosition1.x, encodedPosition1.y, encodedPosition1.z, encodedPosition1.w);
                        pixels[(j * 3 + 2) * blockWidth + i] = new Color(encodedPosition2.x, encodedPosition2.y, encodedPosition2.z, encodedPosition2.w);
                    }
                }
                animationDataTexture.SetPixels(xOffset, yOffset, blockWidth, blockHeight, pixels);
                clipInfoArray[clipIndex].normalizedBlockWidth = (blockWidth - 1) / (float)width;
                clipInfoArray[clipIndex].encodedBlockUV = EncodeUV(new Vector2(xOffset / (float)width, yOffset / (float)height));
                xOffset += blockWidth;
            }
        }
        animationDataTexture.Apply(false, true);

        var mesh = new Mesh { name = originalMesh.name + " VertexAnimated" };
        Vector2[] uv = originalMesh.uv;
        Vector3[] vertices = originalMesh.vertices;
        for (int i = 0; i < originalMesh.boneWeights.Length; i++)
        {
            var boneWeight = originalMesh.boneWeights[i];
            int boneIndex = boneWeight.boneIndex0;
            float maxWeight = boneWeight.weight0;
            if (boneWeight.weight1 > maxWeight)
            {
                maxWeight = boneWeight.weight1;
                boneIndex = boneWeight.boneIndex1;
            }
            if (boneWeight.weight2 > maxWeight)
            {
                maxWeight = boneWeight.weight2;
                boneIndex = boneWeight.boneIndex2;
            }
            if (boneWeight.weight3 > maxWeight)
            {
                boneIndex = boneWeight.boneIndex3;
            }
            uv[i] = new Vector2(EncodeUV(uv[i]), ((float)boneIndex * 3 + 0.5f) / height);
            vertices[i] = scenePoses[boneIndex].MultiplyPoint(vertices[i]);
        }
        List<int[]> triangles = Enumerable.Range(0, originalMesh.subMeshCount).Select(i => originalMesh.GetTriangles(i)).ToList();
        foreach (MeshFilter meshFilter in subMeshFilters)
        {
            int boneIndex = bones.IndexOf(meshFilter.transform.parent);
            var subMesh = meshFilter.sharedMesh;
            int offset = vertices.Length;
            var matrix = scenePoses[boneIndex] * bindPoses[boneIndex].inverse *
                Matrix4x4.TRS(meshFilter.transform.localPosition, meshFilter.transform.localRotation, meshFilter.transform.localScale);
            vertices = vertices.Concat(subMesh.vertices.Select(v => matrix.MultiplyPoint(v))).ToArray();
            uv = uv.Concat(subMesh.uv.Select(uv1 => new Vector2(EncodeUV(uv1), ((float) boneIndex * 3 + 0.5f) / height))).ToArray();
            triangles.AddRange(Enumerable.Range(0, subMesh.subMeshCount).Select(i => subMesh.GetTriangles(i).Select(t => t + offset).ToArray()));
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.subMeshCount = triangles.Count;
        for (int i = 0; i < triangles.Count; i++)
        {
            mesh.SetTriangles(triangles[i], i);
        }
        mesh.RecalculateBounds();
        MeshUtility.Optimize(mesh);
        MeshUtility.SetMeshCompression(mesh, MeshUtility.GetMeshCompression(originalMesh));

        var animatorData = VertexAnimatorData.Create(animationDataTexture, mesh, clipInfoArray, attachementPointsArray);
        animatorData.name = m_GameObject.name + " AnimatorData";
        
        string path = AssetDatabase.GetAssetPath(m_GameObject);
        path = path.Remove(path.LastIndexOf("/", StringComparison.Ordinal));
        string animatorDataPath = path + "/" + animatorData.name + ".asset";
        animatorData = (VertexAnimatorData)CreateOrOverrideAsset(animatorData, animatorDataPath);
        AssetDatabase.AddObjectToAsset(animationDataTexture, animatorData);
        AssetDatabase.AddObjectToAsset(mesh, animatorData);
        

        Material[] originalMaterial = skinnedMeshRenderer.sharedMaterials.Concat(subMeshFilters.SelectMany(filter =>
            {
                var meshRenderer = filter.GetComponent<MeshRenderer>();
                Material[] materials1 = meshRenderer ? meshRenderer.sharedMaterials : new Material[0];
                return materials1.Concat(Enumerable.Repeat<Material>(null, filter.sharedMesh.subMeshCount - materials1.Length));
            })).ToArray();

        var materials = new List<Material>();
        foreach (Material material in originalMaterial)
        {
            if (!material)
            {
                materials.Add(null);
            }
            int materialIndex = materials.FindIndex(material2 => material.mainTexture == material2.mainTexture &&
                                             material.mainTextureScale == material2.mainTextureScale &&
                                             material.mainTextureOffset == material2.mainTextureOffset);
            if (materialIndex == -1)
            {
                Material vertexAnimatedMaterial =
                    new Material(Shader.Find("VertexAnimated1BoneUnlit")) {
                        name = material.name,
                        mainTexture = material.mainTexture,
                        mainTextureScale = material.mainTextureScale,
                        mainTextureOffset = material.mainTextureOffset,
                        enableInstancing = true
                    };
                if (!AssetDatabase.IsValidFolder(path + "/Materials"))
                {
                    AssetDatabase.CreateFolder(path, "Materials");
                }
                vertexAnimatedMaterial = (Material)CreateOrOverrideAsset(vertexAnimatedMaterial, path + "/Materials/" + vertexAnimatedMaterial.name + " VertexAnimated.mat");
                materials.Add(vertexAnimatedMaterial);
            }
            else
            {
                materials.Add(materials[materialIndex]);
            }
        }

        string prefabPath = path + "/" + m_GameObject.name + m_PrefabNameSuffix + ".prefab";
        var prefab = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;
        if (!prefab)
        {
            var tempGameObject = new GameObject(m_GameObject.name);
            PrefabUtility.SaveAsPrefabAsset(tempGameObject, prefabPath);
            DestroyImmediate(tempGameObject);
            prefab = (GameObject)AssetDatabase.LoadMainAssetAtPath(prefabPath);
        }

        var instantation = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        if (!instantation.GetComponent<MeshRenderer>())
        {
            instantation.gameObject.AddComponent<MeshRenderer>();
        }
        if (!instantation.GetComponent<MeshFilter>())
        {
            instantation.gameObject.AddComponent<MeshFilter>();
        }
        if (!instantation.GetComponent<VertexAnimator>())
        {
            instantation.gameObject.AddComponent<VertexAnimator>();
        }

        instantation.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
        instantation.GetComponent<MeshFilter>().sharedMesh = mesh;

        typeof(VertexAnimator)
            .GetField("m_AnimatorData", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(instantation.GetComponent<VertexAnimator>(), animatorData);

        PrefabUtility.SaveAsPrefabAsset(instantation, prefabPath);

        DestroyImmediate(instantation);
        DestroyImmediate(gameObject);

        AssetDatabase.SaveAssets();

        EditorGUIUtility.PingObject(animatorData);
        EditorGUIUtility.PingObject(prefab);
    }
}
