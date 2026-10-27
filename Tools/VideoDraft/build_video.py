import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

FF = r"C:\Users\KGA01\AppData\Local\Packages\PythonSoftwareFoundation.Python.3.12_qbz5n2kfra8p0\LocalCache\local-packages\Python312\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe"
SRC = r"C:\Users\KGA01\Videos\Captures\One Weapon 2026-10-27 12-34-19.mp4"
W = r"C:\Users\KGA01\AppData\Local\Temp\claude\C--Users-KGA01-UnityProject-One-Project\185f9b4a-8b55-4f7a-a5fa-f8b437d08d28\scratchpad"
V = os.path.join(W, "vid")
SRC_CTRL = r"C:\Users\KGA01\Videos\Captures\One Weapon 2026-10-27 14-45-23.mp4"
SRC_STAGE2 = r"C:\Users\KGA01\Videos\Captures\One Weapon 2026-10-27 14-50-00.mp4"
OUT = r"C:\Users\KGA01\Videos\One Weapon 개인프로젝트과제_초안3.mp4"
os.makedirs(os.path.join(V, "assets"), exist_ok=True)
os.makedirs(os.path.join(V, "seg"), exist_ok=True)

FONT_B = r"C:\Windows\Fonts\malgunbd.ttf"
FONT_R = r"C:\Windows\Fonts\malgun.ttf"
GOLD = (255, 205, 90)
WHITE = (245, 245, 248)
SOFT = (190, 194, 205)
RED = (255, 110, 100)
CARD_BG_TOP = (16, 18, 28)
CARD_BG_BOT = (34, 26, 44)


def font(size, bold=True):
    return ImageFont.truetype(FONT_B if bold else FONT_R, size)


def wrap(draw, text, fnt, max_w):
    # wrap at spaces so a word is never split across two lines; a single word longer than a line is split by character
    lines, cur = [], ""
    for word in text.split(" "):
        trial = word if not cur else cur + " " + word
        if draw.textlength(trial, font=fnt) <= max_w:
            cur = trial
            continue
        if cur:
            lines.append(cur)
            cur = ""
        if draw.textlength(word, font=fnt) <= max_w:
            cur = word
            continue
        piece = ""
        for ch in word:
            if draw.textlength(piece + ch, font=fnt) > max_w and piece:
                lines.append(piece)
                piece = ch
            else:
                piece += ch
        cur = piece
    if cur:
        lines.append(cur)
    return lines


# ---------------------------------------------------------------- images
def gradient_bg():
    img = Image.new("RGB", (1920, 1080), CARD_BG_TOP)
    d = ImageDraw.Draw(img)
    for y in range(1080):
        t = y / 1079.0
        c = tuple(int(CARD_BG_TOP[i] + (CARD_BG_BOT[i] - CARD_BG_TOP[i]) * t) for i in range(3))
        d.line([(0, y), (1920, y)], fill=c)
    return img


def card_title(d, text, y=70):
    f = font(64)
    w = d.textlength(text, font=f)
    d.text(((1920 - w) / 2, y), text, font=f, fill=GOLD)
    d.line([(760, y + 92), (1160, y + 92)], fill=(255, 205, 90), width=3)


def draw_rows(d, rows, x0, y0, label_w, label_size=36, text_size=36, gap=26, max_w=1500, label_color=GOLD):
    fl, ft = font(label_size), font(text_size, bold=False)
    y = y0
    for label, text, *rest in rows:
        color = rest[0] if rest else WHITE
        lines = wrap(d, text, ft, max_w - label_w)
        d.text((x0, y), label, font=fl, fill=label_color)
        for i, ln in enumerate(lines):
            d.text((x0 + label_w, y + i * (text_size + 12)), ln, font=ft, fill=color)
        y += max(len(lines), 1) * (text_size + 12) + gap
    return y


def rows_height(d, rows, label_w, text_size, gap, max_w):
    ft = font(text_size, bold=False)
    total = 0
    for label, text, *rest in rows:
        total += max(len(wrap(d, text, ft, max_w - label_w)), 1) * (text_size + 12) + gap
    return total - gap


