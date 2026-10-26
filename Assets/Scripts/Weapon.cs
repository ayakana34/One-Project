using UnityEngine;

public class Weapon : MonoBehaviour
{
    enum Phase { Flying, Bouncing, Resting, Recalling }

    const float RecallSpeed = 35f;

    public static Weapon Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    const float Speed = 30f;
    const float BounceSpeed = 22f; // how fast it bounces off after a hit, kept apart from the flight speed so a faster throw does not scatter it farther
    const float MaxRange = 9f;
    const float Radius = 0.25f;
    const float Gravity = 30f;
    const float Restitution = 0.4f;
    const float SurfaceFriction = 0.75f;
    const float HitKickMin = 1f;
    const float HitKickMax = 6f;
    const float RandomAngle = 70f;
    const float WildChance = 0.3f;
    const float RandomSpeedMin = 0.6f;
    const float RandomSpeedMax = 1.5f;
    const float RestSpeed = 2f;
    const int MaxBounces = 6;
    const float PickupDistance = 0.8f;
    const float BounceHitMinSpeed = 3f;   // a weapon that is only sliding or rolling to a stop does not hurt
    const float BounceHitCooldown = 0.4f; // per monster, so one bounce cannot hit the same monster every frame

    Player owner;
    Vector2 dir, velocity;
    Phase phase = Phase.Flying;
    float traveled;
    int bounces;
    readonly System.Collections.Generic.Dictionary<Enemy, float> lastHit = new();
    readonly System.Collections.Generic.HashSet<Enemy> recallHit = new(); // monsters already hit by the current recall

