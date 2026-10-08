using UnityEngine;

// Hand-made stage layouts. The floor top is y = -6; platform tops use three tiers that can be reached by jumping.
public class StageMapDef
{
    public string name;
    public float minX, maxX;
    public float playerStartX;
    public Vector3[] platforms; // x, top y, width

    public float Width => maxX - minX;
    public float CenterX => (minX + maxX) * 0.5f;
}

public static class StageMaps
{
    public const float Tier1 = -3.6f, Tier2 = -1.2f, Tier3 = 1.2f;

    static Vector3 P(float x, float top, float width) => new Vector3(x, top, width);

    // Stage 1 (ground monsters): wide maps, walk right to the exit.
    public static readonly StageMapDef[] Ground =
    {
        new StageMapDef
        {
            name = "Ground A", minX = -20f, maxX = 20f, playerStartX = -17f,
            platforms = new[]
            {
                P(-13f, Tier1, 4f), P(-8f, Tier2, 4f), P(-2f, Tier1, 5f),
                P(4f, Tier2, 4f), P(9f, Tier1, 4f), P(14f, Tier2, 5f)
            }
        },
        new StageMapDef
        {
            name = "Ground B", minX = -20f, maxX = 20f, playerStartX = -17f,
            platforms = new[]
            {
                P(-14f, Tier1, 5f), P(-9f, Tier2, 3.5f), P(-4f, Tier1, 4f), P(1f, Tier2, 5f),
                P(1f, Tier3, 3.5f), P(7f, Tier1, 4f), P(12f, Tier2, 4f), P(16f, Tier1, 3.5f)
            }
        }
    };

    // Stage 2 (air monsters): longer maps with more platforms to fight from.
    public static readonly StageMapDef[] Air =
    {
        new StageMapDef
        {
            name = "Air A", minX = -22f, maxX = 22f, playerStartX = -19f,
            platforms = new[]
            {
                P(-15f, Tier1, 4f), P(-10f, Tier2, 4f), P(-4f, Tier1, 4f), P(2f, Tier2, 5f),
                P(8f, Tier1, 4f), P(13f, Tier2, 4f), P(18f, Tier1, 4f)
            }
        },
        new StageMapDef
        {
            name = "Air B", minX = -22f, maxX = 22f, playerStartX = -19f,
            platforms = new[]
            {
                P(-16f, Tier1, 5f), P(-11f, Tier2, 3.5f), P(-6f, Tier1, 4f), P(-1f, Tier2, 4f),
                P(-1f, Tier3, 3.5f), P(5f, Tier1, 5f), P(11f, Tier2, 4f), P(16f, Tier1, 4f)
            }
        }
    };

    // Stage 3 (boss): the single-screen arena.
    public static readonly StageMapDef Arena = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        platforms = new Vector3[0]
    };

    public static readonly StageMapDef ArenaWithPlatforms = new StageMapDef
    {
        name = "Arena", minX = -10f, maxX = 10f, playerStartX = 0f,
        platforms = new[] { P(-5f, Tier1, 4f), P(5f, Tier1, 4f), P(0f, Tier2, 4f) }
    };

    public const int MaxPlatforms = 8;
}
