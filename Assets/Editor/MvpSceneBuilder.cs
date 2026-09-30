using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MvpSceneBuilder
{
    const string ArtDir = "Assets/Art";
    const string ShapeDir = "Assets/Art/Shapes";
    const string PrefabDir = "Assets/Prefabs";
    const string MaterialPath = "Assets/Art/NoFriction.physicsMaterial2D";
    const float HalfWidth = 10f;
    const float HalfHeight = 6f;

    static string AutoKey => "MvpSceneBuilder.AutoBuilt." + Application.dataPath;

    static MvpSceneBuilder()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    static void AutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("SampleScene.unity")) return;

        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            if (EditorPrefs.GetBool(AutoKey, false)) return;
            Build();
            EditorPrefs.SetBool(AutoKey, true);
            return;
        }

        EditorPrefs.SetBool(AutoKey, true);
        if (gm.bossPrefab == null || gm.flyerPrefab == null || gm.shooterPrefab == null || gm.gruntPrefab == null || gm.elitePrefab == null)
            AddMissingPrefabs();

        string statsKey = "MvpSceneBuilder.StatsApplied.v1." + Application.dataPath;
        if (!EditorPrefs.GetBool(statsKey, false))
        {
            ApplyEnemyStats();
            EditorPrefs.SetBool(statsKey, true);
        }
    }

    [MenuItem("Tools/MVP/Apply Enemy Stats To Prefabs")]
    public static void ApplyEnemyStats()
    {
        try
        {
            var values = new (string name, int hp)[] { ("Grunt", 10), ("Elite", 20), ("Flyer", 10), ("Shooter", 20), ("Boss", 150) };
            foreach (var (name, hp) in values)
            {
                string path = $"{PrefabDir}/{name}.prefab";
                if (!File.Exists(path)) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var enemy = root.GetComponent<Enemy>();
                    if (enemy == null) continue;
                    enemy.stats = new Stats { maxHp = hp, attack = 10 };
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[MVP] Enemy stats applied.");
        }
        catch (Exception e)
        {
            Debug.LogError("[MVP] Applying enemy stats failed: " + e);
        }
    }

    [MenuItem("Tools/MVP/Add Missing Prefabs")]
    public static void AddMissingPrefabs()
    {
        try
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            var square = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShapeDir}/Square.png");
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShapeDir}/Circle.png");
            var diamond = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShapeDir}/Diamond.png");
            if (gm == null || square == null || circle == null || diamond == null) return;

            EnsureFolder(PrefabDir);
            var mat = GetNoFrictionMaterial();
            if (gm.gruntPrefab == null) gm.gruntPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Grunt.prefab");
            if (gm.elitePrefab == null) gm.elitePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Elite.prefab");
            if (gm.bossPrefab == null) gm.bossPrefab = BuildBossPrefab(square, mat);
            if (gm.flyerPrefab == null) gm.flyerPrefab = BuildFlyerPrefab(diamond, mat);
            if (gm.shooterPrefab == null) gm.shooterPrefab = BuildShooterPrefab(square, BuildBulletPrefab(circle), mat);

            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MVP] Missing prefabs added.");
        }
        catch (Exception e)
        {
            Debug.LogError("[MVP] Adding missing prefabs failed: " + e);
        }
    }

    [MenuItem("Tools/MVP/Build Scene")]
    public static void Build()
    {
        try
        {
            BuildInternal();
            Debug.Log("[MVP] Scene built. Press Play.");
        }
        catch (Exception e)
        {
            Debug.LogError("[MVP] Scene build failed: " + e);
        }
    }

    static void BuildInternal()
    {
        EnsureFolder(ArtDir);
        EnsureFolder(ShapeDir);
        EnsureFolder(PrefabDir);

        var square = CreateSpriteAsset("Square", 4, (x, y) => 1f);
        var circle = CreateSpriteAsset("Circle", 64, (x, y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 32f));
        var diamond = CreateSpriteAsset("Diamond", 64, (x, y) => Mathf.Clamp01((1f - (Mathf.Abs(x) + Mathf.Abs(y))) * 32f));
        var noFriction = GetNoFrictionMaterial();

        var weaponPrefab = BuildWeaponPrefab(diamond);
        var gruntPrefab = BuildEnemyPrefab<Enemy>("Grunt", circle, false, new Color(0.9f, 0.25f, 0.25f), 0.8f, 10, 2.8f, noFriction);
        var elitePrefab = BuildEnemyPrefab<Enemy>("Elite", square, true, new Color(0.6f, 0.25f, 0.85f), 1.2f, 20, 2.0f, noFriction);
        var bossPrefab = BuildBossPrefab(square, noFriction);
        var flyerPrefab = BuildFlyerPrefab(diamond, noFriction);
        var shooterPrefab = BuildShooterPrefab(square, BuildBulletPrefab(circle), noFriction);

        var scene = SceneManager.GetActiveScene();
        foreach (var name in new[] { "GameManager", "Level", "Player", "Enemies" })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.weaponPrefab = weaponPrefab;
        gm.bossPrefab = bossPrefab;
        gm.flyerPrefab = flyerPrefab;
        gm.shooterPrefab = shooterPrefab;
        gm.gruntPrefab = gruntPrefab;
        gm.elitePrefab = elitePrefab;

        BuildLevel(square);
        var player = BuildPlayer(square, noFriction);
        BuildEnemies(gruntPrefab, elitePrefab);
        SetupCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = player.gameObject;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Sprite CreateSpriteAsset(string name, int size, Func<float, float, float> alphaAt)
    {
        string path = $"{ShapeDir}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alphaAt(nx, ny) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static PhysicsMaterial2D GetNoFrictionMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (mat != null) return mat;
        mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }

    static GameObject MakeSprite(string name, Sprite sprite, Color color, Vector2 size, int order, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static GameObject SavePrefab(GameObject go, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject BuildWeaponPrefab(Sprite diamond)
    {
        var go = MakeSprite("Weapon", diamond, new Color(1f, 0.9f, 0.3f), new Vector2(0.5f, 0.5f), 2, null);
        go.AddComponent<Weapon>();
        return SavePrefab(go, "Weapon");
    }

    static GameObject BuildBossPrefab(Sprite square, PhysicsMaterial2D mat) =>
        BuildEnemyPrefab<Boss>("Boss", square, true, new Color(0.85f, 0.45f, 0.15f), 2.2f, 150, 1.8f, mat);

    static GameObject BuildFlyerPrefab(Sprite diamond, PhysicsMaterial2D mat) =>
        BuildEnemyPrefab<Flyer>("Flyer", diamond, false, new Color(0.2f, 0.8f, 0.8f), 0.8f, 10, 3.2f, mat);

    static GameObject BuildShooterPrefab(Sprite square, GameObject bullet, PhysicsMaterial2D mat) =>
        BuildEnemyPrefab<Shooter>("Shooter", square, true, new Color(0.3f, 0.85f, 0.4f), 1f, 20, 2f, mat, s => s.bulletPrefab = bullet);

    static GameObject BuildBulletPrefab(Sprite circle)
    {
        var go = MakeSprite("EnemyBullet", circle, new Color(1f, 0.5f, 0.1f), new Vector2(0.3f, 0.3f), 6, null);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var c = go.AddComponent<CircleCollider2D>();
        c.radius = 0.5f;
        c.isTrigger = true;

        go.AddComponent<EnemyBullet>();
        return SavePrefab(go, "EnemyBullet");
    }

    static GameObject BuildEnemyPrefab<T>(string name, Sprite sprite, bool box, Color color, float size, int hp, float speed, PhysicsMaterial2D mat, Action<T> setup = null) where T : Enemy
    {
        var go = MakeSprite(name, sprite, color, new Vector2(size, size), 3, null);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.mass = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (box)
        {
            var c = go.AddComponent<BoxCollider2D>();
            c.size = Vector2.one;
            c.sharedMaterial = mat;
        }
        else
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = 0.5f;
            c.sharedMaterial = mat;
        }

        var enemy = go.AddComponent<T>();
        enemy.stats = new Stats { maxHp = hp, attack = 10 };
        enemy.speed = speed;
        setup?.Invoke(enemy);
        return SavePrefab(go, name);
    }

    static void BuildLevel(Sprite square)
    {
        var level = new GameObject("Level").transform;

        MakeSprite("Background", square, new Color(0.18f, 0.18f, 0.21f), new Vector2(HalfWidth * 2f, HalfHeight * 2f), -10, level);

        var wallColor = new Color(0.35f, 0.35f, 0.4f);
        AddWall("Ceiling", square, level, new Vector2(0f, HalfHeight + 0.5f), new Vector2(HalfWidth * 2f + 2f, 1f), wallColor);
        AddWall("Floor", square, level, new Vector2(0f, -HalfHeight - 0.5f), new Vector2(HalfWidth * 2f + 2f, 1f), wallColor);
        AddWall("LeftWall", square, level, new Vector2(-HalfWidth - 0.5f, 0f), new Vector2(1f, HalfHeight * 2f), wallColor);
        AddWall("RightWall", square, level, new Vector2(HalfWidth + 0.5f, 0f), new Vector2(1f, HalfHeight * 2f), wallColor);

        var platformColor = new Color(0.5f, 0.45f, 0.35f);
        AddPlatform("PlatformLeft", square, level, -5f, -3.6f, 4f, platformColor);
        AddPlatform("PlatformRight", square, level, 5f, -3.6f, 4f, platformColor);
        AddPlatform("PlatformTop", square, level, 0f, -1.2f, 4f, platformColor);
    }

    static void AddWall(string name, Sprite square, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = MakeSprite(name, square, color, size, -5, parent);
        go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = Vector2.one;
    }

    static void AddPlatform(string name, Sprite square, Transform parent, float x, float topY, float width, Color color)
    {
        var go = MakeSprite(name, square, color, new Vector2(width, 0.4f), -5, parent);
        go.transform.position = new Vector3(x, topY - 0.2f, 0f);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        box.usedByEffector = true;
        var effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.useSideFriction = false;
        effector.surfaceArc = 160f;
    }

    static Player BuildPlayer(Sprite square, PhysicsMaterial2D mat)
    {
        var go = new GameObject("Player");
        go.transform.position = new Vector3(0f, -HalfHeight + 0.55f, 0f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;

        var capsule = go.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(0.7f, 1f);
        capsule.sharedMaterial = mat;

        var player = go.AddComponent<Player>();
        player.bodyRenderer = MakeSprite("Sprite", square, new Color(0.3f, 0.6f, 1f), new Vector2(0.7f, 1f), 4, go.transform).GetComponent<SpriteRenderer>();
        player.aimMark = MakeSprite("Aim", square, Color.white, new Vector2(0.4f, 0.12f), 5, go.transform).transform;
        return player;
    }

    static void BuildEnemies(GameObject grunt, GameObject elite)
    {
        var root = new GameObject("Enemies").transform;
        float gruntY = -HalfHeight + 0.4f + 0.05f;
        float eliteY = -HalfHeight + 0.6f + 0.05f;

        foreach (float x in new[] { -9f, -7.5f, 7.5f, 9f })
            Place(grunt, root, new Vector2(x, gruntY));
        foreach (float x in new[] { -5.5f, 5.5f })
            Place(elite, root, new Vector2(x, eliteY));
    }

    static void Place(GameObject prefab, Transform parent, Vector2 pos)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
    }

    static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
    }
}
