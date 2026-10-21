import math
from PIL import Image, ImageDraw

FLOOR = -6.0
T1, T2, T3 = -3.6, -1.2, 1.2
MAX_CLIMB, MAX_GAP, MAX_WALL = 2.5, 3.5, 2.3
SHOOTER_Y = 4.3


def pyramid(cx, base_w, tops, step_w=2.0):
    """Stacked blocks: widths shrink by 2*step_w per tier; tops are given bottom to top."""
    out = []
    w = base_w
    for t in tops:
        out.append((cx, t, w))
        w -= 2 * step_w
    return out


class Map:
    def __init__(self, name, mn, mx, start, theme):
        self.name, self.mn, self.mx, self.start, self.theme = name, mn, mx, start, theme
        self.blocks, self.plats, self.enemies = [], [], []
        self.rune = None

    def B(self, *bl):
        self.blocks += list(bl)
        return self

    def P(self, *pl):
        self.plats += list(pl)
        return self

    def surface(self, x):
        top = FLOOR
        for (bx, bt, bw) in self.blocks:
            if abs(x - bx) <= bw / 2 and bt > top:
                top = bt
        return top

    def G(self, x): self.enemies.append(("G", x, None)); return self
    def Gp(self, x, top): self.enemies.append(("Gp", x, top)); return self
    def E(self, x): self.enemies.append(("E", x, None)); return self
    def F(self, x, y): self.enemies.append(("F", x, y)); return self
    def S(self, x): self.enemies.append(("S", x, None)); return self

    def validate(self):
        pr = []
        surfaces = [(self.mn, self.mx, FLOOR)]
        surfaces += [(bx - bw / 2, bx + bw / 2, bt) for (bx, bt, bw) in self.blocks]
        surfaces += [(px - pw / 2, px + pw / 2, pt) for (px, pt, pw) in self.plats]
        reached = [False] * len(surfaces)
        reached[0] = True
        q = [0]
        while q:
            a = surfaces[q.pop()]
            for i, s in enumerate(surfaces):
                if reached[i]:
                    continue
                gap = max(0.0, max(a[0], s[0]) - min(a[1], s[1]))
                if gap <= MAX_GAP and s[2] - a[2] <= MAX_CLIMB:
                    reached[i] = True
                    q.append(i)
        for i, s in enumerate(surfaces):
            if not reached[i]:
                pr.append("unreachable %.1f..%.1f top %.1f" % s)
        x = self.mn
        while x < self.mx - 0.1:
            d = abs(self.surface(x + 0.1) - self.surface(x))
            if d > MAX_WALL:
                pr.append("wall %.1f at x=%.1f" % (d, x))
                x += 0.5
            x += 0.1
        exit_x = self.mx - 1.5 if self.name != "Rune" else self.mx - 1.5
        if self.surface(self.start) > FLOOR + 0.01:
            pr.append("start not on floor")
        xx = exit_x - 1.5
        while xx <= exit_x + 1.5:
            if self.surface(xx) > FLOOR + 0.01:
                pr.append("terrain at exit")
                break
            xx += 0.5
        # platforms vs blocks: headroom
        for (px, pt, pw) in self.plats:
            for (bx, bt, bw) in self.blocks:
                if abs(px - bx) < (pw + bw) / 2 and (pt - 0.4) < bt + 1.1:
                    pr.append("platform x=%.1f top %.1f too low over block x=%.1f top %.1f" % (px, pt, bx, bt))
            if pt > 4.5:
                pr.append("platform too high x=%.1f" % px)
        for i, (px, pt, pw) in enumerate(self.plats):
            for (qx, qt, qw) in self.plats[i + 1:]:
                if abs(px - qx) < (pw + qw) / 2 and abs(pt - qt) < 1.5:
                    pr.append("platforms x=%.1f/%.1f too close vertically" % (px, qx))
        for (bx, bt, bw) in self.blocks:
            if bt > 4.0:
                pr.append("block too high")
        for (k, x, a) in self.enemies:
            if k in ("G", "E"):
                for (bx, bt, bw) in self.blocks:
                    if abs(abs(x - bx) - bw / 2) < 0.45:
                        pr.append("monster x=%.1f on block edge (block x=%.1f w=%.1f)" % (x, bx, bw))
                        break
            if k == "Gp":
                ok = any(abs(x - px) < pw / 2 - 0.3 and abs(a - pt) < 0.01 for (px, pt, pw) in self.plats)
                if not ok:
                    pr.append("Gp not on platform x=%.1f" % x)
            if x < self.mn + 1 or x > self.mx - 1:
                pr.append("monster outside x=%.1f" % x)
            if k in ("G", "E") and abs(x - self.start) < 5:
                pr.append("monster too close to start x=%.1f" % x)
        return pr


