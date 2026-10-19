using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds a small 3D dagger (blade, guard, handle, pommel), uses it as the thrown weapon (Weapon prefab) and
// replaces the placeholder blade in the player's hand (PlayerVisual prefab) with the same model.
[InitializeOnLoad]
public static class DaggerSetup
{
    const string Dir = "Assets/Art/Dagger";
    const string PrefabPath = "Assets/Prefabs/Dagger.prefab";
    const string WeaponPrefabPath = "Assets/Prefabs/Weapon.prefab";
    const string PlayerVisualPath = "Assets/Prefabs/PlayerVisual.prefab";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // The dagger is modelled along +X, about 0.74 units long and centered so that it spins around its middle.
    const float Shift = -0.12f;
    const float HandHandleCenter = -0.23f; // where the handle is, so the fist can be placed on it
    const float HandScale = 0.7f;          // the dagger in the hand is a bit smaller than the thrown one

    static string AutoKey => "DaggerSetup.Auto.v8." + Application.dataPath;

    static DaggerSetup()
    {
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        if (!File.Exists(WeaponPrefabPath) || !File.Exists(PlayerVisualPath)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorPrefs.SetBool(AutoKey, true);
        Build();

        // Play the real game once with the 3D player check (it also saves close-ups of the dagger).
        EditorSceneManager.OpenScene(ScenePath);
        EditorPrefs.SetBool(Player3DAutoShot.PrefKey, true);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/MVP/Build Dagger")]
    public static void Build()
    {
        try
        {
            EnsureFolder();
            var dagger = BuildDaggerPrefab();
            ApplyToWeapon(dagger);
            ApplyToPlayerHand(dagger);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(WeaponPrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PlayerVisualPath, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Dagger] Dagger prefab built and used for the thrown weapon and the hand.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Dagger] Build failed: " + e);
        }
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Art", "Dagger");
    }

    static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{Dir}/{name}.mat";
        // Update an existing asset in place: deleting and recreating it leaves the prefab with dangling references
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (isNew) AssetDatabase.CreateAsset(mat, path);
        else EditorUtility.SetDirty(mat);
        return mat;
    }

    // A flat pointed blade (pointing along +X) with a little thickness.
    static Mesh BuildBladeMesh()
    {
        const float length = 0.47f, shoulder = 0.34f, halfWidth = 0.04f, halfThick = 0.012f;
        var outline = new[]
        {
            new Vector2(0f, -halfWidth), new Vector2(shoulder, -halfWidth), new Vector2(length, 0f),
            new Vector2(shoulder, halfWidth), new Vector2(0f, halfWidth)
        };

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void Face(float z, bool flip)
        {
            int start = verts.Count;
            foreach (var p in outline) verts.Add(new Vector3(p.x, p.y, z));
            for (int i = 1; i < outline.Length - 1; i++)
            {
                // Unity's front faces are clockwise
                if (flip) { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
                else { tris.Add(start); tris.Add(start + i + 1); tris.Add(start + i); }
            }
        }

        Face(halfThick, false);
        Face(-halfThick, true);

        for (int i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            int s = verts.Count;
            verts.Add(new Vector3(a.x, a.y, -halfThick));
            verts.Add(new Vector3(b.x, b.y, -halfThick));
            verts.Add(new Vector3(b.x, b.y, halfThick));
            verts.Add(new Vector3(a.x, a.y, halfThick));
            tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
        }

        var mesh = new Mesh { name = "DaggerBlade" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static GameObject Part(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject BuildDaggerPrefab()
    {
        var bladeMat = MakeMaterial("Dagger_Blade", new Color(0.84f, 0.87f, 0.92f), 0.15f, 0.55f);
        var guardMat = MakeMaterial("Dagger_Guard", new Color(0.78f, 0.62f, 0.25f), 0.15f, 0.45f);
        var handleMat = MakeMaterial("Dagger_Handle", new Color(0.27f, 0.16f, 0.1f), 0f, 0.25f);

        string meshPath = $"{Dir}/DaggerBlade.mesh";
        var fresh = BuildBladeMesh();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = fresh;
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        else
        {
            mesh.Clear();
            EditorUtility.CopySerialized(fresh, mesh);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(mesh);
        }

        var root = new GameObject("Dagger");

        var blade = new GameObject("Blade");
        blade.transform.SetParent(root.transform, false);
        blade.transform.localPosition = new Vector3(0.02f + Shift, 0f, 0f);
        blade.AddComponent<MeshFilter>().sharedMesh = mesh;
        blade.AddComponent<MeshRenderer>().sharedMaterial = bladeMat;

        Part(root, "Guard", PrimitiveType.Cube, new Vector3(0f + Shift, 0f, 0f), new Vector3(0.03f, 0.15f, 0.06f), guardMat);
        Part(root, "Handle", PrimitiveType.Cube, new Vector3(-0.11f + Shift, 0f, 0f), new Vector3(0.2f, 0.04f, 0.045f), handleMat);
        Part(root, "Pommel", PrimitiveType.Sphere, new Vector3(-0.22f + Shift, 0f, 0f), new Vector3(0.065f, 0.065f, 0.065f), guardMat);

        AssetDatabase.SaveAssets();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // The thrown weapon keeps its flight code; only the drawing changes (the yellow diamond sprite is switched off).
    static void ApplyToWeapon(GameObject dagger)
    {
        var root = PrefabUtility.LoadPrefabContents(WeaponPrefabPath);
        try
        {
            var old = root.transform.Find("Dagger");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var sprite = root.GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.enabled = false;

            var d = (GameObject)PrefabUtility.InstantiatePrefab(dagger, root.transform);
            d.name = "Dagger";
            var ls = root.transform.localScale; // the weapon root is scaled (0.5, 0.5, 1) for the old sprite
            d.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            d.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            PrefabUtility.SaveAsPrefabAsset(root, WeaponPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyToPlayerHand(GameObject dagger)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerVisualPath);
        try
        {
            var visual = root.GetComponent<Player3DVisual>();
            var hand = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHand");
            if (visual == null || hand == null) throw new System.Exception("PlayerVisual has no Player3DVisual / RightHand.");

            var old = hand.Find("HandWeapon");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var d = (GameObject)PrefabUtility.InstantiatePrefab(dagger, hand);
            d.name = "HandWeapon";
            var ls = hand.lossyScale;
            d.transform.localScale = new Vector3(HandScale / ls.x, HandScale / ls.y, HandScale / ls.z);
            // the blade (+X) points along the bone (+Y); the handle sits in the fist
            d.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            d.transform.localPosition = new Vector3(0f, -HandHandleCenter * HandScale / ls.y, 0f);
            visual.handWeapon = d.transform;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerVisualPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
