using UnityEngine;

public class Shooter : Enemy
{
    const float HoverY = 4.3f;
    const float FireCooldown = 2.4f;
    const float TelegraphTime = 0.6f;
    const float BulletSpeed = 7f;
    const int BulletCount = 5;
    const float SpreadDegrees = 18f;

    public GameObject bulletPrefab;

    float nextFire, fireAt = -1f;

    protected override float GravityScale => 0f;
    protected override bool IgnorePlatforms => true;

    void OnEnable() => nextFire = Time.time + 1.2f;

    protected override void Move(Player p)
    {
        bool winding = fireAt >= 0f;
        telegraphing = winding;

        float vx = 0f;
        if (p != null && !winding)
        {
            float dx = p.transform.position.x - rb.position.x;
            if (Mathf.Abs(dx) > 2f) vx = Mathf.Sign(dx) * speed;
        }
        rb.linearVelocity = new Vector2(vx, (HoverY - rb.position.y) * 4f);

        if (p == null) return;
        if (!winding && Time.time >= nextFire)
        {
            fireAt = Time.time + TelegraphTime;
        }
        else if (winding && Time.time >= fireAt)
        {
            Fire(p);
            fireAt = -1f;
            nextFire = Time.time + FireCooldown;
        }
    }

    void Fire(Player p)
    {
        if (bulletPrefab == null) return;
        Vector2 to = ((Vector2)p.transform.position - rb.position).normalized;
        float baseAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        for (int i = 0; i < BulletCount; i++)
        {
            float angle = (baseAngle + (i - (BulletCount - 1) * 0.5f) * SpreadDegrees) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            EnemyBullet.Spawn(bulletPrefab, rb.position, dir * BulletSpeed, stats.attack);
        }
    }
}
