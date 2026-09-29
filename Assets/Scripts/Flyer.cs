using UnityEngine;

public class Flyer : Enemy
{
    protected override float GravityScale => 0f;
    protected override bool IgnorePlatforms => true;

    protected override void Move(Player p)
    {
        Vector2 v = Vector2.zero;
        if (p != null)
        {
            Vector2 to = (Vector2)p.transform.position - rb.position;
            if (to.sqrMagnitude > 0.01f) v = to.normalized * speed;
            v.y += Mathf.Sin(Time.time * 4f + rb.position.x) * 0.8f;
        }
        rb.linearVelocity = v;
    }
}