def make_card(name, title, rows, label_w=300, label_size=36, text_size=36, gap=26, y0=230, note=None, x0=200):
    img = gradient_bg()
    d = ImageDraw.Draw(img)
    card_title(d, title)
    # centre the block between the title and the bottom note area
    h = rows_height(d, rows, label_w, text_size, gap, 1920 - 2 * x0)
    top, bottom = 230, (930 if note else 1030)
    y0 = max(top, int(top + (bottom - top - h) / 2))
    draw_rows(d, rows, x0, y0, label_w, label_size, text_size, gap, max_w=1920 - 2 * x0)
    if note:
        f = font(32)
        w = d.textlength(note, font=f)
        d.rectangle([(1920 - w) / 2 - 24, 940, (1920 + w) / 2 + 24, 1010], outline=RED, width=3)
        d.text(((1920 - w) / 2, 953), note, font=f, fill=RED)
    path = os.path.join(V, "assets", name + ".png")
    img.save(path)
    return path


def make_section(name, text, sub=None):
    img = gradient_bg()
    d = ImageDraw.Draw(img)
    f = font(96)
    w = d.textlength(text, font=f)
    d.text(((1920 - w) / 2, 440), text, font=f, fill=WHITE)
    d.line([(800, 570), (1120, 570)], fill=GOLD, width=4)
    if sub:
        f2 = font(40, bold=False)
        w2 = d.textlength(sub, font=f2)
        d.text(((1920 - w2) / 2, 600), sub, font=f2, fill=SOFT)
    path = os.path.join(V, "assets", name + ".png")
    img.save(path)
    return path


def make_cover():
    frame = os.path.join(V, "assets", "title_frame.png")
    subprocess.run([FF, "-hide_banner", "-loglevel", "error", "-y", "-ss", "70", "-i", SRC, "-frames:v", "1", frame], check=True)
    bg = Image.open(frame).convert("RGB").resize((1920, 1080))
    bg = ImageEnhance.Brightness(bg).enhance(0.30).filter(ImageFilter.GaussianBlur(7))
    d = ImageDraw.Draw(bg)
    f = font(150)
    t = "ONE WEAPON"
    d.text(((1920 - d.textlength(t, font=f)) / 2, 270), t, font=f, fill=WHITE)
    f = font(56)
    t = "개인 프로젝트 과제"
    d.text(((1920 - d.textlength(t, font=f)) / 2, 470), t, font=f, fill=GOLD)
    f = font(44, bold=False)
    for i, t in enumerate(["제작자  김슬빈", "1인 개발  ·  2026.09.29 ~ 10.28  ·  Unity 6  ·  PC"]):
        d.text(((1920 - d.textlength(t, font=f)) / 2, 590 + i * 70), t, font=f, fill=WHITE)
    path = os.path.join(V, "assets", "cover.png")
    bg.save(path)
    return path


