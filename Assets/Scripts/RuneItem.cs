using UnityEngine;

// The recall rune: a glowing diamond that floats above its pedestal. Touching it unlocks the recall skill.
public class RuneItem : MonoBehaviour
{
    Transform glow, diamond;
    SpriteRenderer glowRenderer;
    float baseY;
    bool placed;

    void Awake()
    {
        var box = gameObject.AddComponent<CircleCollider2D>();
        box.isTrigger = true;
        box.radius = 0.8f;

        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(transform, false);
        glowRenderer = glowGo.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = HazardArt.Circle;
        glowRenderer.color = new Color(0.4f, 0.9f, 1f, 0.25f);
        glowRenderer.sortingOrder = -3;
        glow = glowGo.transform;

        var diamondGo = new GameObject("Diamond");
        diamondGo.transform.SetParent(transform, false);
        var sr = diamondGo.AddComponent<SpriteRenderer>();
        sr.sprite = HazardArt.Square;
        sr.color = new Color(0.55f, 0.95f, 1f);
        sr.sortingOrder = -2;
        diamond = diamondGo.transform;
        diamond.localScale = new Vector3(0.5f, 0.7f, 1f);
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void OnEnable() => placed = false;

    void Update()
    {
        if (!placed)
        {
            baseY = transform.position.y;
            placed = true;
        }
        float t = Time.unscaledTime;
        transform.position = new Vector3(transform.position.x, baseY + Mathf.Sin(t * 2.2f) * 0.12f, 0f);
        float pulse = 1.5f + Mathf.Sin(t * 3f) * 0.25f;
        glow.localScale = Vector3.one * pulse;
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(t * 1.4f) * 8f);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (GameManager.Instance != null && other.GetComponentInParent<Player>() != null)
            GameManager.Instance.OnRuneCollected();
    }
}
