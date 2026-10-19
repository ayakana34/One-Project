using System.IO;
using UnityEditor;
using UnityEngine;

// Imports the golem and bullet sprites as pixel art, builds the small Golem prefab (from the Grunt prefab),
// gives it to the boss and puts the new sprite on the enemy bullet.
[InitializeOnLoad]
public static class GolemSetup
{
    const string SpriteDir = "Assets/Art/Monsters";
    const string GruntPath = "Assets/Prefabs/Grunt.prefab";
    const string GolemPath = "Assets/Prefabs/Golem.prefab";
    const string BossPath = "Assets/Prefabs/Boss.prefab";
    const string BulletPath = "Assets/Prefabs/EnemyBullet.prefab";

    static string AutoKey => "GolemSetup.Auto.v1." + Application.dataPath;

    static GolemSetup()
    {
        EditorApplication.delayCall += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorPrefs.GetBool(AutoKey, false)) return;
        if (!File.Exists(SpriteDir + "/Golem.png") || !File.Exists(SpriteDir + "/Bullet.png")) return;
        EditorPrefs.SetBool(AutoKey, true);
        Apply();
    }

    [MenuItem("Tools/MVP/Apply Golem And Bullet")]
    public static void Apply()
    {
        try
        {
            ConfigureTexture(SpriteDir + "/Golem.png");
            ConfigureTexture(SpriteDir + "/Bullet.png");
            var golemSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Golem.png");
            var bulletSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "/Bullet.png");

            var golem = BuildGolemPrefab(golemSprite);
            AssignToBoss(golem);
            ApplyToBullet(bulletSprite);
            AssetDatabase.SaveAssets();
            Debug.Log("[Golem] Golem prefab built (sprite " + (golemSprite != null) + "), given to the boss; bullet sprite " + (bulletSprite != null) + ".");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Golem] Apply failed: " + e);
        }
    }

    static void ConfigureTexture(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = tex.width; // the square canvas is exactly one world unit
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static GameObject BuildGolemPrefab(Sprite sprite)
    {
        // Edit the prefab in place when it exists so references to it stay valid.
        if (!File.Exists(GolemPath)) AssetDatabase.CopyAsset(GruntPath, GolemPath);

        var root = PrefabUtility.LoadPrefabContents(GolemPath);
        try
        {
            root.name = "Golem";
            root.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            var enemy = root.GetComponent<Enemy>();
            enemy.idleSprite = sprite;
            enemy.visualScale = 1f;
            enemy.speed = 2.2f;
            enemy.stats = new Stats { maxHp = 10, attack = 10 };
            PrefabUtility.SaveAsPrefabAsset(root, GolemPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(GolemPath);
    }

    static void AssignToBoss(GameObject golem)
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            root.GetComponent<Boss>().golemPrefab = golem;
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyToBullet(Sprite sprite)
    {
        var root = PrefabUtility.LoadPrefabContents(BulletPath);
        try
        {
            var sr = root.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white; // the sprite has its own colours
            PrefabUtility.SaveAsPrefabAsset(root, BulletPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
