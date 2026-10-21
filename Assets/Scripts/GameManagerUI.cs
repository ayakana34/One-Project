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
    float uiWidth, hpTrail = -1f;
    float lastTrailTime;

    static Texture2D solid;
    static Texture2D Solid => solid != null ? solid : (solid = Texture2D.whiteTexture);

    // ------------------------------------------------------------------ drawing helpers

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
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        if (State == GameState.Title) DrawMenu(true);
        else
        {
            DrawHud();
            if (State == GameState.Playing)
            {
                DrawExitHint(s);
                DrawStageBanner();
            }
            DrawRuneToast();
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
            Text(b, "보스   " + boss.Hp + " / " + boss.MaxHp, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        Text(new Rect(16f, UiHeight - 32f, 360f, 24f), "R 다시 시작   ·   Esc 메뉴", 14, new Color(1f, 1f, 1f, 0.4f));
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

    void DrawRuneToast()
    {
        if (runeMessageUntil <= Time.unscaledTime || State == GameState.Lost) return;
        float left = runeMessageUntil - Time.unscaledTime;
        float alpha = Mathf.Clamp01(left / 0.6f);
        var r = new Rect(uiWidth * 0.5f - 270f, 108f, 540f, 76f);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        Panel(r, 0.8f);
        Frame(r, new Color(Gold.r, Gold.g, Gold.b, 0.8f * alpha), 2f);
        GUI.color = Color.white;

        var m = GUI.matrix;
        var c = new Vector2(r.x + 40f, r.center.y);
        GUIUtility.RotateAroundPivot(45f, c);
        Fill(new Rect(c.x - 11f, c.y - 11f, 22f, 22f), new Color(0.5f, 0.95f, 1f, alpha));
        GUI.matrix = m;

        Text(new Rect(r.x + 72f, r.y + 8f, r.width - 80f, 32f), "회수의 룬 획득!", 26, new Color(Gold.r, Gold.g, Gold.b, alpha), TextAnchor.MiddleLeft, FontStyle.Bold);
        Text(new Rect(r.x + 72f, r.y + 42f, r.width - 80f, 26f), "Q 키로 던진 무기를 손으로 불러옵니다 (쿨타임 4초)", 16, new Color(1f, 1f, 1f, alpha));
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
        Fill(new Rect(0f, 0f, uiWidth, UiHeight), new Color(Ink.r, Ink.g, Ink.b, 0.78f));
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
        StageRune => "스테이지 3 · 회수의 룬",
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
