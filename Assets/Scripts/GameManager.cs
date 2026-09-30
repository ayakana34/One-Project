using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Reward, Lost }

    public static GameManager Instance { get; private set; }

    const float FloorY = -6f;
    const int StageGround = 0, StageAir = 1, StageBoss = 2;
    const int DashChargeCap = 3;

    public float enemyHpGrowth = 0.6f;
    public float enemyAttackGrowth = 0.25f;
    public float waveHealFraction = 0.05f;
    public float bossHealFraction = 0.10f;
    public float rareChance = 0.25f;
    public float epicChance = 0.05f;
    public float recallCardChance = 0.08f;

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
        public bool isRecall;
        public int Value => type.values[tier];
    }

    static readonly string[] TierPrefix = { "", "RARE ", "EPIC " };
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
    float spawnAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        Physics2D.gravity = new Vector2(0f, -9.81f);
        Player = FindFirstObjectByType<Player>();
        platforms = Array.ConvertAll(FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None), p => p.gameObject);
        State = GameState.Playing;
        BuildRewardTypes();
    }

    void BuildRewardTypes()
    {
        types.Add(new RewardType
        {
            name = "ATTACK", weight = 3, values = new[] { 5, 10, 20 },
            describe = v => "Weapon damage +" + v,
            available = () => true,
            apply = v => Player.AddAttack(v)
        });
        types.Add(new RewardType
        {
            name = "DEFENSE", weight = 3, values = new[] { 1, 2, 4 },
            describe = v => "Damage taken -" + v,
            available = () => true,
            apply = v => Player.AddDefense(v)
        });
        types.Add(new RewardType
        {
            name = "MAX HP", weight = 3, values = new[] { 10, 20, 40 },
            describe = v => "Max HP +" + v + ", heal " + v,
            available = () => true,
            apply = v => Player.AddMaxHp(v)
        });
        types.Add(new RewardType
        {
            name = "DASH", weight = 2, scalesWithTier = false, values = new[] { 1, 1, 1 },
            describe = v => "Max dash charges +" + v,
            available = () => Player.MaxDashCharges < DashChargeCap,
            apply = v => Player.AddDashCharge()
        });
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

        if (!Player.HasRecall && choices.Count > 0 && UnityEngine.Random.value < recallCardChance)
            choices[UnityEngine.Random.Range(0, choices.Count)] = new Card { tier = 2, isRecall = true };

        State = GameState.Reward;
        Time.timeScale = 0f;
    }

    void ChooseReward(int index)
    {
        if (index < 0 || index >= choices.Count) return;

        var card = choices[index];
        if (card.isRecall) Player.GrantRecall();
        else card.type.apply(card.Value);
        choices.Clear();

        Time.timeScale = 1f;
        State = GameState.Playing;
        pendingStage = NextStage();
        spawnAt = Time.time + 1.5f;
    }

    void SpawnStage(int s)
    {
        if (s == StageGround)
        {
            Round++;
            foreach (var platform in platforms) platform.SetActive(true);
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

    float SafeX(float x) => Mathf.Abs(x - Player.transform.position.x) < 3f ? -x : x;

    void SpawnEnemy(GameObject prefab, Vector3 position)
    {
        var enemy = Instantiate(prefab, position, Quaternion.identity).GetComponent<Enemy>();
        enemy.ScaleStats(1f + enemyHpGrowth * (Round - 1), 1f + enemyAttackGrowth * (Round - 1));
    }

    void SpawnGroundWave()
    {
        foreach (float x in new[] { -9f, -7.5f, 7.5f, 9f })
            SpawnEnemy(gruntPrefab, new Vector3(SafeX(x), FloorY + 0.45f, 0f));
        foreach (float x in new[] { -5.5f, 5.5f })
            SpawnEnemy(elitePrefab, new Vector3(SafeX(x), FloorY + 0.65f, 0f));
    }

    void SpawnAirWave()
    {
        foreach (float x in new[] { -8f, 8f })
            SpawnEnemy(flyerPrefab, new Vector3(x, 2f, 0f));
        foreach (float x in new[] { -5f, 5f })
            SpawnEnemy(shooterPrefab, new Vector3(x, 4.3f, 0f));
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

    void OnGUI()
    {
        if (Player == null) return;
        labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
        bigStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        centerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        var hpBar = new Rect(16, 16, 300, 26);
        GUI.color = new Color(0.15f, 0.15f, 0.17f);
        GUI.DrawTexture(hpBar, Texture2D.whiteTexture);
        GUI.color = new Color(0.85f, 0.2f, 0.25f);
        GUI.DrawTexture(new Rect(hpBar.x, hpBar.y, hpBar.width * Mathf.Clamp01((float)Player.Hp / Player.MaxHp), hpBar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(hpBar, Player.Hp + " / " + Player.MaxHp, centerStyle);

        GUI.Label(new Rect(16, 52, 500, 28), "Weapon: " + (Player.HasWeapon ? "READY" : "PICK IT UP"), labelStyle);
        GUI.Label(new Rect(16, 76, 80, 28), "Dash:", labelStyle);
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
        GUI.Label(new Rect(16, 100, 500, 28), "Enemies: " + Enemy.All.Count, labelStyle);
        GUI.Label(new Rect(16, 124, 500, 28), "Round: " + Round, labelStyle);
        GUI.Label(new Rect(16, 148, 500, 28), "ATK " + Player.stats.attack + "   DEF " + Player.stats.defense, labelStyle);
        int nextLine = 172;
        if (Player.HasRecall)
        {
            float left = Player.RecallCooldownLeft;
            GUI.Label(new Rect(16, nextLine, 500, 28), "Recall [Q]: " + (left <= 0f ? "READY" : left.ToString("0.0") + "s"), labelStyle);
            nextLine += 24;
        }
        GUI.Label(new Rect(16, nextLine, 500, 28), "R: Restart", labelStyle);

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
            GUI.Label(new Rect(x, 44, w, 28), "BOSS", centerStyle);
        }

        if (State == GameState.Playing && spawnAt >= 0f)
        {
            string banner = pendingStage == StageGround ? "ROUND " + (Round + 1) : pendingStage == StageAir ? "WAVE 2" : "BOSS INCOMING";
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 80), banner, bigStyle);
        }

        if (State == GameState.Reward) DrawRewards();

        if (State == GameState.Lost)
        {
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "GAME OVER", bigStyle);
            GUI.Label(new Rect(0, Screen.height * 0.5f + 40, Screen.width, 40), "Reached round " + Round + "   (R to restart)", centerStyle);
        }
    }

    void DrawRewards()
    {
        cardStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 20, wordWrap = true, alignment = TextAnchor.MiddleCenter };

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 80), "CHOOSE A REWARD", bigStyle);
        if (lastHeal > 0)
            GUI.Label(new Rect(0, Screen.height * 0.18f + 70, Screen.width, 30), "Recovered " + lastHeal + " HP", centerStyle);

        const float w = 260f, h = 170f, gap = 30f;
        float total = choices.Count * w + (choices.Count - 1) * gap;
        float x0 = (Screen.width - total) * 0.5f;
        float y0 = Screen.height * 0.4f;
        for (int i = 0; i < choices.Count; i++)
        {
            var card = choices[i];
            var rect = new Rect(x0 + i * (w + gap), y0, w, h);
            string text = card.isRecall
                ? "[" + (i + 1) + "]\nEPIC RECALL SKILL\nPress Q to pull your weapon back to you"
                : "[" + (i + 1) + "]\n" + TierPrefix[card.tier] + card.type.name + " +" + card.Value + "\n" + card.type.describe(card.Value);

            GUI.backgroundColor = TierColor[card.tier];
            if (GUI.Button(rect, text, cardStyle)) ChooseReward(i);
            GUI.backgroundColor = Color.white;
        }
    }
}
