using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opens SampleScene and plays it once with StageAutoShot so the stage flow gets checked and captured.
[InitializeOnLoad]
public static class StageTestLauncher
{
    static string RunKey => "StageTestLauncher.Run.v1." + Application.dataPath;

    static StageTestLauncher()
    {
        EditorApplication.delayCall += AutoRun;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.GetBool(StageAutoShot.PrefKey, false)) return;
        EditorPrefs.SetBool(StageAutoShot.PrefKey, false);
        new GameObject("StageAutoShot").AddComponent<StageAutoShot>();
    }

    static void AutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (EditorPrefs.GetBool(RunKey, false)) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorPrefs.SetBool(RunKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorPrefs.SetBool(StageAutoShot.PrefKey, true);
        EditorApplication.EnterPlaymode();
    }
}
