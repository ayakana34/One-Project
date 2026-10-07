using UnityEngine;

public class Boss : Enemy
{
    enum Phase { Chase, Telegraph, Charge, Recover }

    const float ChaseTime = 1.8f;
    const float TelegraphTime = 0.8f;
    const float ChargeTime = 0.7f;
    const float RecoverTime = 1f;
    const float ChargeSpeed = 15f;

    public static Boss Current { get; private set; }

    [Header("Sprites (optional)")]
    public Sprite windupSprite;
    public Sprite chargeSprite;

    Phase phase = Phase.Chase;
    float phaseStart, phaseUntil, chargeDir = 1f;

    protected override int ContactDamage => phase == Phase.Charge ? stats.attack * 2 : stats.attack;

    // The charge direction is locked from the wind-up on, so the sprite keeps facing it.
    protected override bool LockFacing => phase == Phase.Telegraph || phase == Phase.Charge;

    protected override Sprite SpriteForState()
    {
        switch (phase)
        {
            case Phase.Telegraph:
                return windupSprite != null ? windupSprite : idleSprite;
            case Phase.Charge:
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
        telegraphing = phase == Phase.Telegraph;
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
                if (Mathf.Abs(dx) > 0.3f) vx = Mathf.Sign(dx) * speed;
                if (Time.time >= phaseUntil) StartPhase(Phase.Telegraph, TelegraphTime, dx);
                break;
            case Phase.Telegraph:
                if (Time.time >= phaseUntil) StartPhase(Phase.Charge, ChargeTime, dx);
                break;
            case Phase.Charge:
                vx = chargeDir * ChargeSpeed;
                bool blocked = Time.time - phaseStart > 0.15f && Mathf.Abs(rb.linearVelocity.x) < 1f;
                if (blocked || Time.time >= phaseUntil) StartPhase(Phase.Recover, RecoverTime, dx);
                break;
            default:
                if (Time.time >= phaseUntil) StartPhase(Phase.Chase, ChaseTime, dx);
                break;
        }
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    void StartPhase(Phase next, float duration, float dx)
    {
        phase = next;
        phaseStart = Time.time;
        phaseUntil = Time.time + duration;
        if (next == Phase.Telegraph)
        {
            chargeDir = dx >= 0f ? 1f : -1f;
            facing = chargeDir;
        }
    }
}
