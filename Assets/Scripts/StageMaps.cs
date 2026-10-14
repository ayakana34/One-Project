using UnityEngine;

public enum EnemyKind { Grunt, Elite, Flyer, Shooter }

public struct EnemySpawn
{
    public EnemyKind kind;
    public float x, y;
}

// Hand-made stage layouts: platforms and a fixed set of monsters. The floor top is y = -6; platform tops use three
// tiers that can be reached by jumping.
public class StageMapDef
{
    public string name;
    public float minX, maxX;
    public float playerStartX;
    public Vector3[] platforms; // x, top y, width
    public EnemySpawn[] enemies;

    public float Width => maxX - minX;
    public float CenterX => (minX + maxX) * 0.5f;
}

public static class StageMaps
{
    public const float Tier1 = -3.6f, Tier2 = -1.2f, Tier3 = 1.2f;
    public const int MaxPlatforms = 8;

    const float FloorTop = -6f;
    const float ShooterHoverY = 4.3f;

    static Vector3 P(float x, float top, float width) => new Vector3(x, top, width);

    static EnemySpawn Spawn(EnemyKind kind, float x, float y) => new EnemySpawn { kind = kind, x = x, y = y };

    // Ground monsters stand on the floor (or on a platform when a top height is given).
    static EnemySpawn G(float x) => Spawn(EnemyKind.Grunt, x, FloorTop + 0.45f);
    static EnemySpawn Gp(float x, float platformTop) => Spawn(EnemyKind.Grunt, x, platformTop + 0.45f);
    static EnemySpawn E(float x) => Spawn(EnemyKind.Elite, x, FloorTop + 0.65f);
    static EnemySpawn F(float x, float y) => Spawn(EnemyKind.Flyer, x, y);
    static EnemySpawn S(float x) => Spawn(EnemyKind.Shooter, x, ShooterHoverY);

    // Stage 1: two layouts (round 1 uses A, round 2 uses B, then A again ...). Ground and air monsters are mixed.
    public static readonly StageMapDef[] Stage1 =
    {
        new StageMapDef
        {
            name = "Stage 1 A", minX = -20f, maxX = 20f, playerStartX = -17f,
            platforms = new[]
            {
                P(-13f, Tier1, 4f), P(-8f, Tier2, 4f), P(-2f, Tier1, 5f),
                P(4f, Tier2, 4f), P(9f, Tier1, 4f), P(14f, Tier2, 5f)
            },
            enemies = new[]
            {
                G(-10f), G(-5f), Gp(-2f, Tier1), G(7f),
                E(2f), E(14f),
                F(-4f, 2.5f), F(10f, 3f),
                S(6f)
            }
        },
        new StageMapDef
        {
            name = "Stage 1 B", minX = -20f, maxX = 20f, playerStartX = -17f,
            platforms = new[]
            {
                P(-14f, Tier1, 5f), P(-9f, Tier2, 3.5f), P(-4f, Tier1, 4f), P(1f, Tier2, 5f),
                P(1f, Tier3, 3.5f), P(7f, Tier1, 4f), P(12f, Tier2, 4f), P(16f, Tier1, 3.5f)
            },
            enemies = new[]
            {
                G(-11f), G(-6.5f), Gp(7f, Tier1), G(10f),
                E(0f), E(13f),
                F(-8f, 2.5f), F(4f, 3.5f),
                S(9f)
            }
        }
    };

    // Stage 2: longer maps with more monsters.
    public static readonly StageMapDef[] Stage2 =
    {
        new StageMapDef
        {
            name = "Stage 2 A", minX = -22f, maxX = 22f, playerStartX = -19f,
            platforms = new[]
            {
                P(-15f, Tier1, 4f), P(-10f, Tier2, 4f), P(-4f, Tier1, 4f), P(2f, Tier2, 5f),
                P(8f, Tier1, 4f), P(13f, Tier2, 4f), P(18f, Tier1, 4f)
            },
            enemies = new[]
            {
                G(-12f), Gp(-4f, Tier1), G(4f), G(14f),
                E(0f), E(11f),
                F(-8f, 3f), F(-1f, 2.5f), F(9f, 3.5f), F(17f, 2.5f),
                S(-5f), S(6f), S(15f)
            }
        },
        new StageMapDef
        {
            name = "Stage 2 B", minX = -22f, maxX = 22f, playerStartX = -19f,
            platforms = new[]
            {
                P(-16f, Tier1, 5f), P(-11f, Tier2, 3.5f), P(-6f, Tier1, 4f), P(-1f, Tier2, 4f),
                P(-1f, Tier3, 3.5f), P(5f, Tier1, 5f), P(11f, Tier2, 4f), P(16f, Tier1, 4f)
            },
            enemies = new[]
            {
                G(-9f), Gp(5f, Tier1), Gp(16f, Tier1),
                E(0f), E(12f),
                F(-13f, 3f), F(-3f, 3.5f), F(7f, 2.5f), F(15f, 3.5f),
                S(-8f), S(2f), S(12f)
            }
        }
    };

    // Stage 3 (boss): the single-screen arena.
    public static readonly StageMapDef Arena = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        platforms = new Vector3[0], enemies = new EnemySpawn[0]
    };

    public static readonly StageMapDef ArenaWithPlatforms = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        platforms = new[] { P(-5f, Tier1, 4f), P(5f, Tier1, 4f), P(0f, Tier2, 4f) },
        enemies = new EnemySpawn[0]
    };
}
