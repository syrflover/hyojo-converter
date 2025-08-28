using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using System.Linq;

public class HyojoConverter : EditorWindow
{
    [Serializable]
    private class Payload1
    {
        public string name = "m_BlendShapeWeights";
        public int type = -1;
        public int arraySize;
        public string arrayType = "float";
        public List<Payload2> children = new();

        public Payload1(List<BlendShape> blendShapes)
        {
            var p = new Payload2
            {
                arraySize = blendShapes.Count - 1,
                children = blendShapes,
            };
            arraySize = blendShapes.Count - 1;
            children = new() { p };
        }
    }

    [Serializable]
    private class Payload2
    {
        public string name = "Array";
        public int type = -1;
        public int arraySize;
        public string arrayType = "float";
        public List<BlendShape> children = new();
    }

    [Serializable]
    private class BlendShape
    {
        public string name;
        public int type;
        public int val;
    }

    private static readonly string[] MMD_BLENDSHAPE_NAMES = new string[] {
        "まばたき",
        "笑い",
        "ウィンク",
        "ウィンク右",
        "ウィンク２",
        "ｳｨﾝｸ２右",
        "なごみ",
        "はぅ",
        "びっくり",
        "じと目",
        "ｷﾘｯ",
        "星目",
        "はぁと",
        "瞳小",
        "瞳大",
        "恐ろしい子！",
        "光下",
        "ハイライト消し",
        "Λ",
        "▲",
        "ワ",
        "ω",
        "ω□",
        "□",
        "はんっ！",
        "あ",
        "あ2",
        "い",
        "う",
        "え",
        "お",
        "ん",
        "ぺろっ",
        "にやり",
        "にっこり",
        "口角上げ",
        "口角下げ",
        "口角広げ",
        "真面目",
        "困る",
        "にこり",
        "悲しい",
        "怒り",
        "上",
        "下",
        "涙",
        "頬染め",
        "はちゅ目"
    };

    private const string PREFS_LAST_SAVED_PATH = "HyojoConverterLastSavedPath";
    private const string PREFS_LAST_OPENED_PATH = "HyojoConverterLastOpenedPath";

    private GameObject avatar = null;

    private bool copyZero = true;
    private bool copyMMD = false;

    [MenuItem("Hyojo Converter/Open")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<HyojoConverter>();
        wnd.titleContent = new GUIContent("Hyojo Converter");
    }

    public void CreateGUI()
    {
        // Root
        var root = rootVisualElement;
        root.style.paddingLeft = 10; root.style.paddingRight = 10;
        root.style.paddingTop = 10; root.style.paddingBottom = 10;
        root.style.flexDirection = FlexDirection.Column;

        // Fields
        var avatarField = new ObjectField("Avatar") { objectType = typeof(GameObject), allowSceneObjects = true };
        root.Add(avatarField);

        root.Add(new Separator());

        var copyZeroToggle = new Toggle("Copy Zero") { value = copyZero };
        copyZeroToggle.RegisterValueChangedCallback(evt => copyZero = evt.newValue);
        root.Add(copyZeroToggle);

        var copyMMDToggle = new Toggle("Copy MMD") { value = copyMMD };
        copyMMDToggle.RegisterValueChangedCallback(evt => copyMMD = evt.newValue);
        root.Add(copyMMDToggle);

        root.Add(new Separator());

        var applyAvatarToAnimationClipButton = new Button(() =>
        {
            var faceRenderer = GetFaceRenderer(avatar);
            var blendShapes = GetBlendShapesFromFaceRenderer(faceRenderer);

            // size 값이 존재해서 비었다면 길이는 1임
            if (blendShapes.Count == 1)
            {
                return;
            }

            var clip = new AnimationClip();
            SetBlendShapesToAnimationClip(avatar, clip, blendShapes);
            SaveAnimationClip(clip);
        })
        {
            text = "Save Blend Shapes to .anim"
        };
        applyAvatarToAnimationClipButton.SetEnabled(false);
        root.Add(applyAvatarToAnimationClipButton);

        root.Add(new Separator());

        var copyBlendShapesFromAnimationClipButton = new Button(() =>
        {
            var clip = OpenAnimationClip();
            if (clip != null)
            {
                CopyBlendShapesFromAnimationClip(avatar, clip);
            }
        })
        {
            text = "Copy Blend Shapes from .anim"
        };
        copyBlendShapesFromAnimationClipButton.SetEnabled(false);
        root.Add(copyBlendShapesFromAnimationClipButton);

        root.Add(new Separator());

        var pasteBlendShapesToAnimationClipButton = new Button(() =>
        {
            var clip = new AnimationClip();
            PasteBlendShapesToAnimationClip(avatar, clip);
            SaveAnimationClip(clip);
        })
        {
            text = "Paste Blend Shapes to .anim"
        };
        pasteBlendShapesToAnimationClipButton.SetEnabled(false);
        root.Add(pasteBlendShapesToAnimationClipButton);

        // TODO: 아바타 정면에서 바라본 이미지 프리뷰

        avatarField.RegisterValueChangedCallback(evt =>
        {
            avatar = evt.newValue as GameObject;

            applyAvatarToAnimationClipButton.SetEnabled(true);
            pasteBlendShapesToAnimationClipButton.SetEnabled(true);
            copyBlendShapesFromAnimationClipButton.SetEnabled(true);
        });
    }

