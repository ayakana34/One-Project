using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public partial class GameManager : MonoBehaviour
{
    public enum GameState { Title, Playing, Reward, Lost, Paused, Won }

    public static GameManager Instance { get; private set; }

    const float FloorY = -6f;
    const int StageGround = 0, StageAir = 1, StageRune = 2, StageBoss = 3, StageCount = 4;
    const int DashChargeCap = 3;

    public float enemyHpGrowth = 0.3f;
    public float enemyAttackGrowth = 0.6f;
    public float bossHealFraction = 0.10f;
    public float rareChance = 0.25f;
    public float epicChance = 0.05f;

    public GameObject weaponPrefab;
    public GameObject bossPrefab;
    public GameObject flyerPrefab;
    public GameObject shooterPrefab;
    public GameObject gruntPrefab;
    public GameObject elitePrefab;
    public bool removePlatformsDuringBoss = true;
    [Tooltip("Open a reward card screen every time a stage is left (off: stages just follow each other).")]
    public bool rewardAfterEveryStage = false;

    public Player Player { get; private set; }
    public GameState State { get; private set; }
    public int Round { get; private set; } = 1; // one run has no repeat, so monsters are never scaled up

    class RewardType
    {
        public string name;
        public int weight;
        public bool scalesWithTier = true;
        public int[] values;
        public Func<int, string> describe;
        public Func<bool> available;
        public Action<int> apply;
    }

    class Card
    {
        public RewardType type;
        public int tier;
        public int Value => type.values[tier];
    }

    static readonly string[] TierPrefix = { "", "희귀 ", "영웅 " };
    static readonly Color[] TierColor =
    {
        new Color(0.85f, 0.85f, 0.85f),
        new Color(0.45f, 0.75f, 1f),
        new Color(0.85f, 0.55f, 1f)
    };

    readonly List<RewardType> types = new();
    readonly List<Card> choices = new();
    GameObject[] platforms;
    GameObject[] terrain;
    GameObject runeGo;
    bool runeTaken;
    float runeMessageUntil, runStartRealtime, clearSeconds;
    SpriteRenderer bgRenderer, floorRenderer;
    readonly List<SpriteRenderer> terrainRenderers = new();
    int stage = StageGround, pendingStage, lastHeal;
    float bannerUntil = -1f; // the stage banner is shown until then; the monsters are already on the field
    StageMapDef currentMap = StageMaps.Stage1;
    Transform bgT, ceilingT, floorT, leftT, rightT;
    GameObject exitGo;
    SpriteRenderer exitRenderer;
    CameraFollow camFollow;
    Sprite squareSprite;
    readonly List<Transform> stripes = new();
    GameState stateBeforePause;
    float timeScaleBeforePause = 1f;

    static bool skipTitleOnLoad;
    static Font uiFont;

    static Font UiFont => uiFont ??= Font.CreateDynamicFontFromOSFont(
        new[] { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 18);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        skipTitleOnLoad = false;
        uiFont = null;
    }

    void Awake()
    {
        Instance = this;
        Time.timeScale = skipTitleOnLoad ? 1f : 0f;
        Physics2D.gravity = new Vector2(0f, -9.81f);
        Player = FindFirstObjectByType<Player>();
        platforms = Array.ConvertAll(FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None), p => p.gameObject);
        State = skipTitleOnLoad ? GameState.Playing : GameState.Title;
        BuildRewardTypes();
        SetupWorld();
        BeginStage(StageGround, true);
    }

    void Start()
    {
        // The scene still contains the old fixed wave; the first stage is spawned by the stage system instead.
        foreach (var e in Enemy.All.ToArray()) if (e != null) Destroy(e.gameObject);
        SpawnStage(StageGround);
    }

    void BuildRewardTypes()
    {
        types.Add(new RewardType
        {
            name = "공격력", weight = 3, values = new[] { 5, 10, 20 },
            describe = v => "무기 피해 +" + v,
            available = () => true,
            apply = v => Player.AddAttack(v)
        });
        types.Add(new RewardType
        {
            name = "방어력", weight = 3, values = new[] { 1, 2, 4 },
            describe = v => "받는 피해 -" + v,
            available = () => true,
            apply = v => Player.AddDefense(v)
        });
        types.Add(new RewardType
        {
            name = "최대 체력", weight = 3, values = new[] { 10, 20, 40 },
            describe = v => "최대 체력 +" + v + ", 체력 " + v + " 회복",
            available = () => true,
            apply = v => Player.AddMaxHp(v)
        });
        types.Add(new RewardType
        {
            name = "대쉬 충전", weight = 2, scalesWithTier = false, values = new[] { 1, 1, 1 },
            describe = v => "대쉬 최대 충전 +" + v,
            available = () => Player.MaxDashCharges < DashChargeCap,
            apply = v => Player.AddDashCharge()
        });
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (State == GameState.Title)
        {
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) StartGame();
            return;
        }
        if (State == GameState.Paused)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Resume();
            return;
        }
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Pause();
            return;
        }
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            RestartGame();
            return;
        }

        if (State == GameState.Reward)
        {
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) ChooseReward(0);
            else if (kb.digit2Key.wasPressedThisFrame) ChooseReward(1);
            else if (kb.digit3Key.wasPressedThisFrame) ChooseReward(2);
            return;
        }

        if (State != GameState.Playing) return;

        UpdateExitDoor();

        // Stages 1 to 3 are left through the exit door; the game is cleared when the boss is dead.
        if (stage == StageBoss && Enemy.All.Count == 0) OnBossDefeated();
    }

    bool StageReady(int s) => s switch
    {
        StageGround or StageAir or StageRune => gruntPrefab != null && elitePrefab != null && flyerPrefab != null && shooterPrefab != null,
        _ => bossPrefab != null
    };

    // Stage 1 -> 2 -> rune room -> boss; the run ends with the boss.
    int NextStage()
    {
        int s = stage + 1;
        return s < StageCount && StageReady(s) ? s : -1;
    }

    int RollTier()
    {
        float roll = UnityEngine.Random.value;
        if (roll < epicChance) return 2;
        if (roll < epicChance + rareChance) return 1;
        return 0;
    }

    void OpenReward()
    {
        lastHeal = 0; // rewards are the only way to get stronger or healthier

        choices.Clear();
        var candidates = types.FindAll(t => t.available());
        for (int i = 0; i < 3 && candidates.Count > 0; i++)
        {
            int total = 0;
            foreach (var t in candidates) total += t.weight;

            int roll = UnityEngine.Random.Range(0, total);
            RewardType picked = candidates[candidates.Count - 1];
            foreach (var t in candidates)
            {
                if (roll < t.weight)
                {
                    picked = t;
                    break;
                }
                roll -= t.weight;
            }
            candidates.Remove(picked);

            int tier = picked.scalesWithTier ? RollTier() : 0;
            choices.Add(new Card { type = picked, tier = tier });
        }

        State = GameState.Reward;
        Time.timeScale = 0f;
    }

    void ChooseReward(int index)
    {
        if (index < 0 || index >= choices.Count) return;

        var card = choices[index];
        card.type.apply(card.Value);
        choices.Clear();

        Time.timeScale = 1f;
        State = GameState.Playing;
        GoToNextStage();
    }

    void SpawnStage(int s)
    {
        if (s == StageBoss)
        {
            SpawnBoss();
            return;
        }

        // Every map has its own fixed set of monsters.
        foreach (var e in currentMap.enemies)
        {
            float y = e.y;
            if (e.onGround) y += float.IsNaN(e.top) ? currentMap.SurfaceTop(e.x) : e.top;
            SpawnEnemy(PrefabFor(e.kind), new Vector3(e.x, y, 0f));
        }
    }

    GameObject PrefabFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Grunt => gruntPrefab,
        EnemyKind.Elite => elitePrefab,
        EnemyKind.Flyer => flyerPrefab,
        _ => shooterPrefab
    };

    void SpawnEnemy(GameObject prefab, Vector3 position)
    {
        var enemy = Instantiate(prefab, position, Quaternion.identity).GetComponent<Enemy>();
        enemy.ScaleStats(1f + enemyHpGrowth * (Round - 1), 1f + enemyAttackGrowth * (Round - 1));
    }

    // A small stone golem that the boss's rocks hatch (weaker than a grunt, scaled with the round).
    public Enemy SpawnMinion(GameObject golemPrefab, Vector3 position)
    {
        var enemy = Instantiate(golemPrefab != null ? golemPrefab : gruntPrefab, position, Quaternion.identity).GetComponent<Enemy>();
        enemy.ScaleStats((1f + enemyHpGrowth * (Round - 1)) * 0.6f, (1f + enemyAttackGrowth * (Round - 1)) * 0.7f);
        enemy.aggroRange = 1000f;
        return enemy;
    }

    void SpawnBoss()
    {
        float x = Player.transform.position.x < currentMap.CenterX ? 7f : -7f;
        SpawnEnemy(bossPrefab, new Vector3(currentMap.CenterX + x, FloorY + 1.15f, 0f));
    }

    // ---- stage maps ----

    void SetupWorld()
    {
        // Platform pool: the scene's platforms plus clones, each stage map switches the ones it needs on.
        var list = new List<GameObject>(platforms);
        if (list.Count > 0)
        {
            squareSprite = list[0].GetComponent<SpriteRenderer>().sprite;
            while (list.Count < StageMaps.MaxPlatforms)
            {
                var clone = Instantiate(list[0], list[0].transform.parent);
                clone.name = "Platform" + list.Count;
                list.Add(clone);
            }
        }
        platforms = list.ToArray();
        Player.RefreshPlatforms();

        var level = GameObject.Find("Level");
        if (level != null)
        {
            bgT = level.transform.Find("Background");
            ceilingT = level.transform.Find("Ceiling");
            floorT = level.transform.Find("Floor");

            // Solid terrain blocks (steps, hills, pillars) are copies of the floor.
            if (floorT != null)
            {
                terrain = new GameObject[StageMaps.MaxBlocks];
                for (int i = 0; i < terrain.Length; i++)
                {
                    terrain[i] = Instantiate(floorT.gameObject, floorT.parent);
                    terrain[i].name = "Terrain" + i;
                    terrain[i].SetActive(false);
                }
            }
            leftT = level.transform.Find("LeftWall");
            rightT = level.transform.Find("RightWall");
        }

        exitGo = new GameObject("Exit");
        exitGo.transform.localScale = new Vector3(1.4f, 2.4f, 1f);
        exitRenderer = exitGo.AddComponent<SpriteRenderer>();
        exitRenderer.sprite = squareSprite;
        exitRenderer.color = new Color(1f, 0.82f, 0.3f);
        exitRenderer.sortingOrder = -4;
        var exitBox = exitGo.AddComponent<BoxCollider2D>();
        exitBox.size = Vector2.one;
        exitBox.isTrigger = true;
        exitGo.AddComponent<StageExit>();

        runeGo = new GameObject("RecallRune");
        runeGo.AddComponent<RuneItem>();
        runeGo.SetActive(false);

        if (bgT != null) bgRenderer = bgT.GetComponent<SpriteRenderer>();
        if (floorT != null) floorRenderer = floorT.GetComponent<SpriteRenderer>();
        if (terrain != null)
            foreach (var t in terrain) terrainRenderers.Add(t.GetComponent<SpriteRenderer>());

        var cam = Camera.main;
        if (cam != null)
        {
            camFollow = cam.GetComponent<CameraFollow>();
            if (camFollow == null) camFollow = cam.gameObject.AddComponent<CameraFollow>();
            camFollow.target = Player.transform;
        }
    }

    StageMapDef MapFor(int s) => s switch
    {
        StageGround => StageMaps.Stage1,
        StageAir => StageMaps.Stage2,
        StageRune => StageMaps.RuneRoom,
        _ => removePlatformsDuringBoss ? StageMaps.Arena : StageMaps.ArenaWithPlatforms
    };

    // Loads the map of a stage (geometry, platforms, exit, camera) and puts the player at its start.
    void BeginStage(int s, bool first)
    {
        stage = s;
        pendingStage = s;
        runeTaken = false;
        if (first) runStartRealtime = Time.realtimeSinceStartup;

        var map = MapFor(s);

        ClearField();
        ApplyMap(map, s != StageBoss);
        Player.Teleport(new Vector2(map.playerStartX, FloorY + 0.55f));
        if (camFollow != null) camFollow.SetBounds(map.minX, map.maxX, true);
    }

    void GoToNextStage()
    {
        int next = NextStage();
        if (next < 0) return;
        BeginStage(next, false);
        SpawnStage(next);
        bannerUntil = Time.time + 1.5f;
    }

    void ClearField()
    {
        foreach (var e in Enemy.All.ToArray()) if (e != null) Destroy(e.gameObject);
        foreach (var b in FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        if (Weapon.Current != null) Destroy(Weapon.Current.gameObject);
        Player.PickUp();
    }

    void ApplyMap(StageMapDef map, bool withExit)
    {
        currentMap = map;
        float w = map.Width, cx = map.CenterX;
        Place(bgT, new Vector3(cx, 0f, 0f), new Vector3(w, 12f, 1f));
        Place(ceilingT, new Vector3(cx, 6.5f, 0f), new Vector3(w + 2f, 1f, 1f));
        Place(floorT, new Vector3(cx, -6.5f, 0f), new Vector3(w + 2f, 1f, 1f));
        Place(leftT, new Vector3(map.minX - 0.5f, 0f, 0f), new Vector3(1f, 12f, 1f));
        Place(rightT, new Vector3(map.maxX + 0.5f, 0f, 0f), new Vector3(1f, 12f, 1f));

        for (int i = 0; i < platforms.Length; i++)
        {
            bool used = i < map.platforms.Length;
            platforms[i].SetActive(used);
            if (!used) continue;
            var d = map.platforms[i];
            platforms[i].transform.position = new Vector3(d.x, d.y - 0.2f, 0f);
            platforms[i].transform.localScale = new Vector3(d.z, 0.4f, 1f);
        }

        if (terrain != null)
        {
            for (int i = 0; i < terrain.Length; i++)
            {
                bool used = i < map.blocks.Length;
                terrain[i].SetActive(used);
                if (!used) continue;
                var d = map.blocks[i];
                float height = d.y - FloorY;
                terrain[i].transform.position = new Vector3(d.x, FloorY + height * 0.5f, 0f);
                terrain[i].transform.localScale = new Vector3(d.z, height, 1f);
            }
        }

        if (bgRenderer != null) bgRenderer.color = map.backgroundColor;
        if (floorRenderer != null) floorRenderer.color = map.groundColor;
        foreach (var r in terrainRenderers) if (r != null) r.color = map.groundColor;

        runeGo.SetActive(map.runePos.HasValue && !runeTaken);
        if (map.runePos.HasValue) runeGo.transform.position = new Vector3(map.runePos.Value.x, map.runePos.Value.y + 0.9f, 0f);

        exitGo.SetActive(withExit);
        exitGo.transform.position = new Vector3(map.maxX - 1.5f, FloorY + 1.2f, 0f);

        // Faint vertical stripes in the background so that moving along a wide map can be seen.
        int needed = Mathf.CeilToInt(w / 5f) + 1;
        while (stripes.Count < needed)
        {
            var go = new GameObject("BgStripe");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            sr.sortingOrder = -9;
            go.transform.localScale = new Vector3(0.35f, 12f, 1f);
            stripes.Add(go.transform);
        }
        for (int i = 0; i < stripes.Count; i++)
        {
            bool on = i < needed;
            stripes[i].gameObject.SetActive(on);
            stripes[i].GetComponent<SpriteRenderer>().color = map.stripeColor;
            if (on) stripes[i].position = new Vector3(map.minX + 2.5f + i * 5f, 0f, 0f);
        }
    }

    static void Place(Transform t, Vector3 position, Vector3 scale)
    {
        if (t == null) return;
        t.position = position;
        t.localScale = scale;
    }

    // Stages 1 and 2 are left through the exit, which opens once every monster of the stage is dead;
    // the rune room's exit is always open (taking the recall rune there is optional).
    bool ExitOpen => (stage == StageRune || (stage != StageBoss && Enemy.All.Count == 0));

    void UpdateExitDoor()
    {
        if (exitRenderer == null || !exitGo.activeSelf) return;
        exitRenderer.color = ExitOpen ? new Color(1f, 0.82f, 0.3f) : new Color(0.42f, 0.27f, 0.27f);
    }

    public void OnExitReached()
    {
        if (State != GameState.Playing || !ExitOpen) return;
        if (rewardAfterEveryStage && stage != StageRune)
        {
            OpenReward();
        }
        else
        {
            lastHeal = 0;
            GoToNextStage();
        }
    }

    public void OnPlayerDead() => State = GameState.Lost;

    void OnBossDefeated()
    {
        clearSeconds = Time.realtimeSinceStartup - runStartRealtime;
        State = GameState.Won;
        Time.timeScale = 0f;
    }

    // The recall rune: taking it unlocks the recall skill (Q).
    public void OnRuneCollected()
    {
        if (runeTaken || State != GameState.Playing) return;
        runeTaken = true;
        Player.GrantRecall();
        if (runeGo != null) runeGo.SetActive(false);
        runeMessageUntil = Time.unscaledTime + 6f;
    }

    void StartGame()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;
    }

    void Pause()
    {
        stateBeforePause = State;
        timeScaleBeforePause = Time.timeScale;
        State = GameState.Paused;
        Time.timeScale = 0f;
    }

    void Resume()
    {
        State = stateBeforePause;
        Time.timeScale = timeScaleBeforePause;
    }

    void RestartGame()
    {
        skipTitleOnLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}
