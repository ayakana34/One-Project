using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HoodedTestBuilder
{
    const string Dir = "Assets/Characters/Hooded";
    const string ModelPath = Dir + "/Hooded.fbx";
    const string ControllerPath = Dir + "/Hooded.controller";
    const string BaseTexPath = Dir + "/Textures/Hooded_BaseColor.png";
    const string NormalTexPath = Dir + "/Textures/Hooded_Normal.png";
    const string MaterialPath = Dir + "/Hooded.mat";
    const string ScenePath = "Assets/Scenes/CharacterTest.unity";
    const string NoFrictionPath = "Assets/Art/NoFriction.physicsMaterial2D";
    const float CharacterHeight = 1.6f;

    static string AutoKey => "HoodedTestBuilder.Auto.v12." + Application.dataPath;

    static HoodedTestBuilder()
    {
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        if (AssetImporter.GetAtPath(ModelPath) == null) return;

        EditorPrefs.SetBool(AutoKey, true);
        Build();
        if (EditorSceneManager.GetActiveScene().path == ScenePath)
        {
            EditorPrefs.SetBool(CharacterAutoShot.PrefKey, true);
            EditorApplication.EnterPlaymode();
        }
    }

    [MenuItem("Tools/MVP/Build Hooded Character Test")]
    public static void Build()
    {
        try
        {
            ConfigureImporters();
            var controller = BuildController();
            BuildScene(controller);
            RenderDiagnostics();
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) EditorSceneManager.SaveScene(active);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Hooded] Build failed: " + e);
        }
    }

    static Avatar FindAvatar(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();

    static AnimationClip FindClip(string clipName) =>
        AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);

    static void ConfigureImporters()
    {
        var model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        model.importAnimation = true;
        model.animationType = ModelImporterAnimationType.Generic;
        model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        model.clipAnimations = new ModelImporterClipAnimation[0];
        model.SaveAndReimport();

        BuildMaterial();

        model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        var clips = model.defaultClipAnimations;
        Debug.Log("[Hooded] takes=" + model.importedTakeInfos.Length + ", defaultClips=" + clips.Length + ": " + string.Join(", ", clips.Select(c => c.name)));
        foreach (var c in clips)
        {
            bool loop = c.name != "Jump";
            c.loopTime = loop;
            c.loopPose = loop;
            c.lockRootRotation = true;
            c.lockRootHeightY = true;
            c.lockRootPositionXZ = true;
        }
        model.clipAnimations = clips;
        model.SaveAndReimport();
    }

    static Material BuildMaterial()
    {
        AssetDatabase.ImportAsset(BaseTexPath);
        AssetDatabase.ImportAsset(NormalTexPath);
        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(NormalTexPath);
        if (normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexPath));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexPath));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        var model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "material_0"), mat);
        model.SaveAndReimport();
        Debug.Log("[Hooded] Material built: base=" + (mat.GetTexture("_BaseMap") != null) + ", normal=" + (mat.GetTexture("_BumpMap") != null));
        return mat;
    }

    // Renders the model in a few poses/angles to PNG files so the result can be checked without the editor UI.
    static void RenderDiagnostics()
    {
        string outDir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(outDir);

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        var camGo = new GameObject("DiagCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f);
        var lightGo = new GameObject("DiagLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

        var poses = new (string label, string clipName, float time)[]
        {
            ("bind", null, 0f),
            ("idle", "Idle", 1.0f),
            ("run", "Run", 0.3f),
            ("run2", "Run", 0.8f),
            ("jump", "Jump", 1.0f),
        };
        var views = new (string label, Vector3 dir)[]
        {
            ("front", new Vector3(0f, 0f, 1f)),
            ("side", new Vector3(1f, 0f, 0f)),
            ("top", new Vector3(0f, 1f, 0.001f)),
        };

        var rt = new RenderTexture(512, 512, 24);
        var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
        foreach (var (plabel, clipName, time) in poses)
        {
            if (clipName != null)
            {
                var clip = FindClip(clipName);
                var binds = AnimationUtility.GetCurveBindings(clip);
                int resolved = binds.Count(bd => bd.path == "" || inst.transform.Find(bd.path) != null);
                Debug.Log("[Hooded] clip " + clipName + ": len=" + clip.length.ToString("F2") + " bindings=" + binds.Length + " resolved=" + resolved
                    + " e.g. " + string.Join(" | ", binds.Take(3).Select(bd => bd.path + ":" + bd.propertyName)));
                var hips = inst.transform.Find("Armature/Root/Hips");
                var before = hips != null ? hips.localRotation : Quaternion.identity;
                clip.SampleAnimation(inst, time);
                if (hips != null) Debug.Log("[Hooded] hips rot " + before.eulerAngles.ToString("F1") + " -> " + hips.localRotation.eulerAngles.ToString("F1"));
                var thigh = inst.transform.Find("Armature/Root/Hips/LeftUpLeg");
                if (thigh != null) Debug.Log("[Hooded] LeftUpLeg rot " + plabel + " = " + thigh.localRotation.eulerAngles.ToString("F1"));
                else Debug.Log("[Hooded] hierarchy: " + inst.name + " > " + string.Join(", ", Enumerable.Range(0, inst.transform.childCount).Select(i => inst.transform.GetChild(i).name)));
            }
            var b = RenderBounds(inst);
            Debug.Log("[Hooded] diag " + plabel + " bounds center=" + b.center + " size=" + b.size);
            cam.orthographicSize = Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.65f;
            foreach (var (vlabel, dir) in views)
            {
                camGo.transform.position = b.center + dir.normalized * 10f;
                camGo.transform.LookAt(b.center, Mathf.Abs(dir.y) > 0.5f ? Vector3.forward : Vector3.up);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, plabel + "_" + vlabel + ".png"), tex.EncodeToPNG());
            }
        }
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(inst);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        Debug.Log("[Hooded] Diagnostics saved: " + outDir);
    }

    static AnimatorController BuildController()
    {
        var idle = FindClip("Idle");
        var run = FindClip("Run");
        var jump = FindClip("Jump");
        if (idle == null || run == null || jump == null)
        {
            Debug.Log("[Hooded] assets: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(ModelPath).Where(o => !(o is Transform) && !(o is GameObject)).Select(o => o.GetType().Name + ":" + o.name)));
            throw new System.Exception("Animation clips not found in the FBX files.");
        }

        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("Run", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        var sRun = sm.AddState("Run");
        sRun.motion = run;
        var sJump = sm.AddState("Jump");
        sJump.motion = jump;
        sm.defaultState = sIdle;

        Link(sIdle, sRun, ("Run", true), ("Grounded", true));
        Link(sRun, sIdle, ("Run", false), ("Grounded", true));
        Link(sIdle, sJump, ("Grounded", false));
        Link(sRun, sJump, ("Grounded", false));
        Link(sJump, sIdle, ("Grounded", true));

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static void Link(AnimatorState from, AnimatorState to, params (string name, bool value)[] conditions)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.1f;
        foreach (var (name, value) in conditions)
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, name);
    }

    static void BuildScene(AnimatorController controller)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
        camGo.transform.position = new Vector3(0f, 1.5f, -10f);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        lightGo.transform.rotation = Quaternion.Euler(35f, 15f, 0f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.35f, 0.38f, 0.45f));
        const string groundMatPath = Dir + "/Test_Ground.mat";
        if (File.Exists(groundMatPath)) AssetDatabase.DeleteAsset(groundMatPath);
        AssetDatabase.CreateAsset(mat, groundMatPath);

        MakeBlock("Ground", new Vector2(0f, -3.5f), new Vector2(24f, 1f), mat);
        MakeBlock("Platform A", new Vector2(-4f, -0.6f), new Vector2(4f, 0.4f), mat);
        MakeBlock("Platform B", new Vector2(4f, 1.4f), new Vector2(4f, 0.4f), mat);
        MakeWall("Wall L", new Vector2(-10.5f, 0f));
        MakeWall("Wall R", new Vector2(10.5f, 0f));

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, -2.9f, 0f);
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 4f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var capsule = player.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(0.7f, CharacterHeight);
        capsule.offset = new Vector2(0f, CharacterHeight / 2f);
        var noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(NoFrictionPath);
        if (noFriction != null) capsule.sharedMaterial = noFriction;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual.transform);
        FitToHeight(model, player.transform.position.y);

        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var avatar = FindAvatar(ModelPath);
        if (avatar != null) animator.avatar = avatar;

        var ctl = player.AddComponent<CharacterTestController>();
        ctl.visual = visual.transform;
        ctl.animator = animator;
        player.AddComponent<CharacterAutoShot>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Hooded] Test scene built: " + ScenePath);
    }

    static void FitToHeight(GameObject model, float feetY)
    {
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = Vector3.one;
        var b = RenderBounds(model);
        float s = CharacterHeight / Mathf.Max(0.0001f, b.size.y);
        model.transform.localScale = Vector3.one * s;

        b = RenderBounds(model);
        var pos = model.transform.position;
        pos.x -= b.center.x - model.transform.parent.position.x;
        pos.y += feetY - b.min.y;
        pos.z -= b.center.z;
        model.transform.position = pos;
    }

    static Bounds RenderBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static void MakeBlock(string name, Vector2 pos, Vector2 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 2f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<BoxCollider2D>();
    }

    static void MakeWall(string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 30f);
    }
}