    public static Weapon Spawn(Player owner, Vector2 pos, Vector2 dir)
    {
        var go = Instantiate(GameManager.Instance.weaponPrefab, pos, Quaternion.identity);
        var w = go.GetComponent<Weapon>();
        w.owner = owner;
        w.dir = dir;
        Current = w;
        return w;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    static readonly Color RecallColor = new Color(0.5f, 0.95f, 1f);

    public void Recall()
    {
        phase = Phase.Recalling;
        recallHit.Clear();
        HitFx.Burst(transform.position, RecallColor, 12); // it lights up where it lies
    }

    void RecallMove()
    {
        if (owner == null || owner.Dead) return;

        Vector2 to = (Vector2)owner.transform.position - (Vector2)transform.position;
        float step = RecallSpeed * Time.deltaTime;

        // flying back, the weapon hurts every monster on its way once (full damage, like a throw)
        foreach (var hit in Physics2D.CircleCastAll(transform.position, Radius, to.normalized, Mathf.Min(step, to.magnitude)))
        {
            if (hit.collider.isTrigger) continue;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && recallHit.Add(enemy)) enemy.TakeHit(owner.WeaponDamage);
        }

        // a bright trail along the way back
        Vector2 from = transform.position;
        Vector2 dirBack = to.normalized;
        for (int i = 0; i < 5; i++)
        {
            var c = Color.Lerp(RecallColor, Color.white, i / 4f);
            HitFx.Dot(from + dirBack * (step * i / 5f), c, 0.32f - i * 0.02f, 0.32f);
        }

        if (to.magnitude <= step + PickupDistance)
        {
            HitFx.Burst(owner.transform.position, RecallColor, 10); // a flash as it lands in the hand
            owner.PickUp();
            Destroy(gameObject);
            return;
        }

        transform.position += (Vector3)(to.normalized * step);
        transform.Rotate(0f, 0f, 720f * Time.deltaTime);
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Flying:
                Fly();
                break;
            case Phase.Bouncing:
                Bounce();
                TryPickup();
                break;
            case Phase.Recalling:
                RecallMove();
                break;
            default:
                CheckSupport();
                TryPickup();
                break;
        }
    }

    void CheckSupport()
    {
        foreach (var hit in Physics2D.CircleCastAll(transform.position, Radius, Vector2.down, 0.1f))
        {
            if (hit.collider.isTrigger) continue;
            if (hit.collider.GetComponentInParent<Player>() != null) continue;
            if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
            if (hit.normal.y < 0.5f) continue;
            return;
        }

        phase = Phase.Bouncing;
        velocity = Vector2.zero;
    }

    void Fly()
    {
        float step = Mathf.Min(Speed * Time.deltaTime, MaxRange - traveled);
        foreach (var hit in Physics2D.CircleCastAll(transform.position, Radius, dir, step))
        {
            if (hit.collider.isTrigger) continue;
            if (hit.collider.GetComponentInParent<Player>() != null) continue;
            if (hit.collider.GetComponent<PlatformEffector2D>() != null) continue;

            transform.position = hit.centroid;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null) HurtEnemy(enemy);
            else HitFx.Sparks(hit.point, hit.normal, new Color(0.9f, 0.9f, 0.95f), 4, 4f);

            phase = Phase.Bouncing;
            velocity = dir * BounceSpeed;
            Rebound(hit.normal, enemy == null);
            if (phase == Phase.Bouncing) Randomize(hit.normal);
            velocity.y += Random.Range(HitKickMin, HitKickMax);
            return;
        }

        transform.position += (Vector3)(dir * step);
        traveled += step;
        transform.Rotate(0f, 0f, 720f * Time.deltaTime);

        if (traveled >= MaxRange - 0.001f)
        {
            phase = Phase.Bouncing;
            velocity = dir * 4f;
        }
    }

    void Bounce()
    {
        velocity.y -= Gravity * Time.deltaTime;
        float speed = velocity.magnitude;
        float dist = speed * Time.deltaTime;
        transform.Rotate(0f, 0f, -velocity.x * 40f * Time.deltaTime);
        if (dist <= 0.0001f) return;

        Vector2 moveDir = velocity / speed;
        foreach (var hit in Physics2D.CircleCastAll(transform.position, Radius, moveDir, dist))
        {
            if (hit.collider.isTrigger) continue;
            if (hit.collider.GetComponentInParent<Player>() != null) continue;
            var enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null)
            {
                if (speed >= BounceHitMinSpeed) HurtEnemy(enemy);
                continue;
            }

            bool platform = hit.collider.GetComponent<PlatformEffector2D>() != null;
            if (platform && (velocity.y > 0f || hit.normal.y < 0.5f)) continue;

            transform.position = hit.centroid;
            Rebound(hit.normal, true);
            return;
        }

        transform.position += (Vector3)(moveDir * dist);
    }

    void HurtEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        if (lastHit.TryGetValue(enemy, out float t) && Time.time - t < BounceHitCooldown) return;
        lastHit[enemy] = Time.time;
        enemy.TakeHit(owner.WeaponDamage);
    }

    void Randomize(Vector2 normal)
    {
        float speed = velocity.magnitude * Random.Range(RandomSpeedMin, RandomSpeedMax);
        bool wild = Random.value < WildChance || velocity.sqrMagnitude < 0.0001f;
        Vector2 baseDir = wild ? normal : velocity.normalized;
        float range = wild ? 90f : RandomAngle;

        velocity = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-range, range)) * baseDir) * speed;
        if (Vector2.Dot(velocity, normal) < 0f) velocity = Vector2.Reflect(velocity, normal);
    }

    void Rebound(Vector2 normal, bool allowRest)
    {
        bounces++;
        float vn = Vector2.Dot(velocity, normal);
        Vector2 vt = velocity - vn * normal;
        velocity = vt * SurfaceFriction - vn * normal * Restitution;
        transform.position += (Vector3)(normal * 0.02f);

        if (allowRest && normal.y > 0.5f && Vector2.Dot(velocity, normal) < RestSpeed)
        {
            Rest();
            return;
        }

        if (bounces >= MaxBounces) velocity.x = 0f;
    }

    void Rest()
    {
        phase = Phase.Resting;
        transform.rotation = Quaternion.identity;
    }

    void TryPickup()
    {
        if (owner == null || owner.Dead || owner.HasWeapon) return;
        if (Vector2.Distance(transform.position, owner.transform.position) < PickupDistance)
        {
            owner.PickUp();
            Destroy(gameObject);
        }
    }
}
