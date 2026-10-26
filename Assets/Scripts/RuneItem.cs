using UnityEngine;

// The recall rune: a glowing diamond with the rune letter Raido (ᚱ, "journey") on it, floating above its pedestal.
// Touching it unlocks the recall skill.
public class RuneItem : MonoBehaviour
{
    // The letter as strokes, in units of a diamond that is 0.5 wide and 0.7 tall (y up): stem, bowl top, bowl bottom, leg.
    // Shared with the pickup card, which draws the same emblem in UI units.
    public static readonly Vector2[] GlyphPoints =
    {
        new Vector2(-0.065f, -0.2f), new Vector2(-0.065f, 0.2f), new Vector2(0.08f, 0.086f),
        new Vector2(-0.065f, -0.029f), new Vector2(0.086f, -0.2f),
    };
    public static readonly int[,] GlyphStrokes = { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 } };
    public const float GlyphThickness = 0.05f;
    public static readonly Color DiamondColor = new Color(0.55f, 0.95f, 1f);
    public static readonly Color GlyphInk = new Color(0.055f, 0.2f, 0.26f);

    // Drawn larger than the first, plain diamond so the letter can be read from across the screen.
    const float VisualScale = 1.4f;

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
        sr.color = DiamondColor;
        sr.sortingOrder = -2;
        diamond = diamondGo.transform;
        diamond.localScale = new Vector3(0.5f, 0.7f, 1f) * VisualScale;
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // the letter stays upright while the diamond sways, so it hangs on the rune itself, not on the diamond
        var glyphGo = new GameObject("Glyph");
        glyphGo.transform.SetParent(transform, false);
        for (int i = 0; i < GlyphStrokes.GetLength(0); i++)
        {
            Vector2 a = GlyphPoints[GlyphStrokes[i, 0]] * VisualScale, b = GlyphPoints[GlyphStrokes[i, 1]] * VisualScale;
            float thick = GlyphThickness * VisualScale;
            var stroke = new GameObject("Stroke");
            stroke.transform.SetParent(glyphGo.transform, false);
            stroke.transform.localPosition = (a + b) * 0.5f;
            stroke.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            stroke.transform.localScale = new Vector3(Vector2.Distance(a, b) + thick, thick, 1f);
            var ssr = stroke.AddComponent<SpriteRenderer>();
            ssr.sprite = HazardArt.Square;
            ssr.color = GlyphInk;
            ssr.sortingOrder = -1;
        }
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
        float pulse = (1.5f + Mathf.Sin(t * 3f) * 0.25f) * VisualScale;
        glow.localScale = Vector3.one * pulse;
        diamond.localRotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(t * 1.4f) * 8f);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (GameManager.Instance != null && other.GetComponentInParent<Player>() != null)
            GameManager.Instance.OnRuneCollected();
    }
}
