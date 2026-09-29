using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Won, Lost }

    public static GameManager Instance { get; private set; }

    const float FloorY = -6f;

    const int StageGround = 0, StageAir = 1, StageBoss = 2;

    public GameObject weaponPrefab;
    public GameObject bossPrefab;
    public GameObject flyerPrefab;
    public GameObject shooterPrefab;
    public bool removePlatformsDuringBoss = true;

    public Player Player { get; private set; }
    public GameState State { get; private set; }

    GUIStyle labelStyle, bigStyle, centerStyle;
    int stage = StageGround;
    float spawnAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        Instance = this;
        Physics2D.gravity = new Vector2(0f, -9.81f);
        Player = FindFirstObjectByType<Player>();
        State = GameState.Playing;
    }

    void Update()
    {
        if (State == GameState.Playing && Enemy.All.Count == 0) AdvanceStage();
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    int NextStage()
    {
        if (stage == StageGround && flyerPrefab != null && shooterPrefab != null) return StageAir;
        if (stage < StageBoss && bossPrefab != null) return StageBoss;
        return -1;
    }

    void AdvanceStage()
    {
        int next = NextStage();
        if (next < 0)
        {
            State = GameState.Won;
            return;
        }
        if (spawnAt < 0f)
        {
            spawnAt = Time.time + 1.5f;
            return;
        }
        if (Time.time < spawnAt) return;

        spawnAt = -1f;
        stage = next;
        if (stage == StageAir) SpawnAirWave();
        else SpawnBoss();
    }

    void SpawnAirWave()
    {
        foreach (float x in new[] { -8f, 8f })
            Instantiate(flyerPrefab, new Vector3(x, 2f, 0f), Quaternion.identity);
        foreach (float x in new[] { -5f, 5f })
            Instantiate(shooterPrefab, new Vector3(x, 4.3f, 0f), Quaternion.identity);
    }

    void SpawnBoss()
    {
        if (removePlatformsDuringBoss)
        {
            foreach (var platform in FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None))
                platform.gameObject.SetActive(false);
        }

        float x = Player.transform.position.x < 0f ? 7f : -7f;
        Instantiate(bossPrefab, new Vector3(x, FloorY + 1.15f, 0f), Quaternion.identity);
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
        GUI.Label(new Rect(16, 124, 500, 28), "R: Restart", labelStyle);

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
            string banner = NextStage() == StageAir ? "WAVE 2" : "BOSS INCOMING";
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 80), banner, bigStyle);
        }

        if (State != GameState.Playing)
        {
            string msg = State == GameState.Won ? "CLEAR!" : "GAME OVER";
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), msg, bigStyle);
        }
    }
}
