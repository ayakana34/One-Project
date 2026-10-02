using UnityEngine;
using UnityEngine.InputSystem;

// Test-only controller for previewing a 3D character in a 2D side-view scene.
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class CharacterTestController : MonoBehaviour
{
    public Transform visual;
    public Animator animator;
    public float moveSpeed = 7f;
    public float jumpSpeed = 14f;

    Rigidbody2D body;
    CapsuleCollider2D capsule;
    float facing = 1f;
    float yaw = 90f;
    bool grounded;

    // Optional scripted input (used by the automatic screenshot check).
    public float? scriptedInput;
    public bool scriptedJump;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        float input = scriptedInput ?? (kb == null ? 0f : (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f));
        bool jumpPressed = scriptedJump || (kb != null && kb.spaceKey.wasPressedThisFrame);
        scriptedJump = false;
        grounded = IsGrounded();

        var v = body.linearVelocity;
        v.x = input * moveSpeed;
        if (grounded && jumpPressed) v.y = jumpSpeed;
        body.linearVelocity = v;

        if (input != 0f) facing = Mathf.Sign(input);
        if (visual != null)
        {
            yaw = facing * 90f;
            visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        if (animator != null)
        {
            animator.SetBool("Run", Mathf.Abs(input) > 0.01f);
            animator.SetBool("Grounded", grounded);
        }
    }

    bool IsGrounded()
    {
        Bounds b = capsule.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y - 0.05f);
        Vector2 size = new Vector2(b.size.x * 0.8f, 0.1f);
        foreach (var c in Physics2D.OverlapBoxAll(center, size, 0f))
        {
            if (c == capsule || c.isTrigger) continue;
            return true;
        }
        return false;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(12, 8, 400, 24), "A/D move   Space jump   (character test scene)");
    }
}
