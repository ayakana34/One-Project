import io
import gen_maps as g

# the rune sits on the top tier; nothing hovers above it
g.rr.plats = [p for p in g.rr.plats if p != (12, 1.0, 3.5)]


def f(v):
    s = ("%.2f" % v).rstrip("0").rstrip(".")
    if s == "-0":
        s = "0"
    return s + "f"


def tri(t):
    return "B(%s, %s, %s)" % (f(t[0]), f(t[1]), f(t[2]))


def tri_p(t):
    return "P(%s, %s, %s)" % (f(t[0]), f(t[1]), f(t[2]))


def enemy(e):
    k, x, a = e
    if k == "G": return "G(%s)" % f(x)
    if k == "Gp": return "Gp(%s, %s)" % (f(x), f(a))
    if k == "E": return "E(%s)" % f(x)
    if k == "F": return "F(%s, %s)" % (f(x), f(a))
    return "S(%s)" % f(x)


def wrap(items, per=4, indent="                "):
    lines = []
    for i in range(0, len(items), per):
        lines.append(indent + ", ".join(items[i:i + per]))
    return ",\n".join(lines)


def color(c):
    return "new Color(%s, %s, %s)" % tuple(f(v) for v in c)


def mapdef(var, m, comment):
    rune = ""
    if m.rune:
        rune = "\n        runePos = new Vector2(%s, %s)," % (f(m.rune[0]), f(m.rune[1]))
    def arr(items, typ):
        if not items:
            return "new %s[0]" % typ
        nl = chr(10)
        return "new[]" + nl + "        {" + nl + wrap(items) + nl + "        }"

    return """    // %s
    public static readonly StageMapDef %s = new StageMapDef
    {
        name = "%s", minX = %s, maxX = %s, playerStartX = %s,
        backgroundColor = %s,
        groundColor = %s,
        stripeColor = %s,%s
        blocks = %s,
        platforms = %s,
        enemies = %s
    };
""" % (comment, var, m.name, f(m.mn), f(m.mx), f(m.start), color(m.theme[0]), color(m.theme[1]), color(m.theme[2]), rune,
       arr([tri(b) for b in m.blocks], "Vector3"), arr([tri_p(p) for p in m.plats], "Vector3"), arr([enemy(e) for e in m.enemies], "EnemySpawn"))


head = '''using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Grunt, Elite, Flyer, Shooter }

public struct EnemySpawn
{
    public EnemyKind kind;
    public float x, y;
    public bool onGround;  // y is then the height above the surface the monster stands on
    public float top;      // surface height; NaN = whatever terrain is at x
}

// Hand-made stage layouts: solid terrain (steps, hills, pillars), one-way platforms and a fixed set of monsters.
// There are no pits or spikes on purpose: the thrown weapon must never get lost, so the floor is closed everywhere and
// every surface can be reached by jumping (jump height is about 2.8, steps and platform gaps stay at 2.5 or less).
// The three long maps are generated from a design script that checks reachability; StageMaps.Validate repeats the check.
public class StageMapDef
{
    public string name;
    public float minX, maxX;
    public float playerStartX;
    public Color backgroundColor = new Color(0.17f, 0.18f, 0.22f);
    public Color groundColor = new Color(0.36f, 0.37f, 0.43f);
    public Color stripeColor = new Color(0.22f, 0.22f, 0.26f);
    public Vector2? runePos;    // the recall rune of the rune room
    public Vector3[] blocks;    // solid terrain standing on the floor: x, top y, width
    public Vector3[] platforms; // one-way platforms: x, top y, width
    public EnemySpawn[] enemies;

    public float Width => maxX - minX;
    public float CenterX => (minX + maxX) * 0.5f;

    // Height of the ground at x (floor or the highest solid block there).
    public float SurfaceTop(float x)
    {
        float top = StageMaps.FloorTop;
        if (blocks == null) return top;
        foreach (var b in blocks)
            if (Mathf.Abs(x - b.x) <= b.z * 0.5f && b.y > top) top = b.y;
        return top;
    }
}

public static class StageMaps
{
    public const float FloorTop = -6f;
    public const float Tier1 = -3.6f, Tier2 = -1.2f, Tier3 = 1.2f;
    public const int MaxPlatforms = 16;
    public const int MaxBlocks = 24;

    const float ShooterHoverY = 4.3f;
    const float MaxClimb = 2.5f; // the player jumps about 2.8 high
    const float MaxGap = 3.5f;   // horizontal distance that can be jumped over
    const float MaxWall = 2.3f;  // highest step a walking monster hops up

    static Vector3 P(float x, float top, float width) => new Vector3(x, top, width);
    static Vector3 B(float x, float top, float width) => new Vector3(x, top, width);

    static EnemySpawn Spawn(EnemyKind kind, float x, float y) => new EnemySpawn { kind = kind, x = x, y = y, top = float.NaN };

    // Ground monsters stand on whatever terrain is at x (or on a platform when a top height is given).
    static EnemySpawn Ground(EnemyKind kind, float x, float offset, float top)
    {
        var s = Spawn(kind, x, offset);
        s.onGround = true;
        s.top = top;
        return s;
    }
    static EnemySpawn G(float x) => Ground(EnemyKind.Grunt, x, 0.45f, float.NaN);
    static EnemySpawn Gp(float x, float platformTop) => Ground(EnemyKind.Grunt, x, 0.45f, platformTop);
    static EnemySpawn E(float x) => Ground(EnemyKind.Elite, x, 0.65f, float.NaN);
    static EnemySpawn F(float x, float y) => Spawn(EnemyKind.Flyer, x, y);
    static EnemySpawn S(float x) => Spawn(EnemyKind.Shooter, x, ShooterHoverY);

'''

