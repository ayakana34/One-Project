using System.Collections.Generic;
using UnityEngine;

// The boss cycles: chase -> telegraph -> one attack -> recover. Attacks:
//   Charge: a straight dash at the player.
//   Slam:   jumps at the player and sends dust waves along the ground when it lands (jump over them).
// Below half health the boss is enraged: it chases and acts faster, charges twice in a row, and after the landing
// of a slam it hammers the ground with both hands for a second set of shockwaves.
public class Boss : Enemy
{
    public enum Attack { Charge, Slam }
    enum Phase { Chase, Telegraph, Charge, Air, Recover }

    const float TelegraphTime = 0.8f;
    const float ChargeTime = 0.7f;
    const float ChainChargePause = 0.45f; // enraged: the pause (wind-up blink) between the two charges
    const float HandSlamDelay = 0.45f;    // enraged: time from the landing to the second shockwaves
    const float JumpSpeed = 13f;
    const float MaxJumpVx = 11f;

    public static Boss Current { get; private set; }

    [Header("Sprites (optional)")]
    public Sprite windupSprite;
    public Sprite chargeSprite;

    // Test hooks: a fixed attack order and a forced enrage.
    [System.NonSerialized] public Attack[] testSequence;
    [System.NonSerialized] public bool forceEnraged;

    Phase phase = Phase.Chase;
    Attack attack = Attack.Charge, lastAttack = Attack.Slam;
    int repeats, chargeCount;
    float handSlamAt = -1f;
    float phaseStart, phaseUntil, chargeDir = 1f, jumpVx;
    int testIndex;
    bool leftGround;

    public bool Enraged => forceEnraged || Hp * 2 <= MaxHp;
    float ChaseTime => Enraged ? 1.1f : 1.8f;
    float ChargeSpeed => Enraged ? 18f : 15f;

    protected override int ContactDamage => phase == Phase.Charge ? stats.attack * 2 : stats.attack;

    // The boss arena is a single screen, so the boss always goes for the player.
    protected override float AggroRange => 1000f;

    // The direction is locked from the wind-up on, so the sprite keeps facing it.
    protected override bool LockFacing => phase == Phase.Telegraph || phase == Phase.Charge || phase == Phase.Air;

    protected override Sprite SpriteForState()
    {
        if (handSlamAt >= 0f) return windupSprite != null ? windupSprite : idleSprite;
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

    protected override void Move(Player p)
    {
        telegraphing = phase == Phase.Telegraph || handSlamAt >= 0f;
        if (handSlamAt >= 0f && Time.time >= handSlamAt) HandSlam();
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
                if (blocked || Time.time >= phaseUntil)
                {
                    if (Enraged && chargeCount < 2)
                    {
                        // a second charge right behind the first one, aimed at where the player is now
                        attack = Attack.Charge;
                        chargeDir = dx >= 0f ? 1f : -1f;
                        facing = chargeDir;
                        StartPhase(Phase.Telegraph, ChainChargePause);
                    }
                    else StartPhase(Phase.Recover, 1f);
                }
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

        // random, but never the same attack three times in a row
        var next = Random.value < 0.5f ? Attack.Charge : Attack.Slam;
        if (next == lastAttack && repeats >= 2) next = next == Attack.Charge ? Attack.Slam : Attack.Charge;
        return next;
    }

    void StartTelegraph(float dx)
    {
        attack = ChooseAttack();
        repeats = attack == lastAttack ? repeats + 1 : 1;
        lastAttack = attack;
        chargeCount = 0;
        chargeDir = dx >= 0f ? 1f : -1f;
        facing = chargeDir;
        StartPhase(Phase.Telegraph, TelegraphTime);
    }

    void BeginAttack(Player p, float dx)
    {
        switch (attack)
        {
            case Attack.Charge:
                chargeCount++;
                chargeDir = dx >= 0f ? 1f : -1f;
                facing = chargeDir;
                StartPhase(Phase.Charge, ChargeTime);
                break;
            default:
                StartJump(p);
                break;
        }
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

        if (Enraged)
        {
            handSlamAt = Time.time + HandSlamDelay;
            StartPhase(Phase.Recover, 1.2f + HandSlamDelay);
        }
        else StartPhase(Phase.Recover, 1.2f);
    }

    // Enraged: both hands pound the ground a moment after the landing, sending out a second set of shockwaves.
    void HandSlam()
    {
        handSlamAt = -1f;
        float groundY = Col.bounds.min.y;
        float x = transform.position.x;
        Dust.Spawn(new Vector2(x - 1.3f, groundY + 0.3f), 1.8f);
        Dust.Spawn(new Vector2(x + 1.3f, groundY + 0.3f), 1.8f);
        Shockwave.Spawn(this, x - 1f, groundY, -1f, stats.attack);
        Shockwave.Spawn(this, x + 1f, groundY, 1f, stats.attack);
        HitFx.Shake(0.25f, 0.25f);
    }
}
