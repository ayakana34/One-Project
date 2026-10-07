using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Imports the monster sprites as pixel art (point filter, 1 canvas = 1 world unit) and assigns them to the enemy prefabs.
[InitializeOnLoad]
public static class MonsterSpriteApplier
{
    const string SpriteDir = "Assets/Art/Monsters";
    const string PrefabDir = "Assets/Prefabs";

    static string AutoKey => "MonsterSpriteApplier.Auto.v1." + Application.dataPath;

    static MonsterSpriteApplier()
    {
        EditorApplication.delayCall += AutoRun;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(MonsterAutoShot.PrefKey, false)) return;
        EditorPrefs.SetBool(MonsterAutoShot.PrefKey, false);
        new GameObject("MonsterAutoShot").AddComponent<MonsterAutoShot>();
    }

    static string ShotKey => "MonsterSpriteApplier.Shot.v4." + Application.dataPath;

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists(SpriteDir + "/Grunt.png")) return;

        if (!EditorPrefs.GetBool(AutoKey, false))
        {
            EditorPrefs.SetBool(AutoKey, true);
            Apply();
        }

        // Play the real game once and save screenshots so the result can be checked.
        if (!EditorPrefs.GetBool(ShotKey, false) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorPrefs.SetBool(ShotKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorPrefs.SetBool(MonsterAutoShot.PrefKey, true);
            EditorApplication.EnterPlaymode();
        }
    }

    [MenuItem("Tools/MVP/Apply Monster Sprites")]
    public static void Apply()
    {
        try
        {
            foreach (var file in Directory.GetFiles(SpriteDir, "*.png")) ConfigureTexture(file.Replace('\\', '/'));
            AssetDatabase.SaveAssets();

            // prefab, idle, wind-up, charge, size of the drawing relative to the collider
            var table = new (string prefab, string idle, string windup, string charge, float scale)[]
            {
                ("Grunt", "Grunt", null, null, 1f),
                ("Elite", "Elite", null, null, 1f),
                ("Flyer", "Flyer", null, null, 1.9f),
                ("Shooter", "Shooter", null, null, 1.1f),
                ("Boss", "Boss_Idle", "Boss_Windup", "Boss_Charge", 1f),
            };
            foreach (var row in table) ApplyToPrefab(row.prefab, row.idle, row.windup, row.charge, row.scale);
            Debug.Log("[Monsters] Sprites applied to the enemy prefabs.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Monsters] Apply failed: " + e);
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

    static Sprite LoadSprite(string name) =>
        name == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");

    static void ApplyToPrefab(string prefabName, string idle, string windup, string charge, float scale)
    {
        string path = $"{PrefabDir}/{prefabName}.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var enemy = root.GetComponent<Enemy>();
            enemy.idleSprite = LoadSprite(idle);
            enemy.visualScale = scale;
            if (enemy is Boss boss)
            {
                boss.windupSprite = LoadSprite(windup);
                boss.chargeSprite = LoadSprite(charge);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("[Monsters] " + prefabName + ": idle=" + (enemy.idleSprite != null) + (enemy is Boss b ? ", windup=" + (b.windupSprite != null) + ", charge=" + (b.chargeSprite != null) : ""));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
