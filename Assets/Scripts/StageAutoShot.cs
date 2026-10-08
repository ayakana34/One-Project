#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only helper: plays through stage 1 -> 2 -> boss -> stage 1 (round 2) and saves screenshots
// to Library/HoodedDiag/stage_*.png so the stage flow can be checked without watching the editor.
public class StageAutoShot : MonoBehaviour
{
    public const string PrefKey = "Stages.AutoShot";

    static readonly BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static object Invoke(string method, params object[] args)
    {
        var gm = GameManager.Instance;
        var m = typeof(GameManager).GetMethod(method, All);
        if (gm == null || m == null)
        {
            Debug.LogWarning("[Stages] cannot call " + method);
            return null;
        }
        return m.Invoke(gm, args);
    }

    static int Field(string name)
    {
        var f = typeof(GameManager).GetField(name, All);
        return f == null ? -1 : (int)f.GetValue(GameManager.Instance);
    }

    IEnumerator Start()
    {
        string dir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var gm = GameManager.Instance;

        void Shot(string name)
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, "stage_" + name + ".png"), tex.EncodeToPNG());
        }

        void Report(string label)
        {
            int active = FindObjectsByType<PlatformEffector2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            var exit = GameObject.Find("Exit");
            Debug.Log("[Stages] " + label + ": stage=" + Field("stage") + " round=" + gm.Round + " state=" + gm.State
                + " enemies=" + Enemy.All.Count + " platforms=" + active + " player=" + gm.Player.transform.position.ToString("F1")
                + " cam.x=" + cam.transform.position.x.ToString("F1") + " exit=" + (exit != null && exit.activeSelf ? exit.transform.position.x.ToString("F1") : "off")
                + " types=" + string.Join(",", Enemy.All.Select(e => e.name.Replace("(Clone)", "")).GroupBy(n => n).Select(g => g.Key + "x" + g.Count())));
        }

        void GoTo(float x) => gm.Player.Teleport(new Vector2(x, -5.45f));

        IEnumerator LeaveByExit(string shotName)
        {
            var exit = GameObject.Find("Exit");
            GoTo(exit.transform.position.x - 0.2f);
            yield return new WaitForSecondsRealtime(0.4f);
            Report("at exit " + shotName);
            Shot(shotName);
            Invoke("ChooseReward", 0);
        }

        yield return new WaitForSecondsRealtime(1f);
        Invoke("StartGame");

        // Stage 1
        yield return new WaitForSecondsRealtime(1.0f);
        Report("stage 1 start");
        Shot("1_start");
        GoTo(0f);
        yield return new WaitForSecondsRealtime(1.2f);
        Report("stage 1 middle");
        Shot("1_mid");
        yield return LeaveByExit("reward_after_1");

        // Stage 2
        yield return new WaitForSecondsRealtime(0.4f);
        Report("stage 2 loaded");
        Shot("2_banner");
        yield return new WaitForSecondsRealtime(1.5f);
        Report("stage 2 enemies");
        Shot("2_start");
        GoTo(2f);
        yield return new WaitForSecondsRealtime(1.2f);
        Shot("2_mid");
        yield return LeaveByExit("reward_after_2");

        // Boss stage
        yield return new WaitForSecondsRealtime(2.2f);
        Report("boss stage");
        Shot("3_boss");
        foreach (var boss in FindObjectsByType<Boss>(FindObjectsSortMode.None)) boss.TakeHit(100000);
        yield return new WaitForSecondsRealtime(0.5f);
        Report("boss dead");
        Shot("reward_after_boss");
        Invoke("ChooseReward", 0);

        // Back to stage 1 as round 2
        yield return new WaitForSecondsRealtime(2.2f);
        Report("round 2");
        Shot("1_round2");

        Debug.Log("[Stages] shots done");
        Destroy(rt);
        Destroy(tex);
        EditorApplication.isPlaying = false;
    }
}
#endif
