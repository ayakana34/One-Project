#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only helper: drives the test character through its actions and saves screenshots
// to Library/HoodedDiag so the animations can be checked without looking at the editor window.
[RequireComponent(typeof(CharacterTestController))]
public class CharacterAutoShot : MonoBehaviour
{
    public const string PrefKey = "Hooded.AutoShot";

    IEnumerator Start()
    {
        if (!EditorPrefs.GetBool(PrefKey, false)) yield break;
        EditorPrefs.SetBool(PrefKey, false);

        var ctl = GetComponent<CharacterTestController>();
        var rb = GetComponent<Rigidbody2D>();
        var anim = ctl.animator;
        string dir = Path.GetFullPath("Library/HoodedDiag");
        Directory.CreateDirectory(dir);

        var camGo = new GameObject("ShotCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 2.2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
        var rt = new RenderTexture(512, 512, 24);
        var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);

        void Shot(string name)
        {
            camGo.transform.position = new Vector3(transform.position.x + 0.8f * (ctl.visual != null ? Mathf.Sign(Mathf.Sin(ctl.visual.eulerAngles.y * Mathf.Deg2Rad)) : 1f), transform.position.y + 1.0f, -10f);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, "play_" + name + ".png"), tex.EncodeToPNG());
        }

        // 1) Step through the Roll (base layer) and Throw (upper body layer) clips pose by pose.
        ctl.enabled = false;
        rb.simulated = false;
        if (ctl.visual != null) ctl.visual.localRotation = Quaternion.Euler(0f, 90f, 0f);
        anim.SetBool("Grounded", true);
        anim.SetBool("Run", false);
        foreach (var (state, layer, prefix) in new[] { ("Throw", 1, "throwstrip_") })
        {
            for (int i = 0; i < 12; i++)
            {
                anim.Play(state, layer, i / 11f);
                anim.Update(0f);
                yield return null;
                Shot(prefix + i.ToString("00"));
            }
            anim.Play(layer == 0 ? "Idle" : "Empty", layer, 0f);
            anim.Update(0f);
        }
        rb.simulated = true;
        ctl.enabled = true;

        // 2) Real play: throw, dash, run + throw.
        ctl.scriptedAim = Vector2.right;
        ctl.scriptedInput = 0f;
        yield return new WaitForSeconds(0.8f);
        Shot("act_idle");

        ctl.scriptedThrow = true;
        foreach (float wait in new[] { 0.1f, 0.12f, 0.12f, 0.15f })
        {
            yield return new WaitForSeconds(wait);
            Shot("act_throw_" + Time.frameCount);
        }
        Debug.Log("[Hooded] weapon in hand after throw: " + ctl.HasWeapon);
        yield return new WaitForSeconds(0.6f);
        ctl.PickUp();

        ctl.scriptedDash = true;
        foreach (float wait in new[] { 0.05f, 0.05f, 0.1f, 0.15f })
        {
            yield return new WaitForSeconds(wait);
            Shot("act_dash_" + Time.frameCount);
        }
        Debug.Log("[Hooded] after dash x=" + transform.position.x.ToString("F2") + " charges=" + ctl.DashCharges);

        yield return new WaitForSeconds(0.6f);
        ctl.scriptedAim = new Vector2(0.7f, 0.7f);
        ctl.scriptedDash = true;
        foreach (float wait in new[] { 0.05f, 0.08f, 0.2f })
        {
            yield return new WaitForSeconds(wait);
            Shot("act_dashup_" + Time.frameCount);
        }

        yield return new WaitForSeconds(1.2f);
        ctl.PickUp();
        ctl.scriptedAim = Vector2.right;
        ctl.scriptedInput = 1f;
        yield return new WaitForSeconds(0.4f);
        ctl.scriptedThrow = true;
        foreach (float wait in new[] { 0.12f, 0.15f, 0.2f })
        {
            yield return new WaitForSeconds(wait);
            Shot("act_runthrow_" + Time.frameCount);
        }

        // Walking backwards while aiming the other way (run cycle plays reversed).
        ctl.scriptedAim = Vector2.left;
        yield return new WaitForSeconds(0.3f);
        Shot("act_backrun");

        Debug.Log("[Hooded] Playmode shots done");
        Object.Destroy(rt);
        Object.Destroy(camGo);
        EditorApplication.isPlaying = false;
    }
}
#endif
