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
public class StageMapDef
{
    public string name;
    public float minX, maxX;
    public float playerStartX;
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
    public const int MaxPlatforms = 8;
    public const int MaxBlocks = 12;

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

    // Stage 1: two layouts (round 1 uses A, round 2 uses B, then A again ...). Ground and air monsters are mixed.
    public static readonly StageMapDef[] Stage1 =
    {
        // A: stairs up to a plateau, a low valley, a hill with a peak, then a few pillars before the exit.
        new StageMapDef
        {
            name = "Stage 1 A", minX = -20f, maxX = 20f, playerStartX = -17f,
            blocks = new[]
            {
                B(-12.5f, -5.0f, 3f), B(-10f, -4.0f, 2f), B(-8f, -3.0f, 2f), B(-5f, -3.0f, 4f),
                B(-2f, -4.0f, 2f), B(0f, -5.0f, 2f),
                B(6.5f, -4.5f, 5f), B(6.5f, -3.0f, 2f),
                B(11.5f, -4.4f, 1.8f), B(13.5f, -3.2f, 2.2f), B(15.5f, -4.4f, 1.8f)
            },
            platforms = new[]
            {
                P(-5f, Tier2, 3.2f), P(2.5f, Tier1, 3f), P(6.5f, -0.8f, 3f), P(13.5f, -1.8f, 3.5f)
            },
            enemies = new[]
            {
                G(-5.5f), G(2f), G(5f), G(15.5f),
                E(3.5f), E(10f),
                F(-8f, 2.5f), F(8f, 3f),
                S(0f)
            }
        },
        // B: a shelf with a tower, a platform ladder over the valley, then stepped mounds.
        new StageMapDef
        {
            name = "Stage 1 B", minX = -20f, maxX = 20f, playerStartX = -17f,
            blocks = new[]
            {
                B(-13f, -5.0f, 3f), B(-10.5f, -4.0f, 2f),
                B(-3.5f, -4.6f, 5f), B(-4f, -3.0f, 2f),
                B(6f, -4.8f, 4f), B(8.5f, -3.6f, 3f), B(11f, -4.8f, 2f),
                B(15.5f, -4.5f, 2f)
            },
            platforms = new[]
            {
                P(-10.5f, -1.8f, 3f), P(-4f, -0.8f, 3f), P(1f, Tier1, 3.5f), P(1f, Tier2, 3.5f),
                P(1f, Tier3, 3f), P(8.5f, Tier2, 3.5f), P(14f, Tier1, 3f)
            },
            enemies = new[]
            {
                G(-7.5f), G(-3.5f), G(6f), G(13.5f),
                E(1f), E(8.5f),
                F(-9f, 2.5f), F(2f, 3.5f),
                S(10f)
            }
        }
    };

    // Stage 2: longer maps with more monsters.
    public static readonly StageMapDef[] Stage2 =
    {
        // A: a long staircase up, a plateau, a ruin of pillars and a low shelf with a tower.
        new StageMapDef
        {
            name = "Stage 2 A", minX = -22f, maxX = 22f, playerStartX = -19f,
            blocks = new[]
            {
                B(-15f, -5.0f, 3f), B(-12.5f, -4.0f, 2f), B(-10.5f, -3.0f, 2f), B(-7f, -3.0f, 5f),
                B(-4f, -4.2f, 2f), B(-2f, -5.2f, 2f),
                B(4f, -4.4f, 1.6f), B(8f, -4.0f, 2f),
                B(12f, -4.6f, 6f), B(13f, -3.2f, 2f), B(17f, -4.4f, 2.5f)
            },
            platforms = new[]
            {
                P(-7f, Tier2, 3.5f), P(1.5f, Tier1, 3.5f), P(1.5f, Tier2, 3f), P(8f, -1.6f, 3.5f),
                P(12.5f, -0.8f, 3.5f), P(17f, -2.0f, 3f)
            },
            enemies = new[]
            {
                G(-6f), G(0.5f), G(10.5f), G(16.5f),
                E(1.5f), E(11f),
                F(-8f, 3f), F(-1f, 2.5f), F(9f, 3.5f), F(18f, 2.5f),
                S(-5f), S(6f), S(15f)
            }
        },
        // B: a dip, a big mountain with a peak in the middle and stepped ground on the right.
        new StageMapDef
        {
            name = "Stage 2 B", minX = -22f, maxX = 22f, playerStartX = -19f,
            blocks = new[]
            {
                B(-16f, -5.0f, 4f), B(-12.5f, -4.0f, 3f), B(-9.5f, -5.0f, 3f),
                B(0f, -4.6f, 11f), B(0f, -3.0f, 4f),
                B(11f, -4.0f, 3f), B(14f, -5.0f, 3f), B(17.5f, -4.4f, 2f)
            },
            platforms = new[]
            {
                P(-16f, -2.6f, 3f), P(-12.5f, -1.6f, 3f), P(-7f, Tier1, 3f), P(0f, -0.6f, 4f),
                P(7f, Tier1, 3.5f), P(11f, -1.6f, 3f), P(14.5f, -2.6f, 3f)
            },
            enemies = new[]
            {
                G(-6.5f), G(8f), G(14f),
                E(-1f), E(11f),
                F(-13f, 3f), F(-4f, 3.5f), F(7f, 2.5f), F(16f, 3.5f),
                S(-8f), S(3f), S(13f)
            }
        }
    };

    // Stage 3 (boss): the single-screen arena.
    public static readonly StageMapDef Arena = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        blocks = new Vector3[0], platforms = new Vector3[0], enemies = new EnemySpawn[0]
    };

    public static readonly StageMapDef ArenaWithPlatforms = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
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
