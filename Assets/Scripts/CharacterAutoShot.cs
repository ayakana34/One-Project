#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper: drives the test character through idle/run/jump/turn and saves screenshots
// to Library/HoodedDiag so the animation can be checked without looking at the editor window.
[RequireComponent(typeof(CharacterTestController))]
public class CharacterAutoShot : MonoBehaviour
{
    public const string PrefKey = "Hooded.AutoShot";

    IEnumerator Start()
    {
        if (!EditorPrefs.GetBool(PrefKey, false)) yield break;
        EditorPrefs.SetBool(PrefKey, false);

        var ctl = GetComponent<CharacterTestController>();
        string dir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(dir);

        var camGo = new GameObject("ShotCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
        var rt = new RenderTexture(512, 512, 24);
        var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);

        void Shot(string name)
        {
            camGo.transform.position = new Vector3(transform.position.x, transform.position.y + 0.9f, -10f);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, "play_" + name + ".png"), tex.EncodeToPNG());
            Debug.Log("[Hooded] shot " + name + " at " + transform.position + ", animator state hash " + ctl.animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
        }

        ctl.scriptedInput = 0f;
        yield return new WaitForSeconds(1.0f);
        Shot("idle");

        ctl.scriptedInput = 1f;
        yield return new WaitForSeconds(0.6f);
        Shot("run_right_a");
        yield return new WaitForSeconds(0.2f);
        Shot("run_right_b");

        ctl.scriptedInput = -1f;
        yield return new WaitForSeconds(0.5f);
        Shot("run_left");

        ctl.scriptedInput = 0f;
        yield return new WaitForSeconds(0.5f);
        ctl.scriptedJump = true;
        yield return new WaitForSeconds(0.3f);
        Shot("jump");

        yield return new WaitForSeconds(1.0f);
        Debug.Log("[Hooded] Playmode shots done");
        Object.Destroy(rt);
        Object.Destroy(camGo);
        EditorApplication.isPlaying = false;
    }
}
#endif
