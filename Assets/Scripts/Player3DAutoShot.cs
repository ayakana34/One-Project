#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only helper: drives the real Player with scripted input (idle, run, jump, dash, throw) and saves
// close-up screenshots to Library/HoodedDiag/p3d_*.png so the 3D player can be checked without watching the editor.
public class Player3DAutoShot : MonoBehaviour
{
    public const string PrefKey = "Player3D.AutoShot";

    static void Call(string method)
    {
        var gm = GameManager.Instance;
        var m = typeof(GameManager).GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (gm == null || m == null) Debug.LogWarning("[Player3D] cannot call " + method);
        else m.Invoke(gm, null);
    }

    IEnumerator Start()
    {
        string dir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        var gm = GameManager.Instance;
        var player = gm.Player;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);

        var follow = cam.GetComponent<CameraFollow>();

        void Shot(string name)
        {
            float oldSize = cam.orthographicSize;
            Vector3 oldPos = cam.transform.position;
            cam.orthographicSize = 3.0f; // close-up
            if (follow != null) follow.SnapNow();
            // the camera only follows sideways, so lift it to the player's height for the close-up
            var cp = cam.transform.position; cp.y = player.transform.position.y + 0.9f; cam.transform.position = cp;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            // close-up around the player
            // viewport coordinates, because the picture is rendered into a 1280x720 texture, not to the screen
            Vector3 vp = cam.WorldToViewportPoint(player.transform.position + Vector3.up * 0.3f);
            Vector3 sp = new Vector3(vp.x * 1280f, vp.y * 720f, 0f);
            int w = 360, h = 270;
            int x = Mathf.Clamp((int)sp.x - w / 2, 0, 1280 - w);
            int y = Mathf.Clamp((int)sp.y - h / 2, 0, 720 - h);
            var crop = new Texture2D(w, h, TextureFormat.RGB24, false);
            crop.SetPixels(tex.GetPixels(x, y, w, h));
            crop.Apply();
            File.WriteAllBytes(Path.Combine(dir, "p3d_" + name + ".png"), crop.EncodeToPNG());
            Destroy(crop);
            cam.orthographicSize = oldSize;
            cam.transform.position = oldPos;
            if (follow != null) follow.SnapNow();
        }

        yield return new WaitForSecondsRealtime(1f);
        foreach (var e in Enemy.All.ToArray()) if (e != null) Destroy(e.gameObject); // keep the scene quiet
        Call("StartGame");
        player.Teleport(new Vector2(-3f, -5.45f));
        player.testMoveX = 0f;
        player.testAim = Vector2.right;
        yield return new WaitForSecondsRealtime(0.8f);
        Debug.Log("[Player3D] visual built: " + (player.GetComponentInChildren<Player3DVisual>() != null) + ", square hidden: " + (player.bodyRenderer != null && !player.bodyRenderer.enabled));
        var rends = player.GetComponentsInChildren<Renderer>();
        int on = 0; foreach (var r in rends) if (r.enabled && r.gameObject.activeInHierarchy) on++;
        var vis = player.GetComponentInChildren<Player3DVisual>();
        Debug.Log("[Player3D] renderers enabled " + on + "/" + rends.Length + ", visual pos " + (vis != null ? vis.transform.position.ToString("F2") : "none") + ", player " + player.transform.position.ToString("F2"));
        Shot("idle");
        var daggerAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dagger.prefab");
        if (daggerAsset != null)
        {
            var d = Instantiate(daggerAsset, player.transform.position + new Vector3(0.9f, 0.9f, -0.5f), Quaternion.Euler(0f, 0f, 25f));
            Debug.Log("[Dagger] guid->path " + UnityEditor.AssetDatabase.GUIDToAssetPath("64238fa1304b14c41b2e6cfb360de48e")
                + " | load mat " + (UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Dagger/Dagger_Blade.mat") != null)
                + " | load mesh " + (UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Dagger/DaggerBlade.mesh") != null)
                + " | all at mat path " + string.Join(",", System.Array.ConvertAll(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Dagger/Dagger_Blade.mat"), o => o == null ? "null" : o.GetType().Name + ":" + o.name)));
            foreach (var r in d.GetComponentsInChildren<Renderer>())
            {
                var m = r.sharedMaterial;
                Debug.Log("[Dagger] " + r.name + " mat=" + (m != null ? m.name + " shader=" + (m.shader != null ? m.shader.name + " supported=" + m.shader.isSupported : "null") : "null")
                    + " bounds=" + r.bounds.ToString("F2"));
            }
            Shot("dagger_closeup");
            Destroy(d);

            var weaponAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapon.prefab");
            if (weaponAsset != null)
            {
                var wgo = Instantiate(weaponAsset, player.transform.position + new Vector3(1.0f, 0.6f, 0f), Quaternion.Euler(0f, 0f, 30f));
                foreach (var c in wgo.GetComponents<Behaviour>()) if (c is Weapon) c.enabled = false;
                foreach (var rb in wgo.GetComponents<Rigidbody2D>()) rb.simulated = false;
                foreach (var r in wgo.GetComponentsInChildren<Renderer>())
                    Debug.Log("[Dagger] weapon renderer " + r.name + " enabled=" + r.enabled + " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "null") + " bounds=" + r.bounds.extents.ToString("F2"));
                Shot("weapon_prefab");
                Destroy(wgo);
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("idle2");

        // run right
        player.testMoveX = 1f;
        yield return new WaitForSecondsRealtime(0.6f);
        Shot("run_a");
        yield return new WaitForSecondsRealtime(0.15f);
        Shot("run_b");

        // jump while running
        player.testJump = true;
        yield return new WaitForSecondsRealtime(0.15f);
        Shot("jump_a");
        yield return new WaitForSecondsRealtime(0.25f);
        Shot("jump_b");
        yield return new WaitForSecondsRealtime(0.25f);
        Shot("jump_c");
        yield return new WaitForSecondsRealtime(0.6f);

        // dash right, then up-right
        player.testMoveX = 0f;
        yield return new WaitForSecondsRealtime(0.3f);
        player.testDash = true;
        yield return new WaitForSecondsRealtime(0.06f);
        Shot("dash_a");
        yield return new WaitForSecondsRealtime(0.07f);
        Shot("dash_b");
        yield return new WaitForSecondsRealtime(1.0f);
        player.testAim = new Vector2(0.7f, 0.7f);
        player.testDash = true;
        yield return new WaitForSecondsRealtime(0.08f);
        Shot("dashup");
        yield return new WaitForSecondsRealtime(0.8f);

        // throw to the right, standing
        player.testAim = Vector2.right;
        player.testThrow = true;
        foreach (float wait in new[] { 0.04f, 0.05f, 0.06f, 0.1f })
        {
            yield return new WaitForSecondsRealtime(wait);
            Shot("throw_" + Time.frameCount);
        }
        Debug.Log("[Player3D] weapon in hand after throw: " + player.HasWeapon + ", thrown weapon exists: " + (Weapon.Current != null));
        yield return new WaitForSecondsRealtime(0.4f);

        // aim left while walking right (the run cycle plays backwards)
        if (Weapon.Current != null) Destroy(Weapon.Current.gameObject);
        player.PickUp();
        player.testAim = Vector2.left;
        player.testMoveX = 1f;
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("backrun");
        player.testMoveX = -1f;
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("run_left");

        Debug.Log("[Player3D] shots done");
        Destroy(rt);
        Destroy(tex);
        EditorApplication.isPlaying = false;
    }
}
#endif
