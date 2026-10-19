using System;
using UnityEngine;

// Sprites for the boss hazards, drawn in code so no art assets are needed (the rock can be swapped for real art later).
static class HazardArt
{
    static Sprite circle, rock, square;

    public static Sprite Circle
    {
        get
        {
            if (circle == null)
            {
                const int size = 32;
                var tex = NewTexture(size);
                float r = size * 0.5f - 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
                        tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
                    }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return circle;
        }
    }

    public static Sprite Square
    {
        get
        {
            if (square == null)
            {
                var tex = NewTexture(4);
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, Color.white);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
            }
            return square;
        }
    }

    // A chunky pixel rock, 1 unit wide and tall (scale it up for a bigger rock).
    public static Sprite Rock
    {
        get
        {
            if (rock == null)
            {
                const int size = 24;
                var tex = NewTexture(size);
                var center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
                var light = new Vector2(-0.5f, 0.7f).normalized;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var d = new Vector2(x, y) - center;
                        float angle = Mathf.Atan2(d.y, d.x);
                        // a lumpy outline instead of a perfect circle
                        float edge = 10.6f + Mathf.Sin(angle * 3f + 0.7f) * 1.1f + Mathf.Sin(angle * 5f + 2f) * 0.7f;
                        float dist = d.magnitude;
                        if (dist > edge) { tex.SetPixel(x, y, Color.clear); continue; }

                        Color c;
                        if (dist > edge - 1.4f) c = new Color(0.17f, 0.14f, 0.13f);
                        else
                        {
                            float shade = Vector2.Dot(d.normalized, light) * Mathf.Clamp01(dist / edge) + Mathf.Sin(x * 1.7f + y * 2.3f) * 0.12f;
                            c = shade > 0.35f ? new Color(0.66f, 0.6f, 0.52f)
                              : shade > -0.25f ? new Color(0.5f, 0.45f, 0.4f)
                              : new Color(0.33f, 0.29f, 0.27f);
                            // a couple of cracks
                            if ((x + y * 2) % 11 == 0 && dist < edge - 3f) c *= 0.7f;
                        }
                        tex.SetPixel(x, y, c);
                    }
                tex.Apply();
                rock = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return rock;
        }
    }

    static Texture2D NewTexture(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

    public static SpriteRenderer Make(string name, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }
}

// A big rock thrown up by the boss: it leaves the screen, a shadow marks where it lands, then it drops.
public class FallingRock : MonoBehaviour
{
    const float RockSize = 1.9f;
    const float Radius = 1.2f;

    Boss owner;
    int damage;
    Vector2 start;
    float targetX, groundY, topY;
    float riseEnd, fallEnd, fallTime;
    Action<float> onImpact;
    bool fell;
    Transform shadow;
    SpriteRenderer shadowRenderer;

    public static void Spawn(Boss owner, Vector2 from, Func<float> pickTargetX, float groundY, int damage, float riseTime, float fallTime, Action<float> onImpact)
    {
        var go = HazardArt.Make("BossRock", HazardArt.Rock, Color.white, 6).gameObject;
        go.transform.localScale = Vector3.one * RockSize;
        var rock = go.AddComponent<FallingRock>();
        rock.owner = owner;
        rock.damage = damage;
        rock.start = from;
        rock.groundY = groundY;
        rock.fallTime = fallTime;
        rock.onImpact = onImpact;
        rock.pick = pickTargetX;
        rock.riseStart = Time.time;
        rock.riseEnd = Time.time + riseTime;
        rock.fallEnd = rock.riseEnd + fallTime;

        var cam = Camera.main;
        rock.topY = cam != null ? cam.transform.position.y + cam.orthographicSize + RockSize : from.y + 12f;
        go.transform.position = new Vector3(from.x, from.y, 0f);
    }

    Func<float> pick;
    float riseStart;

