using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class EnemyBullet : MonoBehaviour
{
    const float Lifetime = 6f;

    float dieAt;
    int damage;

    public static void Spawn(GameObject prefab, Vector2 pos, Vector2 velocity, int damage)
    {
        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.GetComponent<Rigidbody2D>().linearVelocity = velocity;
        var bullet = go.GetComponent<EnemyBullet>();
        bullet.dieAt = Time.time + Lifetime;
        bullet.damage = damage;
    }

    void Update()
    {
        if (Time.time >= dieAt) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) => Handle(other);
    void OnTriggerStay2D(Collider2D other) => Handle(other);

    void Handle(Collider2D other)
    {
        if (other.isTrigger) return;

        if (other.TryGetComponent<Player>(out var p))
        {
            if (p.Invulnerable) return;
            p.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (other.GetComponentInParent<Enemy>() != null) return;
        if (other.GetComponent<PlatformEffector2D>() != null) return;
        Destroy(gameObject);
    }
}
