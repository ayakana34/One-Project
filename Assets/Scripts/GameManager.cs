using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
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
    [Tooltip("Open a reward screen every time a stage is left (otherwise only after the boss).")]
    public bool rewardAfterEveryStage = true;

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
    GUIStyle labelStyle, bigStyle, centerStyle, cardStyle;
    int stage = StageGround, pendingStage, lastHeal;
    float spawnAt = -1f;
    StageMapDef currentMap = StageMaps.Stage1;
    Transform bgT, ceilingT, floorT, leftT, rightT;
    GameObject exitGo;
    SpriteRenderer exitRenderer;
    CameraFollow camFollow;
    Sprite squareSprite;
    readonly List<Transform> stripes = new();
    GameState stateBeforePause;
    float timeScaleBeforePause = 1f;
    GUIStyle controlsStyle;

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

        if (spawnAt >= 0f)
        {
            if (Time.time >= spawnAt)
            {
                spawnAt = -1f;
                SpawnStage(pendingStage);
            }
            return;
        }

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
        spawnAt = Time.time + 1.5f;
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
    // the rune room's exit opens when the recall rune has been taken.
    bool ExitOpen => spawnAt < 0f && (stage == StageRune ? runeTaken : stage != StageBoss && Enemy.All.Count == 0);

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

    void DrawExitHint()
    {
        var cam = Camera.main;
        if (cam == null || exitGo == null || !exitGo.activeSelf) return;
        bool open = ExitOpen;
        var screen = cam.WorldToScreenPoint(exitGo.transform.position + Vector3.up * 1.7f);
        if (screen.x >= 0f && screen.x <= Screen.width)
        {
            string label = open ? "출구" : "잠김 · 남은 적 " + Enemy.All.Count;
            GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 14f, 200f, 28f), label, centerStyle);
        }
        else
        {
            int meters = Mathf.Max(0, Mathf.RoundToInt(exitGo.transform.position.x - Player.transform.position.x));
            string label = open ? "출구 >> " + meters + "m" : "출구 잠김 · 남은 적 " + Enemy.All.Count;
            GUI.Label(new Rect(Screen.width - 260f, Screen.height * 0.4f, 250f, 28f), label, labelStyle);
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

    void DrawMenu(bool title)
    {
        cardStyle ??= new GUIStyle(GUI.skin.button) { font = UiFont, fontSize =20, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        controlsStyle ??= new GUIStyle(GUI.skin.label) { font = UiFont, fontSize =18, alignment = TextAnchor.UpperLeft, normal = { textColor = Color.white } };

        GUI.color = new Color(0f, 0f, 0f, title ? 0.85f : 0.7f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.08f, Screen.width, 90), title ? "ONE WEAPON" : "일시정지", bigStyle);

        string[] labels = title ? new[] { "시작  (Enter)", "종료" } : new[] { "계속하기  (Esc)", "다시 시작", "종료" };
        Action[] actions = title ? new Action[] { StartGame, QuitGame } : new Action[] { Resume, RestartGame, QuitGame };
        const float bw = 280f, bh = 48f, gap = 14f;
        float x = (Screen.width - bw) * 0.5f;
        float y = Screen.height * 0.25f;
        for (int i = 0; i < labels.Length; i++)
        {
            if (!GUI.Button(new Rect(x, y + i * (bh + gap), bw, bh), labels[i], cardStyle)) continue;

            actions[i]();
            return;
        }

        float cy = y + labels.Length * (bh + gap) + 24f;
        GUI.Label(new Rect((Screen.width - 520f) * 0.5f, cy, 520f, 260f),
            "조작 안내\n" +
            "A / D : 좌우 이동\n" +
            "Space : 점프   (S + Space : 발판 아래로 내려가기)\n" +
            "마우스 : 조준\n" +
            "마우스 좌클릭 : 무기 던지기\n" +
            "Shift / 마우스 우클릭 : 마우스 방향으로 대쉬\n" +
            "Q : 무기 회수 (회수의 룬을 얻은 뒤)\n" +
            "R : 다시 시작      Esc : 메뉴",
            controlsStyle);
    }

    void OnGUI()
    {
        if (Player == null) return;
        labelStyle ??= new GUIStyle(GUI.skin.label) { font = UiFont, fontSize =18, normal = { textColor = Color.white } };
        bigStyle ??= new GUIStyle(GUI.skin.label) { font = UiFont, fontSize =48, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        centerStyle ??= new GUIStyle(GUI.skin.label) { font = UiFont, fontSize =18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        if (State == GameState.Title)
        {
            DrawMenu(true);
            return;
        }

        var hpBar = new Rect(16, 16, 300, 26);
        GUI.color = new Color(0.15f, 0.15f, 0.17f);
        GUI.DrawTexture(hpBar, Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.2f, 0.25f);
        GUI.DrawTexture(new Rect(hpBar.x, hpBar.y, hpBar.width * Mathf.Clamp01((float)Player.Hp / Player.MaxHp), hpBar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(hpBar, Player.Hp + " / " + Player.MaxHp, centerStyle);

        GUI.Label(new Rect(16, 52, 500, 28), "무기: " + (Player.HasWeapon ? "준비됨" : "주워야 함"), labelStyle);
        GUI.Label(new Rect(16, 76, 80, 28), "대쉬:", labelStyle);
        for (int i = 0; i < Player.MaxDashCharges; i++)
        {
            var slot = new Rect(80 + i * 30, 82, 24, 16);
            GUI.color = new Color(0.2f, 0.2f, 0.24f);
            GUI.DrawTexture(slot, Texture2D.whiteTexture);
            float fill = i < Player.DashCharges ? 1f : i == Player.DashCharges ? Player.DashRechargeProgress : 0f;
            GUI.color = new Color(0.4f, 0.8f, 1f);
            GUI.DrawTexture(new Rect(slot.x, slot.y, slot.width * fill, slot.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 100, 500, 28), "남은 적: " + Enemy.All.Count, labelStyle);
        GUI.Label(new Rect(16, 124, 500, 28), StageName(stage), labelStyle);
        GUI.Label(new Rect(16, 148, 500, 28), "공격력 " + Player.stats.attack + "   방어력 " + Player.stats.defense, labelStyle);
        int nextLine = 172;
        if (Player.HasRecall)
        {
            float left = Player.RecallCooldownLeft;
            GUI.Label(new Rect(16, nextLine, 500, 28), "회수 [Q]: " + (left <= 0f ? "준비됨" : left.ToString("0.0") + "초"), labelStyle);
            nextLine += 24;
        }
        GUI.Label(new Rect(16, nextLine, 500, 28), "R: 다시 시작   Esc: 메뉴", labelStyle);

        var boss = Boss.Current;
        if (boss != null)
        {
            float w = 500f;
            float x = (Screen.width - w) * 0.5f;
            GUI.color = new Color(0.15f, 0.15f, 0.15f);
            GUI.DrawTexture(new Rect(x, 20, w, 22), Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.2f, 0.2f);
            GUI.DrawTexture(new Rect(x, 20, w * Mathf.Clamp01((float)boss.Hp / boss.MaxHp), 22), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, 44, w, 28), "보스", centerStyle);
        }

        if (State == GameState.Playing && spawnAt >= 0f)
        {
            string banner = StageName(pendingStage);
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 80), banner, bigStyle);
        }

        if (State == GameState.Playing) DrawExitHint();
        if (State == GameState.Reward) DrawRewards();
        if (State == GameState.Paused) DrawMenu(false);

        if (State == GameState.Lost)
        {
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "게임 오버", bigStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f + 40, Screen.width, 40), "도달한 곳: " + StageName(stage) + "   (R: 다시 시작)", centerStyle);
        }

        if (State == GameState.Won)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 90), "게임 클리어!", bigStyle);
            int sec = Mathf.RoundToInt(clearSeconds);
            GUI.Label(new Rect(0, Screen.height * 0.3f + 90, Screen.width, 40), "클리어 시간 " + sec / 60 + "분 " + sec % 60 + "초", centerStyle);
            GUI.Label(new Rect(0, Screen.height * 0.3f + 130, Screen.width, 40), "R: 다시 시작   Esc: 메뉴", centerStyle);
        }

        if (runeMessageUntil > Time.unscaledTime && State != GameState.Lost)
        {
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 80), "회수의 룬 획득!", bigStyle);
            GUI.Label(new Rect(0, Screen.height * 0.2f + 70, Screen.width, 30), "Q : 던진 무기를 손으로 불러옵니다 (쿨타임 4초)", centerStyle);
        }
    }

    static string StageName(int s) => s switch
    {
        StageGround => "스테이지 1",
        StageAir => "스테이지 2",
        StageRune => "스테이지 3 · 회수의 룬",
        _ => "최종 스테이지 · 보스"
    };

    void DrawRewards()
    {
        cardStyle ??= new GUIStyle(GUI.skin.button) { font = UiFont, fontSize =20, wordWrap = true, alignment = TextAnchor.MiddleCenter };

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 80), "보상을 선택하세요", bigStyle);
        if (lastHeal > 0)
            GUI.Label(new Rect(0, Screen.height * 0.18f + 70, Screen.width, 30), "체력 " + lastHeal + " 회복", centerStyle);

        const float w = 260f, h = 170f, gap = 30f;
        float total = choices.Count * w + (choices.Count - 1) * gap;
        float x0 = (Screen.width - total) * 0.5f;
        float y0 = Screen.height * 0.4f;
        for (int i = 0; i < choices.Count; i++)
        {
            var card = choices[i];
            var rect = new Rect(x0 + i * (w + gap), y0, w, h);
            string text = "[" + (i + 1) + "]\n" + TierPrefix[card.tier] + card.type.name + " +" + card.Value + "\n" + card.type.describe(card.Value);

            GUI.backgroundColor = TierColor[card.tier];
            if (GUI.Button(rect, text, cardStyle)) ChooseReward(i);
            GUI.backgroundColor = Color.white;
        }
    }
}
