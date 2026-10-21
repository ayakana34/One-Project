from PIL import Image, ImageDraw
import gen_maps as g

K = 18
maps = g.maps
W = int(100 * K) + 40
H = int(14.5 * K)
sheet = Image.new("RGB", (W, H * len(maps)), (30, 30, 36))
d = ImageDraw.Draw(sheet)
col = {"G": (230, 70, 70), "Gp": (230, 70, 70), "E": (150, 90, 200), "F": (60, 190, 170), "S": (110, 190, 100)}
for i, m in enumerate(maps):
    oy = i * H

    def X(x): return 20 + (x + 50) * K

    def Y(y): return oy + 10 + (7 - y) * K

    bg = tuple(int(c * 255) for c in m.theme[0])
    gr = tuple(int(c * 255) for c in m.theme[1])
    d.rectangle([X(m.mn), Y(6.5), X(m.mx), Y(-6)], fill=bg)
    d.rectangle([X(m.mn - 1), Y(-6), X(m.mx + 1), Y(-6.8)], fill=gr)
    for (x, top, w) in m.blocks:
        d.rectangle([X(x - w / 2), Y(top), X(x + w / 2), Y(-6)], fill=gr, outline=(150, 150, 170))
    for (x, top, w) in m.plats:
        d.rectangle([X(x - w / 2), Y(top), X(x + w / 2), Y(top - 0.4)], fill=(190, 165, 110))
    ex = m.mx - 1.5
    d.rectangle([X(ex - 0.7), Y(-3.6), X(ex + 0.7), Y(-6)], fill=(255, 210, 80))
    d.ellipse([X(m.start) - 6, Y(-5.6) - 12, X(m.start) + 6, Y(-5.6)], fill=(90, 150, 220))
    for kind, x, a in m.enemies:
        if kind in ("G", "E"):
            yy = m.surface(x) + 0.5
        elif kind == "Gp":
            yy = a + 0.5
        elif kind == "S":
            yy = 4.3
        else:
            yy = a
        d.ellipse([X(x) - 6, Y(yy) - 6, X(x) + 6, Y(yy) + 6], fill=col[kind])
    if m.rune:
        rx, ry = m.rune
        d.polygon([(X(rx), Y(ry + 1.2)), (X(rx) + 9, Y(ry + 0.6)), (X(rx), Y(ry)), (X(rx) - 9, Y(ry + 0.6))], fill=(120, 240, 255))
    d.text((24, oy + 14), "%s  (%d wide)" % (m.name, m.mx - m.mn), fill=(240, 240, 240))
sheet.save("C:/Users/KGA01/UnityProject/One-Project/Library/HoodedDiag/maps_overview2.png")
print(sheet.size)
