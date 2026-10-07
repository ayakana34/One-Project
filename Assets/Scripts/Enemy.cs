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

    public Collider2D Col { get; private set; }
    public int Hp => hp;
    public int MaxHp => stats.maxHp;

    protected Rigidbody2D rb;
    protected bool telegraphing;
    protected virtual int ContactDamage => stats.attack;
    protected virtual float GravityScale => 3f;
    protected virtual bool IgnorePlatforms => false;
    protected virtual bool LockFacing => false;
    protected virtual Sprite SpriteForState() => idleSprite;
    protected float facing = 1f;

    int hp;
    float flashUntil;
    Color baseColor;
    SpriteRenderer sr;
    SpriteRenderer visual;

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

        if (IgnorePlatforms)
        {
            foreach (var effector in FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None))
                Physics2D.IgnoreCollision(Col, effector.GetComponent<Collider2D>());
        }
    }

    void OnDestroy() => All.Remove(this);

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
        else if (telegraphing) visual.color = (int)(Time.time * 12f) % 2 == 0 ? new Color(1f, 0.85f, 0.4f) : Color.white;
        else visual.color = Color.white;
    }

    void FixedUpdate()
    {
        var gm = GameManager.Instance;
        var p = gm.Player;
        bool live = p != null && !p.Dead && gm.State == GameManager.GameState.Playing;

        Move(live ? p : null);

        if (live && Col.Distance(p.Col).isOverlapped) p.TakeDamage(ContactDamage);
    }

    protected virtual void Move(Player p)
    {
        float dir = 0f;
        if (p != null)
        {
            float dx = p.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.1f) dir = Mathf.Sign(dx);
        }
        rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
    }

    public void ScaleStats(float hpMultiplier, float attackMultiplier)
    {
        stats.maxHp = Mathf.Max(1, Mathf.RoundToInt(stats.maxHp * hpMultiplier));
        stats.attack = Mathf.Max(1, Mathf.RoundToInt(stats.attack * attackMultiplier));
        hp = stats.maxHp;
    }

    public void TakeHit(int damage)
    {
        hp -= stats.Reduce(damage);
        flashUntil = Time.time + 0.1f;
        if (hp <= 0) Destroy(gameObject);
    }
}