    void Update()
    {
        if (owner == null)
        {
            Cleanup();
            return;
        }

        if (Time.time < riseEnd)
        {
            float t = Mathf.InverseLerp(riseStart, riseEnd, Time.time);
            transform.position = new Vector3(start.x, Mathf.Lerp(start.y, topY, t * t), 0f);
            return;
        }

        if (!fell)
        {
            // the landing spot is chosen when the rock leaves the screen
            fell = true;
            targetX = pick();
            shadowRenderer = HazardArt.Make("BossRockShadow", HazardArt.Circle, new Color(0f, 0f, 0f, 0.3f), 2);
            shadow = shadowRenderer.transform;
            shadow.position = new Vector3(targetX, groundY + 0.04f, 0f);
        }

        float f = Mathf.Clamp01(1f - (fallEnd - Time.time) / fallTime);
        float restY = groundY + RockSize * 0.5f;
        transform.position = new Vector3(targetX, Mathf.Lerp(topY, restY, f * f), 0f);
        float width = Mathf.Lerp(1.0f, Radius * 2f, f);
        shadow.localScale = new Vector3(width, width * 0.3f, 1f);
        shadowRenderer.color = new Color(0.35f, 0f, 0f, Mathf.Lerp(0.25f, 0.6f, f));

        if (f >= 1f) Impact();
    }

    void Impact()
    {
        var gm = GameManager.Instance;
        var p = gm != null ? gm.Player : null;
        if (p != null && !p.Dead)
        {
            var b = p.Col.bounds;
            if (Mathf.Abs(b.center.x - targetX) <= Radius + b.extents.x * 0.5f && b.min.y < groundY + RockSize) p.TakeDamage(damage);
        }

        Dust.Spawn(new Vector2(targetX, groundY + 0.3f), 1.6f);
        onImpact?.Invoke(targetX);
        Cleanup();
    }

    void Cleanup()
    {
        if (shadow != null) Destroy(shadow.gameObject);
        Destroy(gameObject);
    }
}

// A low wave of dust that runs along the ground after the boss lands; jump over it.
public class Shockwave : MonoBehaviour
{
    const float Speed = 9f, Height = 0.7f, Width = 0.9f, MaxTravel = 24f;

    Boss owner;
    int damage;
    float dir, originX, groundY;

    public static void Spawn(Boss owner, float x, float groundY, float dir, int damage)
    {
        var sr = HazardArt.Make("BossShockwave", HazardArt.Square, new Color(0.85f, 0.75f, 0.55f, 0.9f), 4);
        sr.transform.localScale = new Vector3(Width, Height, 1f);
        var wave = sr.gameObject.AddComponent<Shockwave>();
        wave.owner = owner;
        wave.damage = damage;
        wave.dir = dir;
        wave.originX = x;
        wave.groundY = groundY;
        sr.transform.position = new Vector3(x, groundY + Height * 0.5f, 0f);
    }

    void Update()
    {
        float x = transform.position.x + dir * Speed * Time.deltaTime;
        transform.position = new Vector3(x, groundY + Height * 0.5f, 0f);
        if (owner == null || Mathf.Abs(x - originX) > MaxTravel)
        {
            Destroy(gameObject);
            return;
        }

        var gm = GameManager.Instance;
        var p = gm != null ? gm.Player : null;
        if (p == null || p.Dead) return;
        var wave = new Bounds(transform.position, new Vector3(Width, Height, 1f));
        var b = p.Col.bounds;
        if (b.min.x < wave.max.x && b.max.x > wave.min.x && b.min.y < wave.max.y) p.TakeDamage(damage);
    }
}

// A short puff that grows and fades (rock impact, boss landing).
public class Dust : MonoBehaviour
{
    float start, life = 0.35f, size;
    SpriteRenderer sr;

    public static void Spawn(Vector2 pos, float size)
    {
        var sr = HazardArt.Make("Dust", HazardArt.Circle, new Color(0.8f, 0.72f, 0.58f, 0.8f), 5);
        sr.transform.position = new Vector3(pos.x, pos.y, 0f);
        var d = sr.gameObject.AddComponent<Dust>();
        d.sr = sr;
        d.size = size;
        d.start = Time.time;
    }

    void Update()
    {
        float t = (Time.time - start) / life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.localScale = Vector3.one * Mathf.Lerp(size * 0.4f, size, t);
        var c = sr.color;
        c.a = 0.8f * (1f - t);
        sr.color = c;
    }
}