tail = '''
    // The boss arena: a single screen.
    public static readonly StageMapDef Arena = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        backgroundColor = new Color(0.22f, 0.12f, 0.13f), groundColor = new Color(0.46f, 0.3f, 0.3f), stripeColor = new Color(0.28f, 0.16f, 0.17f),
        blocks = new Vector3[0], platforms = new Vector3[0], enemies = new EnemySpawn[0]
    };

    public static readonly StageMapDef ArenaWithPlatforms = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        backgroundColor = new Color(0.22f, 0.12f, 0.13f), groundColor = new Color(0.46f, 0.3f, 0.3f), stripeColor = new Color(0.28f, 0.16f, 0.17f),
        blocks = new Vector3[0],
        platforms = new[] { P(-5f, Tier1, 4f), P(5f, Tier1, 4f), P(0f, Tier2, 4f) },
        enemies = new EnemySpawn[0]
    };

    // Design check (used by the test harness): everything has to be reachable and nothing may overlap the start or the exit.
    public static List<string> Validate(StageMapDef map)
    {
        var problems = new List<string>();
        if (map.blocks.Length > MaxBlocks) problems.Add("too many blocks " + map.blocks.Length);
        if (map.platforms.Length > MaxPlatforms) problems.Add("too many platforms " + map.platforms.Length);

        // surfaces: floor, blocks, platforms as (min x, max x, top)
        var surfaces = new List<Vector3> { new Vector3(map.minX, map.maxX, FloorTop) };
        foreach (var b in map.blocks) surfaces.Add(new Vector3(b.x - b.z * 0.5f, b.x + b.z * 0.5f, b.y));
        foreach (var p in map.platforms) surfaces.Add(new Vector3(p.x - p.z * 0.5f, p.x + p.z * 0.5f, p.y));

        var reached = new bool[surfaces.Count];
        reached[0] = true;
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.Count > 0)
        {
            var a = surfaces[queue.Dequeue()];
            for (int i = 0; i < surfaces.Count; i++)
            {
                if (reached[i]) continue;
                var s = surfaces[i];
                float gap = Mathf.Max(0f, Mathf.Max(a.x, s.x) - Mathf.Min(a.y, s.y));
                if (gap <= MaxGap && s.z - a.z <= MaxClimb)
                {
                    reached[i] = true;
                    queue.Enqueue(i);
                }
            }
        }
        for (int i = 0; i < surfaces.Count; i++)
            if (!reached[i]) problems.Add("unreachable surface x=" + surfaces[i].x.ToString("F1") + ".." + surfaces[i].y.ToString("F1") + " top=" + surfaces[i].z.ToString("F1"));

        // walls: the ground may not step by more than a monster can hop (the same height protects the player)
        for (float x = map.minX; x < map.maxX - 0.1f; x += 0.1f)
        {
            float diff = Mathf.Abs(map.SurfaceTop(x + 0.1f) - map.SurfaceTop(x));
            if (diff > MaxWall)
            {
                problems.Add("wall of " + diff.ToString("F1") + " at x=" + x.ToString("F1"));
                x += 0.5f;
            }
        }

        // platforms need head room above the terrain below them
        foreach (var p in map.platforms)
            foreach (var b in map.blocks)
                if (Mathf.Abs(p.x - b.x) < (p.z + b.z) * 0.5f && p.y - 0.4f < b.y + 1.1f)
                    problems.Add("platform x=" + p.x.ToString("F1") + " is too low above the block at x=" + b.x.ToString("F1"));

        float exitX = map.maxX - 1.5f;
        if (map.SurfaceTop(map.playerStartX) > FloorTop + 0.01f) problems.Add("player start is not on the floor");
        for (float x = exitX - 1.5f; x <= exitX + 1.5f; x += 0.5f)
            if (map.SurfaceTop(x) > FloorTop + 0.01f)
            {
                problems.Add("terrain at the exit");
                break;
            }

        foreach (var e in map.enemies)
        {
            if (!e.onGround || !float.IsNaN(e.top)) continue;
            foreach (var b in map.blocks)
                if (Mathf.Abs(Mathf.Abs(e.x - b.x) - b.z * 0.5f) < 0.45f)
                {
                    problems.Add("monster at x=" + e.x.ToString("F1") + " stands on a block edge");
                    break;
                }
        }
        return problems;
    }
}
'''

body = (mapdef("Stage1", g.s1, "Stage 1: the outpost. Low terrain only (the recall skill comes later): wide ledges and gentle bumps, no pits or narrow slots.")
        + chr(10) + mapdef("Stage2", g.s2, "Stage 2: the citadel. A rampart, a moat with a two-tier mound, the keep (low wall, raised hall, tall tower) and a stepped way down.")
        + chr(10) + mapdef("RuneRoom", g.rr, "Stage 3: the rune room. A short flat corridor without monsters: take the recall rune, then leave through the exit."))

io.open("C:/Users/KGA01/UnityProject/One-Project/Assets/Scripts/StageMaps.cs", "w", encoding="utf-8", newline="").write(head + body + tail)
print("written")