def theme(bg, ground, stripe):
    return (bg, ground, stripe)


def stairs(x0, direction, n, w, first_top, rise):
    """n steps of width w starting at edge x0 (direction +1/-1), each `rise` higher than the one before."""
    out = []
    for i in range(n):
        out.append((x0 + direction * (i * w + w / 2), first_top + i * rise, w))
    return out


# ------------------------------------------------------------------ stage 1: the outpost (72 wide)
s1 = Map("Stage 1", -36, 36, -33, theme((0.17, 0.18, 0.24), (0.36, 0.38, 0.46), (0.22, 0.23, 0.30)))
s1.B(*stairs(-18, 1, 3, 2, -5.0, 1.0))                      # the guard hill: stairs up, plateau, stairs down
s1.B((-9, -2.0, 6))
s1.B(*stairs(-6, 1, 3, 2, -3.0, -1.0))
s1.P((-9, 0.4, 4))                                          # a lookout above the plateau
s1.P((2, -3.6, 3), (5.5, -1.2, 3), (9, 1.2, 3))             # the sky route over the open ground to the tower
s1.B(*stairs(10, 1, 3, 2, -5.0, 1.0))                       # the tower
s1.B((18, -2.0, 4))
s1.B(*stairs(20, 1, 3, 2, -3.0, -1.0))
s1.P((18, 0.4, 4))
s1.G(-27).G(-23).G(-19.5).G(-8).G(2).G(6).G(23).G(27)
s1.E(-10.5).E(4).E(18)
s1.F(-15, 3).F(-5, 3.5).F(4, 3.5).F(13, 3).F(22, 2.5).F(29, 3)
s1.S(-24).S(18)

# ------------------------------------------------------------------ stage 2: the citadel (92 wide)
s2 = Map("Stage 2", -46, 46, -43, theme((0.20, 0.16, 0.21), (0.44, 0.36, 0.44), (0.27, 0.21, 0.28)))
s2.B(*stairs(-32, 1, 5, 2, -5.0, 1.0))                      # the rampart: stairs up, a long walkway, stairs down
s2.B((-14, -1.0, 16))
s2.B(*stairs(-6, 1, 4, 2, -2.0, -1.0))
s2.P((-14, 1.4, 6))
s2.P((8, -1.6, 3))
s2.P((12.5, -3.6, 3))
s2.B(*stairs(14, 1, 5, 2, -5.0, 1.0))                       # the keep
s2.B((27, -1.0, 6))
s2.B(*stairs(30, 1, 4, 2, -2.0, -1.0))
s2.P((27, 1.4, 4))
s2.G(35).G(-33.5).G(-29).G(-18).G(-10).G(4).G(8).G(19).G(40)
s2.E(-14).E(-3).E(12).E(27)
s2.F(-36, 3).F(-24, 3.5).F(-12, 3).F(0, 3.5).F(12, 2.5).F(24, 3).F(34, 3.5)
s2.S(-30).S(-12).S(22).S(32)

# ------------------------------------------------------------------ rune room: a short flat corridor, no monsters
rr = Map("Rune", -10, 10, -7.5, theme((0.12, 0.19, 0.20), (0.32, 0.46, 0.46), (0.17, 0.26, 0.27)))
rr.rune = (1.5, -6.0)  # floats just above the floor in the middle of the room

maps = [s1, s2, rr]

if __name__ == "__main__":
    for m in maps:
        print(m.name, "blocks", len(m.blocks), "plats", len(m.plats), "enemies", len(m.enemies))
        for p in m.validate():
            print("   PROBLEM:", p)
