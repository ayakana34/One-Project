using UnityEngine;

// Draws the player as the 3D hooded character and drives its animation from the Player's state.
// The gameplay (collider, movement, throw, dash) stays in Player; this only changes what is drawn.
public class Player3DVisual : MonoBehaviour
{
    const float DashLean = 12f;

    public Animator animator;
    public Transform handWeapon;
    [Tooltip("Seconds between the click and the weapon leaving the hand; matches the throw animation.")]
    public float throwReleaseDelay = 0.1f;

    Player player;
    Renderer[] renderers;
    float facing = 1f;

    public void Bind(Player p)
    {
        player = p;
        renderers = GetComponentsInChildren<Renderer>(true);
        player.Threw += OnThrew;
        player.ThrowReleaseDelay = throwReleaseDelay;
        EnsureLight();
    }

    void OnDestroy()
    {
        if (player != null) player.Threw -= OnThrew;
    }

    void OnThrew()
    {
        if (animator != null) animator.SetTrigger("Throw");
    }

    // The sprites are unlit, but the 3D model needs a light.
    static void EnsureLight()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return;

        var go = new GameObject("Directional Light");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        go.transform.rotation = Quaternion.Euler(35f, 15f, 0f);
    }

    void LateUpdate()
    {
        if (player == null) return;

        // The character faces the aim direction, not the walking direction.
        if (player.Dashing && Mathf.Abs(player.DashDir.x) > 0.2f) facing = Mathf.Sign(player.DashDir.x);
        else if (Mathf.Abs(player.Aim.x) > 0.1f) facing = Mathf.Sign(player.Aim.x);

        // While dashing the body leans into the dash direction (less for upward, more for downward dashes).
        float lean = 0f;
        if (player.Dashing)
        {
            var dir = player.DashDir;
            float pitch = Mathf.Atan2(dir.y, Mathf.Max(0.05f, Mathf.Abs(dir.x))) * Mathf.Rad2Deg;
            lean = DashLean - 0.6f * Mathf.Clamp(pitch, -90f, 90f);
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, -lean * facing) * Quaternion.Euler(0f, facing * 90f, 0f);

        if (animator != null)
        {
            float moveX = player.MoveX;
            bool moving = Mathf.Abs(moveX) > 0.01f;
            animator.SetBool("Run", moving);
            animator.SetBool("Grounded", player.IsGrounded());
            animator.SetBool("Dashing", player.Dashing);
            // Walking away from the aim direction plays the run cycle backwards.
            animator.SetFloat("RunSpeed", moving && Mathf.Sign(moveX) != facing ? -1f : 1f);
        }

        if (handWeapon != null) handWeapon.gameObject.SetActive(player.HasWeapon);

        // Hit blink.
        bool hidden = player.HitInvulnerable && (int)(Time.time * 20f) % 2 == 0;
        foreach (var r in renderers) if (r != null) r.enabled = !hidden;
    }
}
