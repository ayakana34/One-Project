using System.Collections.Generic;
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

    // Stage 1: the outskirts. A hill with a peak, rocks, a ruin of pillars, a stepped mesa and a platform archipelago.
    public static readonly StageMapDef Stage1 = new StageMapDef
    {
        name = "Stage 1", minX = -36f, maxX = 36f, playerStartX = -33f,
        backgroundColor = new Color(0.17f, 0.18f, 0.24f),
        groundColor = new Color(0.36f, 0.38f, 0.46f),
        stripeColor = new Color(0.22f, 0.23f, 0.3f),
        blocks = new[]
        {
                B(-24f, -4.7f, 9f), B(-24f, -3.1f, 5f), B(-16f, -5.2f, 2f), B(-13f, -4.6f, 2.4f),
                B(-10f, -5f, 1.8f), B(-5f, -4f, 1.6f), B(-1f, -3.8f, 1.6f), B(3f, -4.2f, 1.6f),
                B(12f, -4.2f, 10f), B(12f, -2.6f, 6.6f), B(12f, -1f, 3.2f), B(20f, -5f, 2.5f),
                B(24f, -4.4f, 2.5f), B(28f, -5f, 2.5f)
        },
        platforms = new[]
        {
                P(-24f, -0.8f, 4f), P(-17f, -3.4f, 3f), P(-12.5f, -2.8f, 3f), P(-3f, -1.4f, 5.5f),
                P(3f, -1.6f, 3f), P(12f, 0.6f, 5f), P(19f, -3.4f, 3f), P(23f, -1.6f, 3f),
                P(27f, -3.2f, 3f)
        },
        enemies = new[]
        {
                G(-20.5f), G(-13f), G(-6.5f), G(1f),
                G(7.8f), G(16.5f), G(26f), E(-1f),
                E(12f), E(24f), F(-18f, 3f), F(-3f, 3.5f),
                F(15f, 3.5f), F(22f, 2.5f), S(-12f), S(20f)
        }
    };

    // Stage 2: the ruined citadel. A big hill, a colonnade, a rampart with towers, floating gardens, the citadel and a descent.
    public static readonly StageMapDef Stage2 = new StageMapDef
    {
        name = "Stage 2", minX = -46f, maxX = 46f, playerStartX = -43f,
        backgroundColor = new Color(0.2f, 0.16f, 0.21f),
        groundColor = new Color(0.44f, 0.36f, 0.44f),
        stripeColor = new Color(0.27f, 0.21f, 0.28f),
        blocks = new[]
        {
                B(-33f, -4.6f, 12f), B(-33f, -3f, 8f), B(-33f, -1.4f, 4f), B(-24f, -4f, 1.6f),
                B(-20f, -3.8f, 1.6f), B(-16f, -4.2f, 1.6f), B(-6f, -4.8f, 12f), B(-6f, -3.2f, 8f),
                B(-8f, -1.6f, 2f), B(-4f, -1.6f, 2f), B(3f, -5.3f, 2f), B(9f, -5.1f, 2.4f),
                B(23f, -4.4f, 12f), B(23f, -2.8f, 8f), B(23f, -1.2f, 4f), B(35f, -4.8f, 3f),
                B(38.5f, -4.2f, 2.4f)
        },
        platforms = new[]
        {
                P(-33f, 0.2f, 4f), P(-20f, -1.4f, 6f), P(-6f, 0.4f, 3f), P(2f, -3.6f, 3f),
                P(5f, -1.2f, 3f), P(8f, 1.2f, 3f), P(11f, -1.2f, 3f), P(23f, 0.4f, 4f),
                P(16f, -2f, 3f), P(30f, -2f, 3f), P(35f, -2.6f, 3.2f), P(39f, -2f, 2.5f)
        },
        enemies = new[]
        {
                G(-36f), G(-26f), G(-22f), G(-13f),
                G(-1f), G(7f), G(26f), G(36f),
                E(-33f), E(-6f), E(23f), E(38.5f),
                F(-30f, 3f), F(-18f, 3.5f), F(-4f, 3f), F(6f, 3.5f),
                F(14f, 2.5f), F(28f, 3f), F(34f, 3.5f), S(-25f),
                S(0f), S(19f), S(32f)
        }
    };

    // Stage 3: the rune room. Climb the stepped mound to the recall rune, then leave through the exit.
    public static readonly StageMapDef RuneRoom = new StageMapDef
    {
        name = "Rune", minX = -30f, maxX = 30f, playerStartX = -27f,
        backgroundColor = new Color(0.12f, 0.19f, 0.2f),
        groundColor = new Color(0.32f, 0.46f, 0.46f),
        stripeColor = new Color(0.17f, 0.26f, 0.27f),
        runePos = new Vector2(12f, -0.6f),
        blocks = new[]
        {
                B(-20f, -5f, 3f), B(-14f, -4.4f, 2.4f), B(-3f, -4.8f, 3f), B(0f, -3.8f, 3f),
                B(12f, -4.8f, 14f), B(12f, -3.4f, 10.8f), B(12f, -2f, 7.6f), B(12f, -0.6f, 4.4f),
                B(22f, -5f, 2.4f)
        },
        platforms = new[]
        {
                P(-17f, -3f, 3.5f), P(-8f, -3f, 3f), P(3f, -2f, 3f)
        },
        enemies = new[]
        {
                G(-17f), G(-9f), G(2.5f), E(6f),
                F(-12f, 3f), F(4f, 3.5f), S(-5f), S(16f)
        }
    };

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
