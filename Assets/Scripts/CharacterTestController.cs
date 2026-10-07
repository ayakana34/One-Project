using UnityEngine;
using UnityEngine.InputSystem;

// Test-only controller for previewing a 3D character in a 2D side-view scene.
// Mirrors the controls of the real Player: A/D move, Space jump, mouse aim, left click throw, Shift/right click dash.
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class CharacterTestController : MonoBehaviour
{
    const float GravityScale = 3f;
    const float DashSpeed = 22f;
    const float DashTime = 0.15f;
    const float DashRechargeTime = 1.5f;
    const int MaxDashCharges = 2;
    const float ChestHeight = 0.9f;
    const float DashLean = 12f;

    public Transform visual;
    public Animator animator;
    public Transform weaponInHand;
    public Material weaponMaterial;
    public float moveSpeed = 7f;
    public float jumpSpeed = 13f;
    public float throwReleaseDelay = 0.16f;

    public bool HasWeapon { get; private set; } = true;
    public int DashCharges { get; private set; } = MaxDashCharges;
    public bool Dashing => Time.time < dashUntil;
    public Vector2 ChestPosition => (Vector2)transform.position + Vector2.up * ChestHeight;

    // Optional scripted input (used by the automatic screenshot check).
    public float? scriptedInput;
    public Vector2? scriptedAim;
    public bool scriptedJump, scriptedThrow, scriptedDash;

    Rigidbody2D body;
    CapsuleCollider2D capsule;
    float facing = 1f;
    float dashUntil, rechargeAt, pendingThrowAt = -1f;
    Vector2 dashDir = Vector2.right, pendingAim = Vector2.right;
    bool grounded, wasDashing;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        body.gravityScale = GravityScale;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        float input = scriptedInput ?? (kb == null ? 0f : (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f));
        Vector2 aim = scriptedAim ?? MouseAim(mouse);
        bool jumpPressed = scriptedJump || (kb != null && kb.spaceKey.wasPressedThisFrame);
        bool throwPressed = scriptedThrow || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        bool dashPressed = scriptedDash || (kb != null && kb.leftShiftKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame);
        scriptedJump = scriptedThrow = scriptedDash = false;
        grounded = IsGrounded();

        if (DashCharges < MaxDashCharges && Time.time >= rechargeAt)
        {
            DashCharges++;
            if (DashCharges < MaxDashCharges) rechargeAt = Time.time + DashRechargeTime;
        }

        if (dashPressed && !Dashing && DashCharges > 0)
        {
            if (DashCharges == MaxDashCharges) rechargeAt = Time.time + DashRechargeTime;
            DashCharges--;
            dashDir = aim;
            dashUntil = Time.time + DashTime;
        }

        if (throwPressed && HasWeapon && pendingThrowAt < 0f)
        {
            pendingThrowAt = Time.time + throwReleaseDelay;
            pendingAim = aim;
            if (animator != null) animator.SetTrigger("Throw");
        }
        if (pendingThrowAt >= 0f && Time.time >= pendingThrowAt)
        {
            pendingThrowAt = -1f;
            HasWeapon = false;
            // The weapon leaves from the hand (the blade held in the hand), not from the chest.
            Vector2 spawn = weaponInHand != null ? (Vector2)weaponInHand.position : ChestPosition + pendingAim * 0.6f;
            if (weaponInHand != null) weaponInHand.gameObject.SetActive(false);
            TestThrownWeapon.Spawn(this, spawn, pendingAim);
        }

        if (Dashing)
        {
            body.gravityScale = 0f;
            body.linearVelocity = dashDir * DashSpeed;
            wasDashing = true;
        }
        else
        {
            body.gravityScale = GravityScale;
            var v = body.linearVelocity;
            if (wasDashing)
            {
                v = Vector2.zero;
                wasDashing = false;
            }
            v.x = input * moveSpeed;
            if (grounded && jumpPressed) v.y = jumpSpeed;
            body.linearVelocity = v;
        }

        // The character faces the aim direction (like the mouse-aimed weapon), not the walking direction.
        if (Dashing && Mathf.Abs(dashDir.x) > 0.2f) facing = Mathf.Sign(dashDir.x);
        else if (Mathf.Abs(aim.x) > 0.1f) facing = Mathf.Sign(aim.x);
        if (visual != null)
        {
            // While dashing the body leans into the dash direction (forward lean, less for upward dashes, more for downward).
            float lean = 0f;
            if (Dashing)
            {
                float pitch = Mathf.Atan2(dashDir.y, Mathf.Max(0.05f, Mathf.Abs(dashDir.x))) * Mathf.Rad2Deg;
                lean = DashLean - 0.6f * Mathf.Clamp(pitch, -90f, 90f);
            }
            visual.localRotation = Quaternion.Euler(0f, 0f, -lean * facing) * Quaternion.Euler(0f, facing * 90f, 0f);
        }

        if (animator != null)
        {
            bool moving = Mathf.Abs(input) > 0.01f;
            animator.SetBool("Run", moving);
            animator.SetBool("Grounded", grounded);
            animator.SetBool("Dashing", Dashing);
            // Walking away from the aim direction plays the run cycle backwards.
            animator.SetFloat("RunSpeed", moving && Mathf.Sign(input) != facing ? -1f : 1f);
        }
    }

    Vector2 MouseAim(Mouse mouse)
    {
        var cam = Camera.main;
        if (mouse == null || cam == null) return Vector2.right * facing;
        Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 aim = world - ChestPosition;
        return aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right * facing;
    }

    public void PickUp()
    {
        HasWeapon = true;
        if (weaponInHand != null) weaponInHand.gameObject.SetActive(true);
    }

    bool IsGrounded()
    {
        Bounds b = capsule.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y - 0.05f);
        Vector2 size = new Vector2(b.size.x * 0.8f, 0.1f);
        foreach (var c in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (c == capsule || c.isTrigger || c.GetComponent<TestThrownWeapon>() != null) continue;
            return true;
        }
        return false;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(12, 8, 700, 24), "A/D move   Space jump   Mouse aim + Left click throw   Shift / Right click dash   (character test scene)");
        GUI.Label(new Rect(12, 28, 400, 24), "Dash charges: " + DashCharges + "/" + MaxDashCharges + "    Weapon: " + (HasWeapon ? "in hand" : "thrown"));
    }
}
