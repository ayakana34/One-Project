using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Title, Playing, Reward, Lost, Paused }

    public static GameManager Instance { get; private set; }

    const float FloorY = -6f;
    const int StageGround = 0, StageAir = 1, StageBoss = 2;
    const int DashChargeCap = 3;
    const float Tier1Top = -3.6f, Tier2Top = -1.2f;

    public float enemyHpGrowth = 0.3f;
    public float enemyAttackGrowth = 0.6f;
    public float waveHealFraction = 0.05f;
    public float bossHealFraction = 0.10f;
    public float rareChance = 0.25f;
    public float epicChance = 0.05f;

    public int gruntMin = 3, gruntMax = 5;
    public int eliteMin = 1, eliteMax = 3;
    public int flyerMin = 1, flyerMax = 3;
    public int shooterMin = 1, shooterMax = 3;

    public GameObject weaponPrefab;
    public GameObject bossPrefab;
    public GameObject flyerPrefab;
    public GameObject shooterPrefab;
    public GameObject gruntPrefab;
    public GameObject elitePrefab;
    public bool removePlatformsDuringBoss = true;

    public Player Player { get; private set; }
    public GameState State { get; private set; }
    public int Round { get; private set; } = 1;

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
    GUIStyle labelStyle, bigStyle, centerStyle, cardStyle;
    int stage = StageGround, pendingStage, lastHeal;
    bool recallJustGranted;
    float spawnAt = -1f;
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
        RandomizePlatforms();
    }

    void Start()
    {
        var xs = PickFloorXs(Enemy.All.Count);
        for (int i = 0; i < xs.Count; i++)
        {
            var t = Enemy.All[i].transform;
            t.position = new Vector3(xs[i], t.position.y, t.position.z);
        }
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

        if (spawnAt >= 0f)
        {
            if (Time.time >= spawnAt)
            {
                spawnAt = -1f;
                SpawnStage(pendingStage);
            }
            return;
        }

        if (Enemy.All.Count == 0 && NextStage() >= 0) OpenReward();
    }

    bool StageReady(int s) => s switch
    {
        StageGround => gruntPrefab != null && elitePrefab != null,
        StageAir => flyerPrefab != null && shooterPrefab != null,
        _ => bossPrefab != null
    };

    int NextStage()
    {
        for (int i = 1; i <= 3; i++)
        {
            int s = (stage + i) % 3;
            if (StageReady(s)) return s;
        }
        return -1;
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
        lastHeal = Player.Heal(stage == StageBoss ? bossHealFraction : waveHealFraction);

        recallJustGranted = stage == StageBoss && !Player.HasRecall;
        if (recallJustGranted) Player.GrantRecall();

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
        pendingStage = NextStage();
        spawnAt = Time.time + 1.5f;

        if (pendingStage == StageGround)
        {
            foreach (var platform in platforms) platform.SetActive(true);
        }
        if (pendingStage != StageBoss || !removePlatformsDuringBoss) RandomizePlatforms();
    }

    void SpawnStage(int s)
    {
        if (s == StageGround)
        {
            Round++;
            SpawnGroundWave();
        }
        else if (s == StageAir)
        {
            SpawnAirWave();
        }
        else
        {
            SpawnBoss();
        }
        stage = s;
    }

    void SpawnEnemy(GameObject prefab, Vector3 position)
    {
        var enemy = Instantiate(prefab, position, Quaternion.identity).GetComponent<Enemy>();
        enemy.ScaleStats(1f + enemyHpGrowth * (Round - 1), 1f + enemyAttackGrowth * (Round - 1));
    }

    List<float> PickFloorXs(int count)
    {
        var xs = new List<float>();
        float playerX = Player.transform.position.x;
        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int t = 0; t < 40 && !placed; t++)
            {
                float x = UnityEngine.Random.Range(-9f, 9f);
                if (Mathf.Abs(x - playerX) < 4f) continue;
                if (xs.Exists(o => Mathf.Abs(o - x) < 1.4f)) continue;
                xs.Add(x);
                placed = true;
            }
            if (!placed) xs.Add(playerX < 0f ? 9f : -9f);
        }
        return xs;
    }

    Vector2 PickAirPoint(List<Vector2> used, float minY, float maxY, float minGap)
    {
        Vector2 playerPos = Player.transform.position;
        Vector2 point = Vector2.zero;
        for (int t = 0; t < 40; t++)
        {
            point = new Vector2(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(minY, maxY));
            if (Vector2.Distance(point, playerPos) < 4f) continue;
            if (used.Exists(o => Vector2.Distance(o, point) < minGap)) continue;
            break;
        }
        used.Add(point);
        return point;
    }

    void SpawnGroundWave()
    {
        int grunts = UnityEngine.Random.Range(gruntMin, gruntMax + 1);
        int elites = UnityEngine.Random.Range(eliteMin, eliteMax + 1);
        var xs = PickFloorXs(grunts + elites);
        for (int i = 0; i < xs.Count; i++)
        {
            if (i < grunts) SpawnEnemy(gruntPrefab, new Vector3(xs[i], FloorY + 0.45f, 0f));
            else SpawnEnemy(elitePrefab, new Vector3(xs[i], FloorY + 0.65f, 0f));
        }
    }

    void SpawnAirWave()
    {
        int flyers = UnityEngine.Random.Range(flyerMin, flyerMax + 1);
        int shooters = UnityEngine.Random.Range(shooterMin, shooterMax + 1);
        var used = new List<Vector2>();
        for (int i = 0; i < flyers; i++)
        {
            var p = PickAirPoint(used, 1.5f, 4f, 3f);
            SpawnEnemy(flyerPrefab, new Vector3(p.x, p.y, 0f));
        }
        for (int i = 0; i < shooters; i++)
        {
            var p = PickAirPoint(used, 4.3f, 4.3f, 3f);
            SpawnEnemy(shooterPrefab, new Vector3(p.x, p.y, 0f));
        }
    }

    void RandomizePlatforms()
    {
        int n = platforms.Length;
        if (n == 0) return;

        var xs = new float[n];
        var ws = new float[n];
        var tiers = new int[n];
        for (int attempt = 0; attempt < 60; attempt++)
        {
            for (int i = 0; i < n; i++)
            {
                tiers[i] = i == 0 ? 1 : UnityEngine.Random.Range(1, 3);
                ws[i] = UnityEngine.Random.Range(3f, 5f);
                float limit = 10f - ws[i] * 0.5f - 0.5f;
                xs[i] = UnityEngine.Random.Range(-limit, limit);
            }
            if (!LayoutValid(xs, ws, tiers)) continue;
            ApplyPlatforms(xs, ws, tiers);
            return;
        }

        for (int i = 0; i < n; i++)
        {
            xs[i] = i == 0 ? -5f : i == 1 ? 5f : 0f;
            ws[i] = 4f;
            tiers[i] = i < 2 ? 1 : 2;
        }
        ApplyPlatforms(xs, ws, tiers);
    }

    static bool LayoutValid(float[] xs, float[] ws, int[] tiers)
    {
        for (int i = 0; i < xs.Length; i++)
        {
            bool reachable = tiers[i] == 1;
            for (int j = 0; j < xs.Length; j++)
            {
                if (i == j) continue;
                float gap = Mathf.Abs(xs[i] - xs[j]) - (ws[i] + ws[j]) * 0.5f;
                if (tiers[i] == tiers[j] && gap < 1.5f) return false;
                if (tiers[i] == 2 && tiers[j] == 1 && gap <= 2f) reachable = true;
            }
            if (!reachable) return false;
        }
        return true;
    }

    void ApplyPlatforms(float[] xs, float[] ws, int[] tiers)
    {
        for (int i = 0; i < platforms.Length; i++)
        {
            float top = tiers[i] == 1 ? Tier1Top : Tier2Top;
            platforms[i].transform.position = new Vector3(xs[i], top - 0.2f, 0f);
            platforms[i].transform.localScale = new Vector3(ws[i], 0.4f, 1f);
        }
    }

    void SpawnBoss()
    {
        if (removePlatformsDuringBoss)
        {
            foreach (var platform in platforms) platform.SetActive(false);
        }

        float x = Player.transform.position.x < 0f ? 7f : -7f;
        SpawnEnemy(bossPrefab, new Vector3(x, FloorY + 1.15f, 0f));
    }

    public void OnPlayerDead() => State = GameState.Lost;

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
            "Q : 무기 회수 (회수 스킬을 얻은 뒤)\n" +
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
        GUI.Label(new Rect(16, 124, 500, 28), "라운드: " + Round, labelStyle);
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
            string banner = pendingStage == StageGround ? "라운드 " + (Round + 1) : pendingStage == StageAir ? "웨이브 2" : "보스 등장";
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 80), banner, bigStyle);
        }

        if (State == GameState.Reward) DrawRewards();
        if (State == GameState.Paused) DrawMenu(false);

        if (State == GameState.Lost)
        {
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "게임 오버", bigStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f + 40, Screen.width, 40), "도달한 라운드: " + Round + "   (R: 다시 시작)", centerStyle);
        }
    }

    void DrawRewards()
    {
        cardStyle ??= new GUIStyle(GUI.skin.button) { font = UiFont, fontSize =20, wordWrap = true, alignment = TextAnchor.MiddleCenter };

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 80), "보상을 선택하세요", bigStyle);
        if (lastHeal > 0)
            GUI.Label(new Rect(0, Screen.height * 0.18f + 70, Screen.width, 30), "체력 " + lastHeal + " 회복", centerStyle);
        if (recallJustGranted)
            GUI.Label(new Rect(0, Screen.height * 0.18f + 100, Screen.width, 30), "회수 스킬 획득!  Q키로 던진 무기를 불러오세요", centerStyle);

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
