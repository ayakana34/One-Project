using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the PlayerVisual prefab from the hooded character, hands it to the Player in SampleScene,
// and plays the game once with Player3DAutoShot so the result can be checked.
[InitializeOnLoad]
public static class Player3DSetup
{
    const string ModelPath = "Assets/Characters/Hooded/Hooded.fbx";
    const string ControllerSource = "Assets/Characters/Hooded/Hooded.controller";
    const string ControllerPath = "Assets/Characters/Hooded/PlayerHooded.controller";
    const string WeaponMatPath = "Assets/Characters/Hooded/Test_Weapon.mat";
    const string PrefabPath = "Assets/Prefabs/PlayerVisual.prefab";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const float VisualHeight = 1.25f; // the capsule is 1 unit tall; the hood sticks out a little
    const float ReferenceHeight = 1.6f; // height the hand blade was designed for
    const float ThrowStateSpeed = 4f;

    static string AutoKey => "Player3DSetup.Auto.v3." + Application.dataPath;

    static Player3DSetup()
    {
        EditorApplication.delayCall += AutoRun;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(Player3DAutoShot.PrefKey, false)) return;
        EditorPrefs.SetBool(Player3DAutoShot.PrefKey, false);
        new GameObject("Player3DAutoShot").AddComponent<Player3DAutoShot>();
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        if (!File.Exists(ModelPath) || !File.Exists(ControllerSource)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorPrefs.SetBool(AutoKey, true);
        Build();

        EditorPrefs.SetBool(Player3DAutoShot.PrefKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/MVP/Build 3D Player")]
    public static void Build()
    {
        try
        {
            PrepareController();
            var prefab = BuildPrefab();
            AssignToPlayer(prefab);
            Debug.Log("[Player3D] PlayerVisual prefab built and assigned to the Player.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Player3D] Build failed: " + e);
        }
    }

    // A copy of the test controller whose throw is fast enough for the real game.
    static void PrepareController()
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        AssetDatabase.CopyAsset(ControllerSource, ControllerPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (var st in ctrl.layers[1].stateMachine.states)
            if (st.state.name == "Throw") st.state.speed = ThrowStateSpeed;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }

    static GameObject BuildPrefab()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var root = new GameObject("PlayerVisual");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
        FitToHeight(model, VisualHeight);

        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        animator.applyRootMotion = false;
        var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (avatar != null) animator.avatar = avatar;

        var visual = root.AddComponent<Player3DVisual>();
        visual.animator = animator;
        visual.handWeapon = AttachHandWeapon(model);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Transform AttachHandWeapon(GameObject model)
    {
        var hand = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RightHand");
        if (hand == null) return null;

        float k = VisualHeight / ReferenceHeight;
        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "HandWeapon";
        Object.DestroyImmediate(blade.GetComponent<BoxCollider>());
        blade.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(WeaponMatPath);
        blade.transform.SetParent(hand, false);
        var ls = hand.lossyScale;
        blade.transform.localRotation = Quaternion.identity;
        blade.transform.localScale = new Vector3(0.06f * k / ls.x, 0.45f * k / ls.y, 0.06f * k / ls.z);
        blade.transform.localPosition = new Vector3(0f, 0.2f * k / ls.y, 0f);
        return blade.transform;
    }

    static void AssignToPlayer(GameObject prefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var player = Object.FindFirstObjectByType<Player>();
        if (player == null) throw new System.Exception("No Player in SampleScene.");

        var so = new SerializedObject(player);
        so.FindProperty("visualPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Scales the model to the wanted height and puts the feet at y = 0, centered on x and z (relative to its parent).
    static void FitToHeight(GameObject model, float height)
    {
        model.transform.localPosition = Vector3.zero;
        model.transform.localScale = Vector3.one;
        var b = MeshBounds(model);
        float s = height / Mathf.Max(0.0001f, b.size.y);
        model.transform.localScale = Vector3.one * s;

        b = MeshBounds(model);
        var pos = model.transform.position;
        pos.x -= b.center.x - model.transform.parent.position.x;
        pos.y += model.transform.parent.position.y - b.min.y;
        pos.z -= b.center.z - model.transform.parent.position.z;
        model.transform.position = pos;
    }

    // Bounds of the actually skinned vertices (renderer.bounds is a cached estimate).
    static Bounds MeshBounds(GameObject go)
    {
        bool first = true;
        var result = new Bounds();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            var toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            foreach (var v in baked.vertices)
            {
                var w = toWorld.MultiplyPoint3x4(v);
                if (first) { result = new Bounds(w, Vector3.zero); first = false; }
                else result.Encapsulate(w);
            }
            Object.DestroyImmediate(baked);
        }
        return result;
    }
}