def make_overlay(name, caption, tag=None, speed=1.0):
    img = Image.new("RGBA", (1920, 1080), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # the caption band sits on the bottom of the game view and the empty strip below it
    d.rectangle([0, 952, 1920, 1080], fill=(8, 9, 14, 214))
    f = font(40)
    lines = wrap(d, caption, f, 1740)[:2]
    total = len(lines) * 50
    y = 952 + (128 - total) / 2 - 2
    for i, ln in enumerate(lines):
        w = d.textlength(ln, font=f)
        d.text(((1920 - w) / 2 + 2, y + i * 50 + 2), ln, font=f, fill=(0, 0, 0, 200))
        d.text(((1920 - w) / 2, y + i * 50), ln, font=f, fill=WHITE)
    chips = []
    if tag:
        chips.append(tag)
    if speed != 1.0:
        chips.append(("%g배속" % speed) if speed > 1 else ("%g배속 슬로" % speed))
    fx = font(30)
    x = 1920 - 28
    for text in reversed(chips):
        w = d.textlength(text, font=fx)
        d.rounded_rectangle([x - w - 28, 20, x, 70], radius=12, fill=(8, 9, 14, 210), outline=(255, 205, 90, 255), width=2)
        d.text((x - w - 14, 26), text, font=fx, fill=GOLD)
        x -= w + 28 + 10
    path = os.path.join(V, "assets", name + ".png")
    img.save(path)
    return path


# ---------------------------------------------------------------- encoding
ENC = ["-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p", "-r", "30", "-an", "-video_track_timescale", "30000"]
segments = []


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(r.stderr[-1500:])
        raise SystemExit("ffmpeg failed")


def add_card(png, dur):
    idx = len(segments) + 1
    out = os.path.join(V, "seg", "s%03d.mp4" % idx)
    vf = "fade=t=in:st=0:d=0.4,fade=t=out:st=%.2f:d=0.4,format=yuv420p" % (dur - 0.4)
    run([FF, "-hide_banner", "-loglevel", "error", "-y", "-loop", "1", "-framerate", "30", "-t", "%.2f" % dur, "-i", png, "-vf", vf] + ENC + [out])
    segments.append((out, dur))


def add_clip(start, src_dur, caption, tag=None, speed=1.0, src=None):
    src = src or SRC
    idx = len(segments) + 1
    out_dur = src_dur / speed
    ov = make_overlay("ov%03d" % idx, caption, tag, speed)
    out = os.path.join(V, "seg", "s%03d.mp4" % idx)
    fc = ("[0:v]scale=1920:1012:flags=lanczos,setpts=PTS/%s,fps=30,pad=1920:1080:0:0:color=black[v];"
          "[v][1:v]overlay=0:0:format=auto:shortest=1,fade=t=in:st=0:d=0.25,fade=t=out:st=%.2f:d=0.25,format=yuv420p[o]") % (speed, out_dur - 0.25)
    run([FF, "-hide_banner", "-loglevel", "error", "-y", "-ss", "%.2f" % start, "-t", "%.2f" % src_dur, "-i", src,
         "-loop", "1", "-framerate", "30", "-i", ov, "-filter_complex", fc, "-map", "[o]"] + ENC + [out])
    segments.append((out, out_dur))


# ---------------------------------------------------------------- the storyboard
def build():
    # S1 maker info
    add_card(make_cover(), 7)

    # S2 overview
    add_card(make_card("c_overview", "게임 개요", [
        ("게임 제목", "One Weapon (가제)"),
        ("장르", "2D 횡스크롤 액션 (스테이지 클리어형, 보스전)"),
        ("컨셉", "하나뿐인 무기를 던져 싸우고, 튕겨 나간 무기를 주워 다시 던진다"),
        ("핵심 재미", "“던진 무기를 되찾는 순간의 판단”"),
    ], label_w=360, label_size=48, text_size=48, gap=60, y0=260), 9)
    add_clip(5.5, 8, "무기가 하나뿐이라, 던지는 순간 무방비가 되고 되찾아야 다시 싸울 수 있다", "② 게임 개요")

    # S3 controls
    add_card(make_card("c_controls", "조작 방법", [
        ("A / D", "좌우 이동"),
        ("Space", "점프"),
        ("마우스 이동", "조준"),
        ("마우스 좌클릭", "무기 던지기 (던진 뒤에는 직접 주워야 함)"),
        ("Shift / 우클릭", "마우스 방향 대쉬 (3회 충전, 연타하면 이어서 나감)"),
        ("Q", "던진 무기 회수 (회수의 룬을 먹은 뒤, 쿨타임 4초)"),
        ("R / Esc", "다시 시작 / 일시정지 메뉴"),
    ], label_w=400, label_size=42, text_size=42, gap=30, y0=235), 11)
    add_clip(3.6, 4.9, "A·D 로 이동하고, 마우스로 몬스터를 조준한다", "③ 조작 방법", src=SRC_CTRL)
    add_clip(8.5, 9.0, "좌클릭으로 무기를 던진다 — 맞으면 피해를 주고 튕겨 나가며, 바닥에 떨어진 무기는 직접 주워야 한다  (Space 점프)", "③ 조작 방법", src=SRC_CTRL)
    add_clip(17.5, 3.3, "Shift 또는 우클릭으로 마우스 방향 대쉬 — 3번 연속으로 쓸 수 있다", "③ 조작 방법", src=SRC_CTRL)

    # S4 key play
    add_card(make_section("sec4", "핵심 플레이 화면", "스테이지 1  →  룬의 동굴  →  스테이지 2  →  보스"), 3)
    add_clip(5.5, 24.5, "스테이지 1 · 몬스터 12마리를 모두 처치하면 출구가 열린다", "④ 핵심 플레이")
    add_clip(30, 56, "빨리 감기", "④ 핵심 플레이", speed=6)
    add_clip(86, 7.5, "출구가 열리면 다음 구역으로 이동한다", "④ 핵심 플레이")
    add_clip(94, 10.5, "룬의 동굴 · 회수의 룬을 먹으면 Q 회수를 쓸 수 있게 된다", "④ 핵심 플레이")
    add_clip(104.5, 0.01 + 0.0, "", "④ 핵심 플레이") if False else None
    add_clip(110.5, 3, "획득 카드가 사용법을 알려 준다: 던진 무기를 Q로 즉시 부르고, 오는 길의 적도 맞힌다", "④ 핵심 플레이")
    add_clip(3.6, 16.4, "스테이지 2 · 높은 성벽과 탑을 오르내리며 몬스터 15마리와 싸운다", "④ 핵심 플레이", src=SRC_STAGE2)
    add_clip(20, 30, "빨리 감기", "④ 핵심 플레이", speed=4, src=SRC_STAGE2)
    add_clip(50, 9.6, "마지막 몬스터를 처치하면 출구가 열리고 보스 스테이지로 이어진다", "④ 핵심 플레이", src=SRC_STAGE2)
    add_clip(162.5, 5, "최종 보스 · 대지의 파수꾼 — 바위가 깨지며 등장한다", "④ 핵심 플레이")
    add_clip(167.5, 11.5, "패턴은 돌진과 점프 내려찍기, 둘 다 예고를 보고 피할 수 있다", "④ 핵심 플레이")
    add_clip(179, 4, "체력이 절반이 되면 광분한다 — 돌진 2연속, 충격파 2번", "④ 핵심 플레이")
    add_clip(183, 12.5, "광분한 보스와의 마지막 전투", "④ 핵심 플레이")
    add_clip(195.5, 4.5, "처치 연출 — 슬로 모션과 폭발 뒤 클리어", "④ 핵심 플레이")
    add_clip(200, 4.5, "게임 클리어 · 클리어 시간이 표시된다", "④ 핵심 플레이")

    # S5 features
    add_card(make_section("sec5", "주요 기능 및 시스템", "직접 구현한 기능과 실제 동작 화면"), 3)
    add_clip(6, 4, "① 투척과 튕김 — 맞으면 랜덤 각도로 반사되고, 튕겨 다니는 중에도 피해를 준다", "⑤ 주요 기능", speed=0.5)
    add_card(make_card("c_dash", "② 대쉬", [
        ("충전", "3회 충전, 1.0초마다 1회 회복"),
        ("무적", "대쉬 중에는 무적"),
        ("연속 사용", "대쉬 중에 누른 입력을 0.25초 기억해 이어서 나간다"),
    ], label_w=290, label_size=46, text_size=46, gap=60, y0=300), 5)
    add_clip(18.2, 2.4, "② 대쉬 — 3번 연속으로 나가면 충전이 비고, 이후 1초마다 1칸씩 다시 찬다", "⑤ 주요 기능", speed=0.5, src=SRC_CTRL)
    add_clip(13.8, 2.4, "③ 회수 스킬 — Q를 누르면 멀리 떨어진 무기가 즉시 손으로 돌아오고, 오는 길의 적도 맞힌다", "⑤ 주요 기능", speed=0.5, src=SRC_STAGE2)
    add_clip(26.0, 2.0, "③ 회수 스킬 — 하늘색 궤적이 남고, 쿨타임(4초)은 화면 왼쪽 위에 표시된다", "⑤ 주요 기능", speed=0.5, src=SRC_STAGE2)
    add_clip(57.0, 2.5, "③ 회수 스킬 — 마지막 몬스터도 회수로 처치", "⑤ 주요 기능", speed=0.5, src=SRC_STAGE2)
    add_clip(138, 10, "④ 몬스터 4종 — 졸병·정예(추적, 뛰어오름), 비행, 슈터(예고 뒤 조준 사격)", "⑤ 주요 기능")
    add_clip(163, 4, "⑤ 보스 연출 — 바위가 깨지며 등장하는 컷신과 이름 카드", "⑤ 주요 기능")
    add_clip(179.5, 3, "⑤ 보스 연출 — 체력 절반에서 멈춰 포효하는 광분 전환", "⑤ 주요 기능")
    add_clip(195.5, 4, "⑤ 보스 연출 — 슬로 모션 처치와 폭발", "⑤ 주요 기능")
    add_clip(87, 5, "⑥ 스테이지 구조와 UI — 남은 적 수와 출구 상태를 표시한다. 모든 UI는 코드로 직접 그렸다", "⑤ 주요 기능")

    # S6 flow
    add_card(make_section("sec6", "시작부터 종료까지의 흐름", "시작 화면  →  플레이  →  결과 화면"), 3)
    add_clip(1, 4, "① 시작 화면 — Enter로 시작, 조작 안내를 함께 보여 준다", "⑥ 전체 흐름")
    add_clip(20, 3, "② 플레이 — 스테이지 1 → 룬의 동굴 → 스테이지 2 → 보스", "⑥ 전체 흐름")
    add_clip(200, 4, "③ 클리어 화면 — 보스를 쓰러뜨리면 클리어 시간이 나온다", "⑥ 전체 흐름")
    add_clip(212, 5.5, "④ 게임 오버 — 체력이 0이 되면 도달한 곳을 보여 주고 R로 다시 시작한다", "⑥ 전체 흐름")

    # S7 dev process
    add_card(make_section("sec7", "개발 과정"), 2.5)
    add_card(make_card("c_tools", "사용 엔진 · 도구", [
        ("엔진", "Unity 6.3 (URP, Input System, 2D 물리)"),
        ("언어", "C#  (스테이지 맵 설계 도구는 Python)"),
        ("아트 도구", "VARCO (AI 도트·3D 초안),  Blender 5.2 (3D 보정)"),
        ("개발 보조", "Claude Code + Unity MCP"),
        ("형상 관리", "GitHub"),
    ], label_w=340, label_size=46, text_size=46, gap=56, y0=280), 9)
    add_card(make_card("c_made", "직접 제작한 부분과 외부 활용", [
        ("직접 제작", "게임 규칙과 로직 전체 (투척·튕김·회수, 대쉬, 몬스터 AI, 보스 패턴과 연출), 스테이지 맵 설계·검증 도구, 모든 UI와 이펙트"),
        ("AI 생성 후 정리", "몬스터·보스 도트 (VARCO 초안 → 격자 정리), 3D 후드 캐릭터와 단검 (VARCO 생성 → Blender 보정)"),
        ("엔진·라이브러리", "Unity 6.3, URP, Input System"),
        ("확인 필요", "3D 캐릭터 애니메이션의 출처 / 효과음·음악은 사용하지 않음", RED),
    ], label_w=430, label_size=40, text_size=40, gap=44, y0=250), 13)
    add_card(make_card("c_sched", "개발 일정", [
        ("MVP  ~ 10/8", "도형 아트로 던지기·튕김·대쉬·보스를 먼저 검증"),
        ("CBT  10/9 ~ 10/16", "넓은 스테이지 맵, 도트 몬스터, 3D 캐릭터와 단검"),
        ("OBT  10/17 ~ 10/24", "한 판 클리어형으로 정리, 룬의 동굴, 보스 컷신·연출, UI"),
        ("출시  10/25 ~ 10/28", "빌드 안정화, 발표와 영상 제작 (발표 10/27)"),
    ], label_w=540, label_size=44, text_size=44, gap=54, y0=290, x0=150), 9)
    add_card(make_card("c_prob1", "개발 중 겪은 문제와 해결 (1/2)", [
        ("1", "튕겨 나간 무기를 줍기 힘들다  →  회수 스킬(Q)과 회수 피해 추가, 스테이지 1을 낮은 지형으로 재구성, 구덩이와 좁은 틈 제거"),
        ("2", "빠르게 던지니 튕김도 더 멀리 간다  →  튕김 속도를 비행 속도와 분리해서 따로 정함"),
        ("3", "몬스터가 늦게 나타나 바로 맞는다  →  스테이지 시작 즉시 배치, 등장 직후 0.75초는 접촉 피해 없음, 시작 후 1초 무적"),
        ("4", "대쉬를 연타하면 한 번만 나간다  →  대쉬 중 입력을 0.25초 기억해 이어서 실행, 충전 3회로 확대"),
    ], label_w=80, label_size=44, text_size=40, gap=40, y0=250), 14)
    add_card(make_card("c_prob2", "개발 중 겪은 문제와 해결 (2/2)", [
        ("5", "보스 판정이 몸보다 크게 느껴진다  →  스프라이트를 분석해 위쪽 빈 공간을 덮던 판정 상자를 몸에 맞춰 줄임"),
        ("6", "클리어 시간이 영상보다 길다  →  시작 화면과 일시정지 시간까지 재던 것을 고쳐 플레이 시간만 측정"),
        ("7", "발판을 밟고 싸우는 플레이가 거의 없다  →  발판을 빼고 큰 지형 중심으로 맵을 다시 설계"),
    ], label_w=80, label_size=46, text_size=42, gap=56, y0=280), 11)

    # closing: the cover again (the self-assessment / improvement plan card was removed on request)
    add_card(make_cover(), 4)


def concat():
    lst = os.path.join(V, "list.txt")
    with open(lst, "w", encoding="utf-8") as f:
        for path, _ in segments:
            f.write("file '%s'\n" % path.replace("\\", "/"))
    run([FF, "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", "-movflags", "+faststart", OUT])
    total = sum(d for _, d in segments)
    print("segments:", len(segments), " planned length: %d:%02d" % (total // 60, total % 60))
    print("output:", OUT, "%.1f MB" % (os.path.getsize(OUT) / 1048576))


if __name__ == "__main__":
    build()
    concat()
