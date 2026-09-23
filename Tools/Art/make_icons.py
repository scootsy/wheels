"""Generates the M1 placeholder icon set (original, simple, flat) into Assets/Game/Art/Icons.

Run from the repo root:  python Tools/Art/make_icons.py
Icons are drawn at 4x and downsampled for smooth edges. Re-running overwrites the PNGs;
Unity keeps their GUIDs because the .meta files are untouched.
"""
import math
import os

from PIL import Image, ImageDraw, ImageFont

S = 512          # drawing size
OUT = 128        # final size
DST = os.path.join("Assets", "Game", "Art", "Icons")
OUTLINE = (20, 16, 14, 255)
W = 18           # outline width at 4x


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save(img, name):
    img.resize((OUT, OUT), Image.LANCZOS).save(os.path.join(DST, name + ".png"))


def poly(d, pts, fill, outline=OUTLINE, width=W):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=outline, width=width, joint="curve")


def rot(pts, ang, cx=S / 2, cy=S / 2):
    a = math.radians(ang)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]


def star_pts(cx, cy, r1, r2, n=5, start=-90):
    pts = []
    for i in range(n * 2):
        r = r1 if i % 2 == 0 else r2
        a = math.radians(start + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def energy_a():
    img = canvas(); d = ImageDraw.Draw(img)
    d.rounded_rectangle((86, 86, 426, 426), radius=60, fill=(240, 140, 50, 255), outline=OUTLINE, width=W)
    d.rounded_rectangle((136, 136, 376, 376), radius=36, fill=(255, 185, 95, 255))
    d.polygon([(150, 150), (300, 150), (150, 300)], fill=(255, 225, 170, 255))
    save(img, "energy_a")


def energy_b():
    img = canvas(); d = ImageDraw.Draw(img)
    poly(d, [(256, 50), (462, 256), (256, 462), (50, 256)], (60, 195, 210, 255))
    d.polygon([(256, 120), (392, 256), (256, 392), (120, 256)], fill=(120, 225, 235, 255))
    d.polygon([(256, 120), (320, 184), (192, 184)], fill=(210, 250, 255, 255))
    save(img, "energy_b")


def hammer():
    img = canvas(); d = ImageDraw.Draw(img)
    poly(d, rot([(236, 190), (276, 190), (276, 470), (236, 470)], -35), (160, 110, 60, 255))
    poly(d, rot([(140, 90), (372, 90), (372, 210), (140, 210)], -35), (190, 195, 205, 255))
    d.polygon(rot([(160, 108), (352, 108), (352, 132), (160, 132)], -35), fill=(235, 240, 245, 255))
    save(img, "hammer")


def xp_star():
    img = canvas(); d = ImageDraw.Draw(img)
    poly(d, star_pts(256, 270, 230, 100), (150, 110, 245, 255))
    poly(d, star_pts(256, 270, 150, 66), (205, 180, 255, 255), outline=(150, 110, 245, 255), width=6)
    save(img, "xp_star")


def blank():
    img = canvas(); d = ImageDraw.Draw(img)
    for i in range(12):
        a0 = i * 30
        d.arc((110, 110, 402, 402), a0, a0 + 16, fill=(120, 112, 104, 255), width=22)
    save(img, "blank")


def padlock():
    img = canvas(); d = ImageDraw.Draw(img)
    red = (235, 70, 70, 255)
    d.arc((140, 50, 372, 300), 180, 360, fill=OUTLINE, width=70)
    d.arc((140, 50, 372, 300), 180, 360, fill=red, width=38)
    d.line([(158, 175), (158, 230)], fill=red, width=38)
    d.line([(354, 175), (354, 230)], fill=red, width=38)
    d.rounded_rectangle((96, 220, 416, 470), radius=40, fill=red, outline=OUTLINE, width=W)
    d.ellipse((230, 300, 282, 352), fill=OUTLINE)
    d.rectangle((246, 330, 266, 410), fill=OUTLINE)
    save(img, "padlock")


def crown():
    img = canvas(); d = ImageDraw.Draw(img)
    poly(d, [(70, 400), (70, 170), (160, 270), (256, 110), (352, 270), (442, 170), (442, 400)], (245, 200, 60, 255))
    d.rectangle((70, 370, 442, 430), fill=(220, 165, 40, 255), outline=OUTLINE, width=W)
    for cx, col in [(160, (230, 60, 60, 255)), (256, (60, 150, 230, 255)), (352, (60, 190, 90, 255))]:
        d.ellipse((cx - 28, 300, cx + 28, 356), fill=col, outline=OUTLINE, width=10)
    for cx, cy in [(70, 170), (256, 110), (442, 170)]:
        d.ellipse((cx - 26, cy - 26, cx + 26, cy + 26), fill=(255, 235, 140, 255), outline=OUTLINE, width=10)
    save(img, "crown")


def wall():
    img = canvas(); d = ImageDraw.Draw(img)
    rows = [(120, 200), (200, 280), (280, 360), (360, 440)]
    for i, (y0, y1) in enumerate(rows):
        x = 50 - (0 if i % 2 == 0 else 60)
        while x < 462:
            x0, x1 = max(50, x), min(462, x + 120)
            if x1 - x0 > 20:
                shade = 150 + (i * 17 + int(x)) % 40
                d.rectangle((x0, y0, x1, y1), fill=(shade, shade, shade + 10, 255), outline=OUTLINE, width=12)
            x += 120
    save(img, "wall")


def bomb():
    img = canvas(); d = ImageDraw.Draw(img)
    d.ellipse((80, 140, 400, 460), fill=(45, 45, 55, 255), outline=OUTLINE, width=W)
    d.ellipse((140, 200, 220, 280), fill=(110, 110, 125, 255))
    d.rectangle((260, 110, 330, 170), fill=(90, 90, 100, 255), outline=OUTLINE, width=12)
    d.line([(300, 110), (340, 60), (380, 70)], fill=(160, 110, 60, 255), width=16)
    poly(d, star_pts(400, 70, 60, 25, 6), (255, 200, 60, 255), width=8)
    save(img, "bomb")


def striker():
    img = canvas(); d = ImageDraw.Draw(img)
    d.ellipse((40, 250, 230, 440), fill=(70, 110, 200, 255), outline=OUTLINE, width=W)
    d.ellipse((110, 320, 160, 370), fill=(230, 200, 90, 255), outline=OUTLINE, width=8)
    poly(d, rot([(236, 40), (276, 40), (276, 330), (236, 330)], 35), (215, 220, 230, 255))
    poly(d, rot([(236, 40), (276, 40), (256, 0)], 35), (215, 220, 230, 255))
    poly(d, rot([(170, 320), (342, 320), (342, 360), (170, 360)], 35), (200, 160, 60, 255))
    poly(d, rot([(240, 360), (272, 360), (272, 440), (240, 440)], 35), (120, 70, 40, 255))
    px, py = rot([(256, 460)], 35)[0]
    d.ellipse((px - 30, py - 30, px + 30, py + 30), fill=(200, 160, 60, 255), outline=OUTLINE, width=12)
    save(img, "unit_striker")


def caster():
    img = canvas(); d = ImageDraw.Draw(img)
    d.line([(390, 90), (330, 480)], fill=OUTLINE, width=40)
    d.line([(390, 90), (330, 480)], fill=(150, 100, 55, 255), width=22)
    d.ellipse((350, 40, 440, 130), fill=(120, 230, 255, 255), outline=OUTLINE, width=12)
    poly(d, [(60, 400), (360, 400), (230, 60), (170, 150)], (85, 70, 170, 255))
    d.ellipse((30, 370, 390, 450), fill=(70, 55, 150, 255), outline=OUTLINE, width=W)
    poly(d, star_pts(215, 280, 44, 18), (250, 220, 90, 255), width=8)
    save(img, "unit_caster")


def generic(name, letter, col):
    img = canvas(); d = ImageDraw.Draw(img)
    d.ellipse((60, 60, 452, 452), fill=col, outline=OUTLINE, width=W)
    try:
        f = ImageFont.truetype("arialbd.ttf", 240)
    except OSError:
        f = ImageFont.load_default()
    d.text((256, 262), letter, font=f, fill=(255, 255, 255, 255), anchor="mm", stroke_width=10, stroke_fill=OUTLINE)
    save(img, name)


if __name__ == "__main__":
    os.makedirs(DST, exist_ok=True)
    energy_a(); energy_b(); hammer(); xp_star(); blank(); padlock(); crown(); wall(); bomb(); striker(); caster()
    for n, l, c in [("unit_ranger", "R", (80, 150, 80, 255)), ("unit_mason", "M", (150, 110, 70, 255)),
                    ("unit_shade", "S", (70, 60, 90, 255)), ("unit_mender", "+", (200, 200, 120, 255)),
                    ("unit_hexer", "H", (140, 50, 90, 255))]:
        generic(n, l, c)
    print("icons written to", DST)
