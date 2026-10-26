using UnityEngine;

// Small code-drawn impact effects: sparks on a hit, a burst when a monster dies, and a camera shake.
public static class HitFx
{
    public static void Sparks(Vector2 pos, Vector2 normal, Color color, int count = 6, float speed = 6f)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Quaternion.Euler(0f, 0f, Random.Range(-70f, 70f)) * normal;
            Spawn(pos, (Vector2)dir * Random.Range(speed * 0.5f, speed), color, Random.Range(0.1f, 0.18f), Random.Range(0.18f, 0.3f));
        }
    }

    public static void Burst(Vector2 pos, Color color, int count = 14)
    {
        for (int i = 0; i < count; i++)
        {
            var dir = Random.insideUnitCircle.normalized;
            Spawn(pos, dir * Random.Range(2f, 7f) + Vector2.up * 2f, color, Random.Range(0.12f, 0.28f), Random.Range(0.35f, 0.6f));
        }
    }

    public static void Shake(float amplitude, float duration)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null) follow.Shake(amplitude, duration);
    }

    // A dot that stays where it is and fades out: the trail of a flying weapon.
    public static void Dot(Vector2 pos, Color color, float size = 0.3f, float life = 0.3f) => Spawn(pos, Vector2.zero, color, size, life, 0f);

    static void Spawn(Vector2 pos, Vector2 velocity, Color color, float size, float life, float gravity = 18f)
    {
        var sr = HazardArt.Make("Fx", HazardArt.Square, color, 50);
        sr.transform.position = pos;
        sr.transform.localScale = Vector3.one * size;
        sr.gameObject.AddComponent<FxParticle>().Init(velocity, life, gravity);
    }
}

public class FxParticle : MonoBehaviour
{
    Vector2 velocity;
    float life, age;
    SpriteRenderer sr;
    Color color;
    Vector3 scale;
    float gravity = 18f;

    public void Init(Vector2 v, float lifetime, float gravityPull = 18f)
    {
        velocity = v;
        life = lifetime;
        gravity = gravityPull;
        sr = GetComponent<SpriteRenderer>();
        color = sr.color;
        scale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }

        velocity.y -= gravity * Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);

        float t = age / life;
        sr.color = new Color(color.r, color.g, color.b, 1f - t * t);
        transform.localScale = scale * (1f - t * 0.5f);
    }
}