    private void SaveAnimationClip(AnimationClip clip)
    {
        var last_saved_path = EditorPrefs.GetString(PREFS_LAST_SAVED_PATH, "Assets");
        var path = EditorUtility.SaveFilePanelInProject("Save facial animation clip", "F_new", "anim", "HelloWorld", last_saved_path);
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(clip, path);
            EditorPrefs.SetString(PREFS_LAST_SAVED_PATH, path);
        }
    }

    private AnimationClip OpenAnimationClip()
    {
        var last_opened_path = EditorPrefs.GetString(PREFS_LAST_OPENED_PATH, "Assets");
        var absPath = EditorUtility.OpenFilePanel("Open facial animation clip", last_opened_path, "anim");
        if (!string.IsNullOrEmpty(absPath))
        {
            EditorPrefs.SetString(PREFS_LAST_OPENED_PATH, absPath);
            var relPath = "Assets" + absPath.Substring(Application.dataPath.Length);
            Debug.Log(absPath + " : " + relPath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(relPath);
        }
        return null;
    }

    private SkinnedMeshRenderer GetFaceRenderer(GameObject avatar)
    {
        var face = avatar.transform.Find("Body");

        if (!face)
        {
            Debug.LogError("Can't find FaceRenderer 'Body'");
            return null;
        }

        var faceRednerer = face.gameObject.GetComponent<SkinnedMeshRenderer>();

        if (faceRednerer.sharedMesh == null)
        {
            Debug.LogError("Can't find 'SkinnedMeshRenderer.sharedMesh'");
            return null;
        }

        return faceRednerer;
    }

    private List<BlendShape> GetBlendShapesFromFaceRenderer(SkinnedMeshRenderer faceRenderer)
    {
        var mesh = faceRenderer.sharedMesh;
        var blendShapes = new List<BlendShape>
        {
            new() {
                name = "size",
                type = 12,
                val = mesh.blendShapeCount,
            }
        };

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            float w = faceRenderer.GetBlendShapeWeight(i); // 0~100

            // Debug.Log(mesh.GetBlendShapeIndex(name) + " " + name + " : " + w);

            blendShapes.Add(new()
            {
                name = "data",
                type = 2,
                val = (int)w,
            });
        }

        return blendShapes;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="avatar"></param>
    /// <param name="animationClip"></param>
    /// <returns>(rendererPath, blendShapes)</returns>
    private List<BlendShape> GetBlendShapesFromAnimationClip(GameObject avatar, AnimationClip animationClip)
    {
        // 아바타 원본 blendshapes를 가져와서,
        // 애니메이션 클립에 있는 blendshapes로 덮어씀

        var faceRenderer = GetFaceRenderer(avatar);
        var blendShapes = GetBlendShapesFromFaceRenderer(faceRenderer);
        var blendShapeNames = faceRenderer.sharedMesh.GetBlendShapeNames();
        var size = faceRenderer.sharedMesh.blendShapeCount;

        var path = GetFaceRendererPath(avatar, faceRenderer);
        for (int i = 0; i < size; i++)
        {
            string prop = $"blendShape.{blendShapeNames[i]}";
            var binding = new EditorCurveBinding { path = path, type = typeof(SkinnedMeshRenderer), propertyName = prop };
            var curve = AnimationUtility.GetEditorCurve(animationClip, binding);
            float v = 0f;
            if (curve != null && curve.length > 0)
            {
                v = curve.Evaluate(0f);
            }
            blendShapes[i + 1].val = (int)Mathf.Clamp(v, 0f, 100f);
        }

        return blendShapes;
    }

    private string GetFaceRendererPath(GameObject avatar, SkinnedMeshRenderer faceRenderer)
    {
        return AnimationUtility.CalculateTransformPath(faceRenderer.transform, avatar.transform);
    }

    private void SetBlendShapesToAnimationClip(GameObject avatar, AnimationClip animationClip, List<BlendShape> blendShapes)
    {
        // .fbx 임베디드 클립 방지
        if (AssetDatabase.IsSubAsset(animationClip) || AssetDatabase.IsForeignAsset(animationClip))
        {
            Debug.LogError("This is a read-only (embedded) clip. Please duplicate it as a .anim file before proceeding.");
            return;
        }

        animationClip.ClearCurves();

        var faceRenderer = GetFaceRenderer(avatar);
        var faceRendererPath = GetFaceRendererPath(avatar, faceRenderer);
        var blendShapeNames = faceRenderer.sharedMesh.GetBlendShapeNames();
        var blendShapeCount = faceRenderer.sharedMesh.blendShapeCount;

        // var xs = new List<string>();

        for (int i = 0; i < blendShapeCount; i++)
        {
            var blendShape = blendShapes[i + 1];
            var blendShapeName = blendShapeNames[i];

            if (!copyZero && blendShape.val == 0)
            {
                continue;
            }

            // Debug.Log($"{blendShapeName} : {MMD_BLENDSHAPE_NAMES.Contains(blendShapeName)}");

            // xs.Add(blendShapeName);

            if (!copyMMD && MMD_BLENDSHAPE_NAMES.Contains(blendShapeName))
            {
                continue;
            }

            var binding = new EditorCurveBinding
            {
                path = faceRendererPath,
                type = typeof(SkinnedMeshRenderer),
                propertyName = $"blendShape.{blendShapeName}"
            };

            var curve = new AnimationCurve(new Keyframe(0f, Mathf.Clamp(blendShape.val, 0f, 100f)));

            AnimationUtility.SetEditorCurve(animationClip, binding, curve);
        }

        // var p = EditorUtility.SaveFilePanel("ddddd", "Assets/", "blendshape names", "json");
        // File.WriteAllText(p, Json.Encode(xs));
    }

    // clip -> clipboard
    private void CopyBlendShapesFromAnimationClip(GameObject avatar, AnimationClip animationClip)
    {
        var blendShapes = GetBlendShapesFromAnimationClip(avatar, animationClip);

        var p = new Payload1(blendShapes);
        EditorGUIUtility.systemCopyBuffer = "GenericPropertyJSON:" + JsonUtility.ToJson(p);
    }

    // clipboard -> clip
    private void PasteBlendShapesToAnimationClip(GameObject avatar, AnimationClip animationClip)
    {
        var c = EditorGUIUtility.systemCopyBuffer;
        var p = JsonUtility.FromJson<Payload1>(c.Substring("GenericPropertyJSON:".Length));
        var blendShapes = p.children[0].children;

        SetBlendShapesToAnimationClip(avatar, animationClip, blendShapes);
    }

    // 작은 구분선
    private class Separator : VisualElement
    {
        public Separator()
        {
            style.height = 1;
            style.marginTop = 6;
            style.marginBottom = 6;
            style.backgroundColor = new Color(0, 0, 0, 0.2f);
        }
    }
}

public static class MeshUtility
{
    public static List<string> GetBlendShapeNames(this Mesh mesh)
    {
        var names = new List<string>();
        for (var i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
        return names;
    }
}

public static class SkinnedMeshRendererUtility
{
    public static float GetBlendShapeWeight(this SkinnedMeshRenderer renderer, string name)
    {
        return renderer.GetBlendShapeWeight(renderer.sharedMesh.GetBlendShapeIndex(name));
    }
}
