using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new();

    public Stats stats = new Stats { maxHp = 10, attack = 10 };
    public float speed = 2.8f;

    [Header("Sprite (optional, the shape is used when empty)")]
    public Sprite idleSprite;
    public float visualScale = 1f;
    [System.NonSerialized] public Color tint = Color.white; // e.g. stone grey for the boss's golems, red when the boss is enraged

    public Collider2D Col { get; private set; }
    public Bounds VisualBounds => visual != null ? visual.bounds : sr.bounds;
    public Transform VisualTransform => visual != null ? visual.transform : transform;
    public void SetVisualVisible(bool on)
    {
        if (visual != null) visual.enabled = on;
        else sr.enabled = on;
    }
    public int Hp => hp;
    public int MaxHp => stats.maxHp;

    protected Rigidbody2D rb;
    protected bool telegraphing;
    protected virtual int ContactDamage => stats.attack;
    protected virtual float GravityScale => 3f;
    protected virtual bool IgnorePlatforms => false;
    protected virtual float AggroRange => aggroRange;
    protected virtual bool CanBeHurt => true; // the boss ignores hits during its intro and its enrage
    protected virtual bool StartDeath() => false; // a subclass can play its own death and destroy itself later

    // Enemies on the wide stage maps stay idle until the player comes close, then chase for good.
    public float aggroRange = 12f;
    bool aggroed;

    protected virtual bool LockFacing => false;
    protected virtual Sprite SpriteForState() => idleSprite;
    protected float facing = 1f;

    // A monster that has just appeared cannot hurt by touch for a moment, so a stage change never costs health at once.
    const float SpawnGrace = 0.75f;
    protected float harmlessUntil;

    int hp;
    float flashUntil;
    Color baseColor;
    protected SpriteRenderer sr;
    protected SpriteRenderer visual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        Col = GetComponent<Collider2D>();

        rb.gravityScale = GravityScale;
        rb.mass = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        hp = stats.maxHp;
        baseColor = sr.color;
        harmlessUntil = Time.time + SpawnGrace;

        if (idleSprite != null)
        {
            // The sprite fills the same unit square as the collider (feet at the bottom), drawn on a child
            // so the collider and the gameplay size stay exactly as with the shape.
            var go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * visualScale;
            visual = go.AddComponent<SpriteRenderer>();
            visual.sprite = idleSprite;
            visual.sortingOrder = sr.sortingOrder;
            sr.enabled = false;
            baseColor = Color.white;
        }
        All.Add(this);
    }

    void Start()
    {
        var p = GameManager.Instance.Player;
        if (p != null) Physics2D.IgnoreCollision(Col, p.Col);

        // Monsters do not block each other either (a hatched golem can stand inside the boss), the same as with the player.
        foreach (var other in All)
            if (other != null && other != this && other.Col != null) Physics2D.IgnoreCollision(Col, other.Col);

        if (IgnorePlatforms)
        {
            foreach (var effector in FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None))
                Physics2D.IgnoreCollision(Col, effector.GetComponent<Collider2D>());
        }
    }

    protected virtual void OnDestroy() => All.Remove(this);

    void Update()
    {
        if (visual != null)
        {
            UpdateVisual();
            return;
        }

        if (Time.time < flashUntil) sr.color = Color.white;
        else if (telegraphing) sr.color = (int)(Time.time * 12f) % 2 == 0 ? new Color(1f, 0.85f, 0.2f) : baseColor;
        else sr.color = Color.Lerp(baseColor, Color.white, (1f - (float)hp / stats.maxHp) * 0.5f);
    }

    void UpdateVisual()
    {
        var sprite = SpriteForState();
        if (sprite != null && visual.sprite != sprite) visual.sprite = sprite;

        if (!LockFacing)
        {
            var p = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (p != null && Mathf.Abs(p.transform.position.x - transform.position.x) > 0.1f)
                facing = Mathf.Sign(p.transform.position.x - transform.position.x);
        }
        visual.flipX = facing < 0f;

        // Sprites cannot get brighter by tinting, so the hit flash is a red tint and the telegraph blink stays yellow.
        if (Time.time < flashUntil) visual.color = new Color(1f, 0.45f, 0.45f);
        else if (telegraphing) visual.color = (int)(Time.time * 12f) % 2 == 0 ? new Color(1f, 0.85f, 0.4f) : tint;
        else visual.color = tint;
    }

    void FixedUpdate()
    {
        var gm = GameManager.Instance;
        var p = gm.Player;
        bool live = p != null && !p.Dead && gm.State == GameManager.GameState.Playing;

        if (live && !aggroed && Vector2.Distance(p.transform.position, transform.position) <= AggroRange) aggroed = true;
        Move(live && aggroed ? p : null);

        if (live && Time.time >= harmlessUntil && Col.Distance(p.Col).isOverlapped) p.TakeDamage(ContactDamage);
    }

    protected virtual void Move(Player p)
    {
        float dir = 0f;
        if (p != null)
        {
            float dx = p.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f) dir = Mathf.Sign(dx);
        }
        var v = new Vector2(dir * speed, rb.linearVelocity.y);
        if (dir != 0f && GravityScale > 0f && ShouldStepUp(dir)) v.y = StepJumpSpeed;
        rb.linearVelocity = v;
    }

    // Walking monsters hop up the steps and pillars of the stage maps when something solid blocks the way.
    const float StepJumpSpeed = 12f;
    float nextStepAt;

    bool ShouldStepUp(float dir)
    {
        if (Time.time < nextStepAt || Mathf.Abs(rb.linearVelocity.y) > 0.1f) return false;
        var b = Col.bounds;
        var origin = new Vector2(b.center.x + dir * (b.extents.x + 0.05f), b.min.y + 0.2f);
        foreach (var hit in Physics2D.RaycastAll(origin, new Vector2(dir, 0f), 0.3f))
        {
            var c = hit.collider;
            if (c == null || c.isTrigger || c == Col) continue;
            if (c.GetComponentInParent<Enemy>() != null || c.GetComponent<Player>() != null) continue;
            if (c.GetComponent<PlatformEffector2D>() != null) continue;
            nextStepAt = Time.time + 0.5f;
            return true;
        }
        return false;
    }

    public void ScaleStats(float hpMultiplier, float attackMultiplier)
    {
        stats.maxHp = Mathf.Max(1, Mathf.RoundToInt(stats.maxHp * hpMultiplier));
        stats.attack = Mathf.Max(1, Mathf.RoundToInt(stats.attack * attackMultiplier));
        hp = stats.maxHp;
    }

    public void TakeHit(int damage)
    {
        if (!CanBeHurt)
        {
            HitFx.Sparks(Col.bounds.center, Vector2.up, new Color(0.8f, 0.85f, 1f), 3, 4f);
            return;
        }

        hp -= stats.Reduce(damage);
        flashUntil = Time.time + 0.1f;

        var center = (Vector2)Col.bounds.center;
        if (hp <= 0 && StartDeath()) return;
        if (hp <= 0)
        {
            HitFx.Burst(center, new Color(1f, 0.55f, 0.3f), 16);
            HitFx.Shake(0.18f, 0.2f);
            Destroy(gameObject);
        }
        else
        {
            HitFx.Sparks(center, Vector2.up, new Color(1f, 0.95f, 0.6f), 6);
            HitFx.Shake(0.07f, 0.1f);
        }
    }
}
