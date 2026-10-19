using System.Collections.Generic;
using UnityEngine;

// The boss cycles: chase -> telegraph -> one attack -> recover. Attacks:
//   Charge: a straight dash at the player.
//   Rock:   throws a big rock up; a shadow marks the landing spot, the rock hatches a small golem on impact.
//   Slam:   jumps at the player and sends dust waves along the ground when it lands (jump over them).
// Below half health the boss is enraged: it acts faster, throws two rocks and keeps more golems around.
public class Boss : Enemy
{
    public enum Attack { Charge, Rock, Slam }
    enum Phase { Chase, Telegraph, Charge, Air, Recover }

    const float TelegraphTime = 0.8f;
    const float ChargeTime = 0.7f;
    const float JumpSpeed = 13f;
    const float MaxJumpVx = 11f;
    const float RockRise = 0.45f, RockFall = 0.95f;

    public static Boss Current { get; private set; }

    [Header("Sprites (optional)")]
    public Sprite windupSprite;
    public Sprite chargeSprite;

    // Test hooks: a fixed attack order and a forced enrage.
    [System.NonSerialized] public Attack[] testSequence;
    [System.NonSerialized] public bool forceEnraged;

    Phase phase = Phase.Chase;
    Attack attack = Attack.Charge, lastAttack = Attack.Slam;
    float phaseStart, phaseUntil, chargeDir = 1f, jumpVx;
    int testIndex;
    bool leftGround;
    readonly List<Enemy> minions = new();

    public bool Enraged => forceEnraged || Hp * 2 <= MaxHp;
    float ChaseTime => Enraged ? 1.1f : 1.8f;
    float ChargeSpeed => Enraged ? 18f : 15f;
    int MinionCap => Enraged ? 4 : 2;

    protected override int ContactDamage => phase == Phase.Charge ? stats.attack * 2 : stats.attack;

    // The boss arena is a single screen, so the boss always goes for the player.
    protected override float AggroRange => 1000f;

    // The direction is locked from the wind-up on, so the sprite keeps facing it.
    protected override bool LockFacing => phase == Phase.Telegraph || phase == Phase.Charge || phase == Phase.Air;

