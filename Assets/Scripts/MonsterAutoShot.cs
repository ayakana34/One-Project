#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only helper: plays the real game for a few seconds, spawns the boss and saves screenshots
// to Library/HoodedDiag/mon_*.png so the monster sprites can be checked without watching the editor.
public class MonsterAutoShot : MonoBehaviour
{
    public const string PrefKey = "Monsters.AutoShot";

    static void Call(string method)
    {
        var gm = GameManager.Instance;
        var m = typeof(GameManager).GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (gm == null || m == null) Debug.LogWarning("[Monsters] cannot call " + method);
        else m.Invoke(gm, null);
    }

    IEnumerator Start()
    {
        string dir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);

        void Shot(string name)
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, "mon_" + name + ".png"), tex.EncodeToPNG());
        }

        yield return new WaitForSecondsRealtime(1f);
        Call("StartGame");
        yield return new WaitForSeconds(0.6f);
        Shot("wave_a");
        Debug.Log("[Monsters] enemies in the first wave: " + string.Join(", ", Enemy.All.Select(e => e.name.Replace("(Clone)", "")).GroupBy(n => n).Select(g => g.Key + " x" + g.Count())));
        yield return new WaitForSeconds(1.2f);
        Shot("wave_b");

        // Hide (not destroy) the first wave: destroying all enemies would end the wave and open the reward screen.
        foreach (var e in Enemy.All.ToArray()) if (e != null) e.gameObject.SetActive(false);
        yield return null;
        Call("SpawnBoss");
        for (int i = 0; i < 20; i++)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            Shot("boss_" + i.ToString("00"));
        }

        // Flyer and shooter do not appear in the first wave, so spawn them directly.
        foreach (var b in FindObjectsByType<Boss>(FindObjectsSortMode.None)) b.gameObject.SetActive(false);
        var gm = GameManager.Instance;
        Instantiate(gm.flyerPrefab, new Vector3(-5f, 0.5f, 0f), Quaternion.identity);
        Instantiate(gm.shooterPrefab, new Vector3(5f, 1.5f, 0f), Quaternion.identity);
        yield return new WaitForSecondsRealtime(0.7f);
        Shot("air_a");
        yield return new WaitForSecondsRealtime(1.0f);
        Shot("air_b");

        Debug.Log("[Monsters] shots done");
        Destroy(rt);
        Destroy(tex);
        EditorApplication.isPlaying = false;
    }
}
#endif
