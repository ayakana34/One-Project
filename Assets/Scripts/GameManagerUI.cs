using System;
using UnityEngine;

// All of the on-screen UI (IMGUI). It is laid out on a virtual 720 pixel high screen and scaled to the real window,
// so it looks the same at every resolution.
public partial class GameManager
{
    const float UiHeight = 720f;

    static readonly Color Ink = new Color(0.05f, 0.06f, 0.09f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
    static readonly Color Soft = new Color(1f, 1f, 1f, 0.62f);
    static readonly Color HpGood = new Color(0.36f, 0.82f, 0.46f);
    static readonly Color HpWarn = new Color(0.95f, 0.75f, 0.25f);
    static readonly Color HpBad = new Color(0.9f, 0.27f, 0.3f);
    static readonly Color DashColor = new Color(0.42f, 0.82f, 1f);

    GUIStyle uiText, buttonStyle, buttonPrimaryStyle;
    Matrix4x4 uiMatrix = Matrix4x4.identity;
    float wonShownAt = -1f;
    float uiWidth, hpTrail = -1f;
    float lastTrailTime;

    static Texture2D solid;
    static Texture2D Solid => solid != null ? solid : (solid = Texture2D.whiteTexture);

    // ------------------------------------------------------------------ drawing helpers

    // GUIUtility.RotateAroundPivot takes its pivot in unscaled screen pixels; this one takes virtual UI coordinates.
    void RotateUi(float degrees, Vector2 pivot)
    {
        GUI.matrix = uiMatrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
    }

    void ResetRotation() => GUI.matrix = uiMatrix;

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Solid);
        GUI.color = Color.white;
    }

    static void Frame(Rect r, Color c, float t = 1f)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static void Panel(Rect r, float alpha = 0.72f)
    {
        Fill(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.25f));
        Fill(r, new Color(Ink.r, Ink.g, Ink.b, alpha));
        Frame(r, new Color(1f, 1f, 1f, 0.14f));
    }

    static Texture2D MakeTexture(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    void Text(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle fontStyle = FontStyle.Normal, bool shadow = true)
    {
        uiText ??= new GUIStyle(GUI.skin.label) { font = UiFont, wordWrap = false, clipping = TextClipping.Overflow };
        uiText.fontSize = size;
        uiText.alignment = anchor;
        uiText.fontStyle = fontStyle;
        if (shadow)
        {
            SetTextColor(uiText, new Color(0f, 0f, 0f, 0.75f * color.a));
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, uiText);
        }
        SetTextColor(uiText, color);
        GUI.Label(r, text, uiText);
    }

    // Every state gets the same color: a label whose hover state differs from its normal one changes color
    // under the mouse, which makes plain text look like something that can be clicked.
    static void SetTextColor(GUIStyle style, Color c)
    {
        style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = c;
        style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = c;
    }

    bool UiButton(Rect r, string text, bool primary)
    {
        if (buttonStyle == null)
        {
            GUIStyle Make(Color normal, Color hover, Color active, Color textColor) => new GUIStyle(GUI.skin.button)
            {
                font = UiFont, fontSize = 22, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0), wordWrap = true,
                normal = { background = MakeTexture(normal), textColor = textColor },
                hover = { background = MakeTexture(hover), textColor = Color.white },
                active = { background = MakeTexture(active), textColor = Color.white },
                focused = { background = MakeTexture(normal), textColor = textColor }
            };
            buttonStyle = Make(new Color(0.14f, 0.16f, 0.22f, 0.95f), new Color(0.22f, 0.26f, 0.36f, 1f), new Color(0.1f, 0.12f, 0.17f, 1f), new Color(0.85f, 0.88f, 0.95f));
            buttonPrimaryStyle = Make(new Color(0.78f, 0.58f, 0.16f, 0.95f), new Color(0.95f, 0.72f, 0.22f, 1f), new Color(0.6f, 0.44f, 0.1f, 1f), new Color(0.1f, 0.07f, 0.02f));
        }
        Fill(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f));
        return GUI.Button(r, text, primary ? buttonPrimaryStyle : buttonStyle);
    }

    // ------------------------------------------------------------------ the screen

    void OnGUI()
    {
        if (Player == null) return;

        float s = Screen.height / UiHeight;
        uiWidth = Screen.width / s;
        var oldMatrix = GUI.matrix;
        uiMatrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        GUI.matrix = uiMatrix;

        if (State == GameState.Title) DrawMenu(true);
        else
        {
            if (State != GameState.Intro) DrawHud(); // the cutscene has the screen to itself
            if (State == GameState.Playing)
            {
                DrawExitHint(s);
                DrawStageBanner();
                DrawEnrageBanner();
            }
            if (State == GameState.Intro) DrawBossIntro();
            DrawBossDeath();
            if (State == GameState.Tutorial) DrawRuneTutorial();
            if (State == GameState.Reward) DrawRewards();
            if (State == GameState.Paused) DrawMenu(false);
            if (State == GameState.Lost) DrawGameOver();
            if (State == GameState.Won) DrawGameClear();
        }

        GUI.matrix = oldMatrix;
    }

    void DrawHud()
    {
        var p = Player;

        // the trailing bar lags behind the real health so a hit is easy to see
        float ratio = Mathf.Clamp01((float)p.Hp / p.MaxHp);
        if (hpTrail < 0f) hpTrail = ratio;
        if (Event.current.type == EventType.Repaint)
        {
            float dt = Mathf.Min(0.1f, Time.realtimeSinceStartup - lastTrailTime);
            lastTrailTime = Time.realtimeSinceStartup;
            hpTrail = ratio >= hpTrail ? ratio : Mathf.MoveTowards(hpTrail, ratio, dt * 0.5f);
        }

        bool recall = p.HasRecall;
        var panel = new Rect(16f, 16f, 352f, recall ? 138f : 112f);
        Panel(panel);

        // health
        var bar = new Rect(panel.x + 12f, panel.y + 12f, panel.width - 24f, 24f);
        Fill(bar, new Color(0.02f, 0.02f, 0.04f, 0.95f));
        Fill(new Rect(bar.x, bar.y, bar.width * hpTrail, bar.height), new Color(1f, 1f, 1f, 0.7f));
        var hpColor = ratio > 0.5f ? HpGood : ratio > 0.25f ? HpWarn : Color.Lerp(HpBad, Color.white, Mathf.PingPong(Time.unscaledTime * 2.5f, 1f) * 0.4f);
        Fill(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), hpColor);
        Fill(new Rect(bar.x, bar.y, bar.width * ratio, 3f), new Color(1f, 1f, 1f, 0.28f));
        Frame(bar, p.HitInvulnerable ? new Color(1f, 0.4f, 0.4f, 0.95f) : new Color(1f, 1f, 1f, 0.25f), p.HitInvulnerable ? 2f : 1f);
        Text(bar, "HP  " + p.Hp + " / " + p.MaxHp, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

        // dash charges
        float y = panel.y + 48f;
        Text(new Rect(panel.x + 12f, y - 4f, 60f, 24f), "대쉬", 16, Soft);
        for (int i = 0; i < p.MaxDashCharges; i++)
        {
            var slot = new Rect(panel.x + 62f + i * 34f, y, 28f, 14f);
            Fill(slot, new Color(0.02f, 0.02f, 0.04f, 0.95f));
            float fill = i < p.DashCharges ? 1f : i == p.DashCharges ? p.DashRechargeProgress : 0f;
            Fill(new Rect(slot.x, slot.y, slot.width * fill, slot.height), i < p.DashCharges ? DashColor : DashColor * 0.7f);
            Frame(slot, new Color(1f, 1f, 1f, 0.22f));
        }
        Text(new Rect(panel.xMax - 130f, y - 4f, 118f, 24f), "공격 " + p.stats.attack + "  방어 " + p.stats.defense, 16, Soft, TextAnchor.MiddleRight);

        // weapon
        y += 28f;
        Text(new Rect(panel.x + 12f, y - 4f, 60f, 24f), "무기", 16, Soft);
        bool ready = p.HasWeapon;
        float blink = ready ? 1f : 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 3f, 1f);
        var dot = new Rect(panel.x + 64f, y + 2f, 12f, 12f);
        Fill(dot, ready ? HpGood : new Color(HpBad.r, HpBad.g, HpBad.b, blink));
        Frame(dot, new Color(0f, 0f, 0f, 0.5f));
        Text(new Rect(panel.x + 84f, y - 4f, 200f, 24f), ready ? "준비됨" : "주워야 함", 16, ready ? Color.white : new Color(1f, 0.6f, 0.6f, blink));

        // recall skill
        if (recall)
        {
            y += 28f;
            float left = p.RecallCooldownLeft;
            Text(new Rect(panel.x + 12f, y - 4f, 60f, 24f), "회수", 16, Soft);
            var key = new Rect(panel.x + 62f, y - 2f, 22f, 20f);
            Fill(key, new Color(0.2f, 0.23f, 0.3f));
            Frame(key, new Color(1f, 1f, 1f, 0.3f));
            Text(key, "Q", 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            var cd = new Rect(panel.x + 94f, y + 2f, 130f, 12f);
            Fill(cd, new Color(0.02f, 0.02f, 0.04f, 0.95f));
            float cdFill = left <= 0f ? 1f : Mathf.Clamp01(1f - left / 4f);
            Fill(new Rect(cd.x, cd.y, cd.width * cdFill, cd.height), left <= 0f ? Gold : Gold * 0.6f);
            Frame(cd, new Color(1f, 1f, 1f, 0.22f));
            Text(new Rect(cd.xMax + 8f, y - 4f, 80f, 24f), left <= 0f ? "준비됨" : left.ToString("0.0") + "초", 16, left <= 0f ? Color.white : Soft);
        }

        // stage name and what is left to do
        var boss = Boss.Current;
        float cx = uiWidth * 0.5f;
        var chip = new Rect(cx - 150f, 14f, 300f, 54f);
        Panel(chip, 0.62f);
        Text(new Rect(chip.x, chip.y + 4f, chip.width, 26f), StageName(stage), 19, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        string status;
        Color statusColor = Color.white;
        if (stage == StageBoss) status = boss != null ? "보스를 쓰러뜨리세요" : "";
        else if (stage == StageRune) status = "룬을 얻고 출구로";
        else if (Enemy.All.Count > 0) status = "남은 적  " + Enemy.All.Count;
        else { status = "출구가 열렸습니다"; statusColor = HpGood; }
        Text(new Rect(chip.x, chip.y + 28f, chip.width, 22f), status, 16, statusColor, TextAnchor.MiddleCenter);

        if (boss != null)
        {
            const float w = 560f;
            var b = new Rect(cx - w * 0.5f, chip.yMax + 12f, w, 22f);
            float br = Mathf.Clamp01((float)boss.Hp / boss.MaxHp);
            Fill(new Rect(b.x - 3f, b.y - 3f, b.width + 6f, b.height + 6f), new Color(Ink.r, Ink.g, Ink.b, 0.8f));
            Fill(b, new Color(0.02f, 0.02f, 0.04f, 0.95f));
            Fill(new Rect(b.x, b.y, b.width * br, b.height), br > 0.5f ? new Color(0.86f, 0.24f, 0.24f) : new Color(1f, 0.4f, 0.2f));
            Fill(new Rect(b.x, b.y, b.width * br, 3f), new Color(1f, 1f, 1f, 0.28f));
            Frame(b, new Color(1f, 1f, 1f, 0.3f));
            Text(b, "보스", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        Text(new Rect(16f, UiHeight - 32f, 700f, 24f), stageJumpKeys ? "R 다시 시작   ·   Esc 메뉴   ·   F1~F4 스테이지 이동" : "R 다시 시작   ·   Esc 메뉴", 14, new Color(1f, 1f, 1f, 0.4f));
    }

    void DrawExitHint(float scale)
    {
        var cam = Camera.main;
        if (cam == null || exitGo == null || !exitGo.activeSelf) return;
        bool open = ExitOpen;
        var screen = cam.WorldToScreenPoint(exitGo.transform.position + Vector3.up * 1.9f);
        string label = open ? "출구" : "잠김 · 남은 적 " + Enemy.All.Count;
        var color = open ? Gold : new Color(1f, 0.6f, 0.6f);

        if (screen.x >= 0f && screen.x <= Screen.width)
        {
            var r = new Rect(screen.x / scale - 70f, (Screen.height - screen.y) / scale - 14f, 140f, 28f);
            Panel(r, 0.6f);
            Text(r, label, 16, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
        else
        {
            int meters = Mathf.Max(0, Mathf.RoundToInt(exitGo.transform.position.x - Player.transform.position.x));
            var r = new Rect(uiWidth - 226f, UiHeight * 0.42f, 210f, 34f);
            Panel(r, 0.6f);
            Text(r, open ? "출구  " + meters + "m  ▶" : "출구 잠김 · 적 " + Enemy.All.Count, 17, color, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }

    static string StageHint(int s) => s switch
    {
        StageGround or StageAir => "모든 적을 처치하면 출구가 열립니다",
        StageRune => "회수의 룬을 얻으면 던진 무기를 불러올 수 있습니다",
        _ => "보스를 쓰러뜨리세요"
    };

    void DrawStageBanner()
    {
        if (bannerUntil < 0f || Time.time >= bannerUntil) return;
        float left = bannerUntil - Time.time;
        float alpha = Mathf.Clamp01(left / 0.4f) * Mathf.Clamp01((1.5f - left) / 0.25f + 0.2f);
        var band = new Rect(0f, UiHeight * 0.27f, uiWidth, 118f);
        Fill(band, new Color(Ink.r, Ink.g, Ink.b, 0.6f * alpha));
        Fill(new Rect(0f, band.y, uiWidth, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.7f * alpha));
        Fill(new Rect(0f, band.yMax - 2f, uiWidth, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.7f * alpha));
        Text(new Rect(0f, band.y + 12f, uiWidth, 62f), StageName(pendingStage), 50, new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleCenter, FontStyle.Bold);
        Text(new Rect(0f, band.y + 76f, uiWidth, 28f), StageHint(pendingStage), 19, new Color(Soft.r, Soft.g, Soft.b, Soft.a * alpha), TextAnchor.MiddleCenter);
    }

    // The card shown when the recall rune is taken: a small animated picture and a short explanation.
    void DrawRuneTutorial()
    {
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(0f, 0f, 0f, 0.62f));
        float cx = uiWidth * 0.5f;
        var panel = new Rect(cx - 330f, 60f, 660f, 580f);
        Panel(panel, 0.92f);
        Frame(panel, new Color(Gold.r, Gold.g, Gold.b, 0.85f), 2f);

        // title with the rune's diamond
        var dc = new Vector2(cx - 205f, panel.y + 50f);
        RotateUi(45f, dc);
        Fill(new Rect(dc.x - 11f, dc.y - 11f, 22f, 22f), new Color(0.5f, 0.95f, 1f));
        ResetRotation();
        Text(new Rect(panel.x, panel.y + 24f, panel.width, 52f), "회수의 룬 획득!", 38, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        Text(new Rect(panel.x, panel.y + 80f, panel.width, 28f), "새로운 기술 : 무기 회수", 20, Color.white, TextAnchor.MiddleCenter);

        // picture: the weapon lies far away, Q pulls it back into the hand
        var pic = new Rect(panel.x + 30f, panel.y + 124f, panel.width - 60f, 200f);
        Fill(pic, new Color(0.1f, 0.12f, 0.17f, 1f));
        Frame(pic, new Color(1f, 1f, 1f, 0.16f));
        float ground = pic.y + 150f;
        Fill(new Rect(pic.x, ground, pic.width, pic.yMax - ground), new Color(0.3f, 0.32f, 0.4f));
        Fill(new Rect(pic.x, ground, pic.width, 3f), new Color(1f, 1f, 1f, 0.25f));

        float px = pic.x + 90f, wx = pic.xMax - 100f;
        // the player: a hooded figure with an empty hand
        Fill(new Rect(px - 14f, ground - 56f, 28f, 56f), new Color(0.32f, 0.46f, 0.72f));
        Fill(new Rect(px - 12f, ground - 78f, 24f, 24f), new Color(0.2f, 0.3f, 0.52f));
        Fill(new Rect(px - 5f, ground - 70f, 10f, 8f), new Color(0.85f, 0.88f, 0.95f));
        Text(new Rect(px - 50f, ground + 6f, 100f, 24f), "플레이어", 15, Soft, TextAnchor.MiddleCenter);

        // the weapon lying on the ground far away
        var wc = new Vector2(wx, ground - 8f);
        RotateUi(-12f, wc);
        Fill(new Rect(wx - 24f, ground - 12f, 48f, 8f), new Color(0.92f, 0.94f, 1f));
        Fill(new Rect(wx - 24f, ground - 12f, 14f, 8f), new Color(0.7f, 0.55f, 0.25f));
        ResetRotation();
        Text(new Rect(wx - 60f, ground + 6f, 120f, 24f), "떨어진 무기", 15, Soft, TextAnchor.MiddleCenter);

        // the way back: moving dots from the weapon to the player
        const int dots = 9;
        float phase = Time.unscaledTime * 6f;
        for (int i = 0; i < dots; i++)
        {
            float t = (i + 0.5f) / dots;
            float x = Mathf.Lerp(wx - 30f, px + 26f, t);
            float y = ground - 34f - Mathf.Sin(t * Mathf.PI) * 34f;
            float lit = Mathf.Clamp01(1f - Mathf.Abs(((phase - (dots - 1 - i)) % dots + dots) % dots) / 3f);
            Fill(new Rect(x - 4f, y - 4f, 8f, 8f), Color.Lerp(new Color(1f, 1f, 1f, 0.3f), Gold, lit));
        }
        Text(new Rect(px + 6f, ground - 72f, 30f, 28f), "◀", 22, Gold, TextAnchor.MiddleCenter);

        // the Q key above the middle of the path
        var key = new Rect((px + wx) * 0.5f - 22f, pic.y + 10f, 44f, 38f);
        Fill(new Rect(key.x, key.y + 4f, key.width, key.height), new Color(0f, 0f, 0f, 0.4f));
        Fill(key, new Color(0.2f, 0.23f, 0.3f));
        Frame(key, Gold, 2f);
        Text(key, "Q", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, false);
        Text(new Rect(key.x - 40f, key.yMax + 2f, key.width + 80f, 22f), "누르면", 15, Soft, TextAnchor.MiddleCenter);

        // explanation
        Text(new Rect(panel.x + 30f, panel.y + 340f, panel.width - 60f, 30f), "무기를 던지면 지형에 튕겨 멀리 떨어질 수 있습니다.", 18, Color.white, TextAnchor.MiddleCenter);
        Text(new Rect(panel.x + 30f, panel.y + 368f, panel.width - 60f, 30f), "그때 Q 키를 누르면 무기가 곧바로 손으로 돌아옵니다.", 18, Color.white, TextAnchor.MiddleCenter);
        Text(new Rect(panel.x + 30f, panel.y + 398f, panel.width - 60f, 30f), "돌아오는 길에 있는 적에게도 피해를 줍니다!", 18, new Color(1f, 0.6f, 0.45f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Text(new Rect(panel.x + 30f, panel.y + 436f, panel.width - 60f, 26f), "쿨타임 4초  ·  무기가 손에 없을 때만 쓸 수 있습니다", 16, Gold, TextAnchor.MiddleCenter);

        if (UiButton(new Rect(cx - 130f, panel.yMax - 76f, 260f, 50f), "확인  (Enter)", true)) CloseRuneTutorial();
    }

    // The boss's death sequence: letterbox bars and white flashes at the killing blow and the final burst.
    void DrawBossDeath()
    {
        var boss = Boss.Current;
        if (boss == null || !boss.Dying) return;
        float bar = Mathf.Clamp01(boss.DeathAge / 0.4f) * 56f;
        Fill(new Rect(0f, 0f, uiWidth, bar), Color.black);
        Fill(new Rect(0f, UiHeight - bar, uiWidth, bar), Color.black);
        float flash = boss.DeathFlash;
        if (flash > 0f) Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(1f, 1f, 1f, flash));
    }

    // The boss cutscene: letterbox bars and, once the golem has burst out of the boulder, its portrait and name.
    void DrawBossIntro()
    {
        var boss = Boss.Current;
        if (boss == null) return;
        float t = boss.IntroTime;

        // black bars slide in from the top and bottom
        float bar = Mathf.Clamp01(t / 0.5f) * 64f;
        Fill(new Rect(0f, 0f, uiWidth, bar), Color.black);
        Fill(new Rect(0f, UiHeight - bar, uiWidth, bar), Color.black);

        const float cardIn = 2.1f, cardOut = 4.3f;
        float alpha = Mathf.Clamp01((t - cardIn) / 0.4f) * Mathf.Clamp01((cardOut + 0.4f - t) / 0.4f);
        if (alpha <= 0f) return;

        float slide = (1f - Mathf.Clamp01((t - cardIn) / 0.4f)) * 60f;
        var band = new Rect(0f, UiHeight * 0.5f - 20f, uiWidth, 190f);
        Fill(band, new Color(Ink.r, Ink.g, Ink.b, 0.82f * alpha));
        Fill(new Rect(0f, band.y, uiWidth, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.85f * alpha));
        Fill(new Rect(0f, band.yMax - 2f, uiWidth, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.85f * alpha));

        // portrait
        var frame = new Rect(uiWidth * 0.5f - 400f - slide, band.y + 15f, 160f, 160f);
        Fill(frame, new Color(0.08f, 0.09f, 0.13f, alpha));
        Frame(frame, new Color(Gold.r, Gold.g, Gold.b, alpha), 3f);
        var sprite = boss.idleSprite;
        if (sprite != null)
        {
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            float aspect = tr.width / tr.height;
            float h = frame.height - 14f, w = h * aspect;
            if (w > frame.width - 14f) { w = frame.width - 14f; h = w / aspect; }
            var img = new Rect(frame.center.x - w * 0.5f, frame.center.y - h * 0.5f, w, h);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(img, tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            GUI.color = Color.white;
        }

        // name
        float tx = frame.xMax + 36f;
        Text(new Rect(tx + slide, band.y + 28f, 700f, 28f), "최종 보스", 20, new Color(Gold.r, Gold.g, Gold.b, alpha), TextAnchor.MiddleLeft, FontStyle.Bold);
        Text(new Rect(tx + slide, band.y + 58f, 700f, 70f), BossName, 60, new Color(1f, 1f, 1f, alpha), TextAnchor.MiddleLeft, FontStyle.Bold);
        Text(new Rect(tx + slide, band.y + 132f, 700f, 30f), BossTagline, 20, new Color(Soft.r, Soft.g, Soft.b, Soft.a * alpha), TextAnchor.MiddleLeft);

        if (t > 0.6f) Text(new Rect(0f, UiHeight - 58f, uiWidth - 24f, 24f), "Enter  건너뛰기", 14, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight);
    }

    const string BossName = "대지의 파수꾼";
    const string BossTagline = "잠들어 있던 거대한 바위 골렘";

    // The boss goes berserk: a red flash and a big word while it cannot be hurt.
    void DrawEnrageBanner()
    {
        var boss = Boss.Current;
        if (boss == null || !boss.InEnrageTransition) return;
        float age = boss.EnrageBannerAge;
        float fadeIn = Mathf.Clamp01(age / 0.2f);
        float fadeOut = Mathf.Clamp01((2.0f - age) / 0.4f);
        float a = fadeIn * fadeOut;

        // red edges pulse
        float pulse = 0.12f + 0.1f * Mathf.PingPong(Time.time * 5f, 1f);
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(0.8f, 0.05f, 0.05f, pulse * a));

        float punch = 1f + (1f - Mathf.Clamp01(age / 0.25f)) * 0.35f;
        int size = Mathf.RoundToInt(86f * punch);
        Text(new Rect(0f, UiHeight * 0.26f, uiWidth, 110f), "광분!", size, new Color(1f, 0.28f, 0.22f, a), TextAnchor.MiddleCenter, FontStyle.Bold);
        Text(new Rect(0f, UiHeight * 0.26f + 100f, uiWidth, 30f), "보스가 분노했습니다", 22, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter);
    }

    // ------------------------------------------------------------------ menus and end screens

    void DrawMenu(bool title)
    {
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(Ink.r, Ink.g, Ink.b, title ? 0.9f : 0.78f));
        float cx = uiWidth * 0.5f;

        if (title)
        {
            Text(new Rect(0f, 56f, uiWidth, 90f), "ONE WEAPON", 76, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text(new Rect(0f, 142f, uiWidth, 30f), "던지고, 줍고, 살아남아라", 21, Gold, TextAnchor.MiddleCenter);
        }
        else
        {
            Text(new Rect(0f, 64f, uiWidth, 70f), "일시정지", 54, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
        Fill(new Rect(cx - 90f, 186f, 180f, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.8f));

        string[] labels = title ? new[] { "시작  (Enter)", "종료" } : new[] { "계속하기  (Esc)", "다시 시작", "종료" };
        Action[] actions = title ? new Action[] { StartGame, QuitGame } : new Action[] { Resume, RestartGame, QuitGame };
        const float bw = 300f, bh = 50f, gap = 12f;
        float y = 210f;
        for (int i = 0; i < labels.Length; i++)
        {
            if (!UiButton(new Rect(cx - bw * 0.5f, y + i * (bh + gap), bw, bh), labels[i], i == 0)) continue;
            actions[i]();
            return;
        }

        DrawControls(cx, y + labels.Length * (bh + gap) + 22f);
    }

    void DrawControls(float cx, float top)
    {
        string[,] rows =
        {
            { "A  D", "좌우 이동" },
            { "Space", "점프  (S + Space : 발판 아래로)" },
            { "마우스", "조준" },
            { "좌클릭", "무기 던지기 (던진 뒤에는 직접 주워야 함)" },
            { "Shift / 우클릭", "마우스 방향으로 대쉬" },
            { "Q", "무기 회수 (회수의 룬을 얻은 뒤)" },
            { "R", "다시 시작" },
        };
        const float w = 560f, rowH = 28f;
        float h = 40f + rows.GetLength(0) * rowH + 8f;
        var panel = new Rect(cx - w * 0.5f, top, w, h);
        Panel(panel, 0.6f);
        Text(new Rect(panel.x + 16f, panel.y + 6f, 200f, 28f), "조작 안내", 18, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = panel.y + 40f + i * rowH;
            var key = new Rect(panel.x + 16f, y, 132f, 22f);
            Fill(key, new Color(0.2f, 0.23f, 0.3f, 0.95f));
            Frame(key, new Color(1f, 1f, 1f, 0.28f));
            Text(key, rows[i, 0], 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            Text(new Rect(panel.x + 162f, y - 1f, w - 176f, 24f), rows[i, 1], 16, new Color(1f, 1f, 1f, 0.88f));
        }
    }

    void DrawGameOver()
    {
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(0.25f, 0.02f, 0.04f, 0.62f));
        float cx = uiWidth * 0.5f;
        Text(new Rect(0f, UiHeight * 0.26f, uiWidth, 90f), "게임 오버", 72, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Text(new Rect(0f, UiHeight * 0.26f + 96f, uiWidth, 30f), "도달한 곳   " + StageName(stage), 22, Color.white, TextAnchor.MiddleCenter);
        if (UiButton(new Rect(cx - 150f, UiHeight * 0.26f + 150f, 300f, 52f), "다시 시작  (R)", true)) RestartGame();
    }

    void DrawGameClear()
    {
        // fades in, so the end of the boss fight does not cut hard to a menu
        if (wonShownAt < 0f) wonShownAt = Time.realtimeSinceStartup;
        float fade = Mathf.Clamp01((Time.realtimeSinceStartup - wonShownAt) / 0.9f);
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(Ink.r, Ink.g, Ink.b, 0.78f * fade));
        if (fade < 0.6f) return; // the card appears halfway through the fade
        float cx = uiWidth * 0.5f;
        var panel = new Rect(cx - 280f, UiHeight * 0.2f, 560f, 330f);
        Panel(panel, 0.85f);
        Frame(panel, new Color(Gold.r, Gold.g, Gold.b, 0.8f), 2f);
        Text(new Rect(panel.x, panel.y + 28f, panel.width, 80f), "게임 클리어!", 62, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        int sec = Mathf.RoundToInt(clearSeconds);
        Text(new Rect(panel.x, panel.y + 120f, panel.width, 24f), "클리어 시간", 17, Soft, TextAnchor.MiddleCenter);
        Text(new Rect(panel.x, panel.y + 142f, panel.width, 56f), (sec / 60).ToString("00") + " : " + (sec % 60).ToString("00"), 46, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (UiButton(new Rect(cx - 150f, panel.y + 222f, 300f, 50f), "다시 시작  (R)", true)) RestartGame();
        Text(new Rect(panel.x, panel.y + 286f, panel.width, 24f), "Esc  메뉴", 15, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
    }

    static string StageName(int s) => s switch
    {
        StageGround => "스테이지 1",
        StageAir => "스테이지 2",
        StageRune => "룬의 동굴",
        _ => "최종 스테이지 · 보스"
    };

    // Only used when rewardAfterEveryStage is turned on (it is off for the current game).
    void DrawRewards()
    {
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(0f, 0f, 0f, 0.65f));
        Text(new Rect(0f, UiHeight * 0.18f, uiWidth, 70f), "보상을 선택하세요", 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        if (lastHeal > 0)
            Text(new Rect(0f, UiHeight * 0.18f + 70f, uiWidth, 30f), "체력 " + lastHeal + " 회복", 18, HpGood, TextAnchor.MiddleCenter);

        const float w = 260f, h = 170f, gap = 30f;
        float total = choices.Count * w + (choices.Count - 1) * gap;
        float x0 = (uiWidth - total) * 0.5f;
        float y0 = UiHeight * 0.4f;
        for (int i = 0; i < choices.Count; i++)
        {
            var card = choices[i];
            var rect = new Rect(x0 + i * (w + gap), y0, w, h);
            string text = "[" + (i + 1) + "]\n" + TierPrefix[card.tier] + card.type.name + " +" + card.Value + "\n" + card.type.describe(card.Value);
            Fill(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), TierColor[card.tier]);
            if (UiButton(rect, text, false)) ChooseReward(i);
        }
    }
}
