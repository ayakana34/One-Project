using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opens SampleScene and plays it once with BossAutoShot so the new boss attacks get captured.
[InitializeOnLoad]
public static class BossTestLauncher
{
    static string RunKey => "BossTestLauncher.Run.v7." + Application.dataPath;

    static BossTestLauncher()
    {
        EditorApplication.delayCall += AutoRun;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(BossAutoShot.PrefKey, false)) return;
        EditorPrefs.SetBool(BossAutoShot.PrefKey, false);
        new GameObject("BossAutoShot").AddComponent<BossAutoShot>();
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorPrefs.GetBool(RunKey, false)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorPrefs.SetBool(RunKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorPrefs.SetBool(BossAutoShot.PrefKey, true);
        EditorApplication.EnterPlaymode();
    }
}
