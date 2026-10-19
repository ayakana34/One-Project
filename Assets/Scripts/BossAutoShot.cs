#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only helper: puts the player into the boss arena, forces the boss attack order (rock, slam, charge,
// then enraged rocks) and saves screenshots to Library/HoodedDiag/boss2_*.png.
public class BossAutoShot : MonoBehaviour
{
    public const string PrefKey = "Boss.AutoShot";
    static readonly BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static void Call(string method, params object[] args)
    {
        var gm = GameManager.Instance;
        var m = typeof(GameManager).GetMethod(method, All);
        if (gm == null || m == null) Debug.LogWarning("[Boss] cannot call " + method);
        else m.Invoke(gm, args);
    }

    IEnumerator Start()
    {
        string dir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "boss2_*.png")) File.Delete(old);
        var cam = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var gm = GameManager.Instance;
        int n = 0;

        void Shot(string tag)
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, "boss2_" + (n++).ToString("000") + "_" + tag + ".png"), tex.EncodeToPNG());
        }

        IEnumerator Watch(float seconds, string tag)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                gm.Player.Heal(1f); // keep the player alive so the whole sequence plays
                Shot(tag);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            var boss = Boss.Current;
            if (boss != null)
                foreach (var e in Enemy.All)
                    if (e != null && e != boss) { Debug.Log("[Boss] boss vs " + e.name + " ignore collision: " + Physics2D.GetIgnoreCollision(boss.Col, e.Col)); break; }
            Debug.Log("[Boss] after " + tag + ": hp=" + gm.Player.Hp + " enemies=" + Enemy.All.Count + " bossPos=" + (boss != null ? boss.transform.position.ToString("F1") : "none"));
        }

        yield return new WaitForSecondsRealtime(1f);
        Call("StartGame");
        // wait for stage 1 to spawn, then jump to the boss stage and spawn the boss in the same frame
        // (a boss stage without any enemy would open the reward screen at once)
        yield return new WaitForSecondsRealtime(2.5f);
        Call("BeginStage", 2, false);
        Call("SpawnBoss");
        yield return null;
        gm.Player.Teleport(new Vector2(-4f, -5.45f));

        var boss0 = FindObjectsByType<Boss>(FindObjectsSortMode.None).FirstOrDefault();
        Debug.Log("[Boss] boss spawned: " + (boss0 != null) + " state=" + gm.State + " timeScale=" + Time.timeScale + " stage=" + typeof(GameManager).GetField("stage", All).GetValue(gm)
            + " spawnAt=" + typeof(GameManager).GetField("spawnAt", All).GetValue(gm) + " time=" + Time.time + " dead=" + gm.Player.Dead
            + (boss0 != null ? " bossEnabled=" + boss0.enabled + " dist=" + Vector2.Distance(gm.Player.transform.position, boss0.transform.position) : ""));
        if (boss0 == null) { EditorApplication.isPlaying = false; yield break; }
        boss0.testSequence = new[] { Boss.Attack.Rock, Boss.Attack.Slam, Boss.Attack.Charge };

        yield return Watch(7f, "rock");
        yield return Watch(5f, "slam");
        yield return Watch(4f, "charge");

        // enraged: two rocks at once
        boss0.forceEnraged = true;
        boss0.testSequence = new[] { Boss.Attack.Rock };
        yield return Watch(7f, "enraged");

        Debug.Log("[Boss] shots done");
        Destroy(rt);
        Destroy(tex);
        EditorApplication.isPlaying = false;
    }
}
#endif
