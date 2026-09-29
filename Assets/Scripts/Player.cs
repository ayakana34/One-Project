using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class Player : MonoBehaviour
{
    public const int MaxDashCharges = 2;

    const float MoveSpeed = 7f;
    const float JumpSpeed = 13f;
    const float GravityScale = 3f;
    const float DashSpeed = 22f;
    const float DashTime = 0.15f;
    const float DashRechargeTime = 1.5f;
    const float DashEndInvulnTime = 0.1f;
    const float HitInvulnTime = 1f;
    const float DropThroughTime = 0.35f;

    public Stats stats = new Stats { maxHp = 50, attack = 10 };

    public int Hp { get; private set; }
    public int MaxHp => stats.maxHp;
    public int WeaponDamage => stats.ScaledAttack;
    public bool HasWeapon { get; private set; } = true;
    public bool Dead => Hp <= 0;
    public Collider2D Col => col;
    public bool Invulnerable => Dead || Time.time < dashUntil + DashEndInvulnTime || Time.time < hitInvulnUntil;
    public bool Dashing => Time.time < dashUntil;
    public int DashCharges { get; private set; } = MaxDashCharges;
    public float DashRechargeProgress => DashCharges >= MaxDashCharges ? 1f : Mathf.Clamp01(1f - (rechargeAt - Time.time) / DashRechargeTime);

    public SpriteRenderer bodyRenderer;
    public Transform aimMark;

    Rigidbody2D rb;
    Collider2D col;
    Vector2 dashDir = Vector2.right;
    float moveX, dashUntil, rechargeAt, hitInvulnUntil, dropUntil;
    Collider2D dropCollider;
    bool active, jumpRequested, wasDashing, ignoringPlatforms;
    Collider2D[] platforms;
    float platformIgnoreUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        Hp = stats.maxHp;
        var effectors = FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None);
        platforms = new Collider2D[effectors.Length];
        for (int i = 0; i < effectors.Length; i++) platforms[i] = effectors[i].GetComponent<Collider2D>();
        rb.gravityScale = GravityScale;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        active = kb != null && mouse != null && !Dead && GameManager.Instance.State == GameManager.GameState.Playing;

        if (dropCollider != null && Time.time >= dropUntil)
        {
            if (!ignoringPlatforms && dropCollider.gameObject.activeInHierarchy)
                Physics2D.IgnoreCollision(col, dropCollider, false);
            dropCollider = null;
        }
        UpdatePlatformIgnore();

        if (DashCharges < MaxDashCharges && Time.time >= rechargeAt)
        {
            DashCharges++;
            if (DashCharges < MaxDashCharges) rechargeAt = Time.time + DashRechargeTime;
        }

        if (!active)
        {
            moveX = 0f;
            jumpRequested = false;
            return;
        }

        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 aim = mouseWorld - rb.position;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;

        moveX = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);

        if (kb.spaceKey.wasPressedThisFrame)
        {
            var ground = GroundCollider();
            if (ground != null)
            {
                if (kb.sKey.isPressed && ground.GetComponent<PlatformEffector2D>() != null)
                {
                    dropCollider = ground;
                    dropUntil = Time.time + DropThroughTime;
                    Physics2D.IgnoreCollision(col, ground, true);
                }
                else
                {
                    jumpRequested = true;
                }
            }
        }

        bool dashPressed = kb.leftShiftKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
        if (dashPressed && !Dashing && DashCharges > 0)
        {
            if (DashCharges == MaxDashCharges) rechargeAt = Time.time + DashRechargeTime;
            DashCharges--;
            dashDir = aim;
            dashUntil = Time.time + DashTime;
            if (aim.y < -0.3f) platformIgnoreUntil = dashUntil + 0.1f;
            UpdatePlatformIgnore();
        }

        if (mouse.leftButton.wasPressedThisFrame && HasWeapon)
        {
            HasWeapon = false;
            Weapon.Spawn(this, rb.position + aim * 0.6f, aim);
        }

        UpdateVisuals(aim);
    }

    void UpdatePlatformIgnore()
    {
        bool ignore = Time.time < platformIgnoreUntil;
        if (ignore == ignoringPlatforms) return;
        ignoringPlatforms = ignore;
        foreach (var p in platforms)
        {
            if (p != null && p.gameObject.activeInHierarchy) Physics2D.IgnoreCollision(col, p, ignore);
        }
    }

    void UpdateVisuals(Vector2 aim)
    {
        aimMark.gameObject.SetActive(HasWeapon);
        aimMark.localPosition = aim * 0.7f;
        aimMark.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

        bool blink = Time.time < hitInvulnUntil && (int)(Time.time * 20f) % 2 == 0;
        var c = bodyRenderer.color;
        c.a = Dashing ? 0.5f : blink ? 0.3f : 1f;
        bodyRenderer.color = c;
    }

    void FixedUpdate()
    {
        if (active && Dashing)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = dashDir * DashSpeed;
            wasDashing = true;
            return;
        }

        rb.gravityScale = GravityScale;
        var v = rb.linearVelocity;
        if (wasDashing)
        {
            v = Vector2.zero;
            wasDashing = false;
        }
        v.x = active ? moveX * MoveSpeed : 0f;
        if (jumpRequested)
        {
            v.y = JumpSpeed;
            jumpRequested = false;
        }
        rb.linearVelocity = v;
    }

    Collider2D GroundCollider()
    {
        var b = col.bounds;
        var center = new Vector2(b.center.x, b.min.y - 0.05f);
        var size = new Vector2(b.size.x * 0.9f, 0.1f);
        foreach (var h in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (h == col || h == dropCollider || h.isTrigger) continue;
            if (h.GetComponent<Enemy>() != null) continue;
            if (h.GetComponent<PlatformEffector2D>() != null && rb.linearVelocity.y > 0.1f) continue;
            return h;
        }
        return null;
    }

    public void PickUp() => HasWeapon = true;

    public void TakeDamage(int damage)
    {
        if (Invulnerable) return;
        Hp = Mathf.Max(0, Hp - stats.Reduce(damage));
        hitInvulnUntil = Time.time + HitInvulnTime;
        if (Dead) GameManager.Instance.OnPlayerDead();
    }
}
