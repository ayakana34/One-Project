using UnityEngine;

// Test-only stand-in for the thrown weapon: flies straight, then falls and bounces, and is picked up by walking over it.
public class TestThrownWeapon : MonoBehaviour
{
    const float Speed = 22f;
    const float MaxRange = 9f;
    const float PickupDistance = 0.9f;
    const float FallGravity = 5f;

    CharacterTestController owner;
    Rigidbody2D body;
    float traveled, born;
    bool flying = true;

    public static TestThrownWeapon Spawn(CharacterTestController owner, Vector2 pos, Vector2 dir)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ThrownWeapon";
        Destroy(go.GetComponent<BoxCollider>());
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.5f, 0.1f, 0.1f);
        if (owner.weaponMaterial != null) go.GetComponent<Renderer>().sharedMaterial = owner.weaponMaterial;

        var w = go.AddComponent<TestThrownWeapon>();
        w.owner = owner;
        w.born = Time.time;
        w.body = go.AddComponent<Rigidbody2D>();
        w.body.gravityScale = 0f;
        w.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        w.body.linearVelocity = dir * Speed;
        w.body.angularVelocity = 720f;

        var col = go.AddComponent<BoxCollider2D>();
        col.sharedMaterial = new PhysicsMaterial2D { bounciness = 0.4f, friction = 0.4f };
        Physics2D.IgnoreCollision(col, owner.GetComponent<Collider2D>());
        return w;
    }

    void Update()
    {
        if (flying)
        {
            traveled += Speed * Time.deltaTime;
            if (traveled >= MaxRange) Fall();
        }
        else if (Time.time - born > 0.35f && Vector2.Distance(transform.position, owner.ChestPosition) < PickupDistance)
        {
            owner.PickUp();
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (flying) Fall();
    }

    void Fall()
    {
        flying = false;
        body.gravityScale = FallGravity;
        body.linearVelocity *= 0.3f;
    }
}