    protected override Sprite SpriteForState()
    {
        switch (phase)
        {
            case Phase.Telegraph:
                return windupSprite != null ? windupSprite : idleSprite;
            case Phase.Charge:
            case Phase.Air:
                return chargeSprite != null ? chargeSprite : (windupSprite != null ? windupSprite : idleSprite);
            default:
                return idleSprite;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    void OnEnable()
    {
        Current = this;
        phase = Phase.Chase;
        phaseStart = Time.time;
        phaseUntil = Time.time + ChaseTime;
    }

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    protected override void OnDestroy()
    {
        // the golems fall apart with the boss, so the stage can end
        foreach (var m in minions) if (m != null) Destroy(m.gameObject);
        minions.Clear();
        base.OnDestroy();
    }

    int LiveMinions()
    {
        minions.RemoveAll(m => m == null);
        return minions.Count;
    }

    protected override void Move(Player p)
    {
        telegraphing = phase == Phase.Telegraph;
        tint = Enraged ? new Color(1f, 0.72f, 0.72f) : Color.white;
        if (p == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float dx = p.transform.position.x - transform.position.x;
        float vx = 0f;
        switch (phase)
        {
            case Phase.Chase:
                if (Mathf.Abs(dx) > 0.3f) vx = Mathf.Sign(dx) * speed * (Enraged ? 1.3f : 1f);
                if (Time.time >= phaseUntil) StartTelegraph(dx);
                break;
            case Phase.Telegraph:
                if (Time.time >= phaseUntil) BeginAttack(p, dx);
                break;
            case Phase.Charge:
                vx = chargeDir * ChargeSpeed;
                bool blocked = Time.time - phaseStart > 0.15f && Mathf.Abs(rb.linearVelocity.x) < 1f;
                if (blocked || Time.time >= phaseUntil) StartPhase(Phase.Recover, 1f);
                break;
            case Phase.Air:
                vx = jumpVx;
                if (!leftGround && rb.linearVelocity.y > 1f) leftGround = true;
                bool landed = leftGround && rb.linearVelocity.y <= 0.1f && OnGround();
                if (landed || Time.time >= phaseUntil) Land();
                break;
            default:
                if (Time.time >= phaseUntil) StartPhase(Phase.Chase, ChaseTime);
                break;
        }
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    void StartPhase(Phase next, float duration)
    {
        phase = next;
        phaseStart = Time.time;
        phaseUntil = Time.time + duration;
    }

    Attack ChooseAttack()
    {
        if (testSequence != null && testSequence.Length > 0) return testSequence[testIndex++ % testSequence.Length];

        var options = new List<Attack>();
        foreach (Attack a in System.Enum.GetValues(typeof(Attack)))
        {
            if (a == lastAttack) continue; // never the same attack twice in a row
            if (a == Attack.Rock && LiveMinions() >= MinionCap) continue;
            options.Add(a);
        }
        return options[Random.Range(0, options.Count)];
    }

    void StartTelegraph(float dx)
    {
        attack = ChooseAttack();
        lastAttack = attack;
        chargeDir = dx >= 0f ? 1f : -1f;
        facing = chargeDir;
        StartPhase(Phase.Telegraph, TelegraphTime);
    }

    void BeginAttack(Player p, float dx)
    {
        switch (attack)
        {
            case Attack.Charge:
                chargeDir = dx >= 0f ? 1f : -1f;
                facing = chargeDir;
                StartPhase(Phase.Charge, ChargeTime);
                break;
            case Attack.Rock:
                ThrowRocks(p);
                StartPhase(Phase.Recover, 1.4f);
                break;
            default:
                StartJump(p);
                break;
        }
    }

    // ---- rock + golem ----

    void ThrowRocks(Player p)
    {
        float groundY = Col.bounds.min.y;
        Vector2 from = new Vector2(transform.position.x, Col.bounds.max.y);
        int rocks = Enraged ? 2 : 1;
        for (int i = 0; i < rocks; i++)
        {
            int index = i;
            float rise = RockRise + index * 0.35f;
            FallingRock.Spawn(this, from, () => RockTarget(p, index, rise), groundY, stats.attack, rise, RockFall, SpawnGolem);
        }
    }

    // The first rock lands where the player is when it leaves the screen, the second one where the player is heading.
    static float RockTarget(Player p, int index, float rise)
    {
        if (p == null) return 0f;
        float x = p.transform.position.x;
        if (index > 0) x += p.MoveX * 7f * RockFall;
        return Mathf.Clamp(x, -9f, 9f);
    }

    void SpawnGolem(float x)
    {
        if (this == null || LiveMinions() >= MinionCap) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        minions.Add(gm.SpawnMinion(new Vector3(x, Col.bounds.min.y + 0.5f, 0f)));
    }

    // ---- jump slam ----

    void StartJump(Player p)
    {
        float g = -Physics2D.gravity.y * rb.gravityScale;
        float airTime = 2f * JumpSpeed / g;
        float dx = p.transform.position.x - transform.position.x;
        jumpVx = Mathf.Clamp(dx / airTime, -MaxJumpVx, MaxJumpVx);
        facing = chargeDir = jumpVx >= 0f ? 1f : -1f;
        leftGround = false;
        rb.linearVelocity = new Vector2(jumpVx, JumpSpeed);
        StartPhase(Phase.Air, airTime + 0.8f);
    }

    bool OnGround()
    {
        var b = Col.bounds;
        var hits = Physics2D.OverlapBoxAll(new Vector2(b.center.x, b.min.y - 0.06f), new Vector2(b.size.x * 0.8f, 0.1f), 0f);
        foreach (var h in hits)
        {
            if (h == Col || h.isTrigger) continue;
            if (h.GetComponent<Player>() != null || h.GetComponentInParent<Enemy>() != null) continue;
            return true;
        }
        return false;
    }

    void Land()
    {
        float groundY = Col.bounds.min.y;
        float x = transform.position.x;
        Dust.Spawn(new Vector2(x, groundY + 0.3f), 2.4f);
        Shockwave.Spawn(this, x - 1f, groundY, -1f, stats.attack);
        Shockwave.Spawn(this, x + 1f, groundY, 1f, stats.attack);
        StartPhase(Phase.Recover, 1.2f);
    }
}
