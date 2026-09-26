"""Generates the UI kit sprites (original, neutral shapes, tinted in Unity) into Assets/Game/Art/UI.

Run from the repo root:  python Tools/Art/make_ui.py
Shapes are drawn white at 4x and downsampled; Unity tints them with Image.color. Nine-slice borders are set on
import by Assets/Game/Editor/IconImport.cs (UiBorders). Re-running keeps GUIDs (the .meta files are untouched).
"""
import os

from PIL import Image, ImageDraw, ImageFilter

DST = os.path.join("Assets", "Game", "Art", "UI")
K = 4  # supersampling


def save(img, name, size):
    img.resize(size, Image.LANCZOS).save(os.path.join(DST, name + ".png"))


def rounded(w, h, r, fill=(255, 255, 255, 255), outline=None, width=0):
    img = Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    inset = width * K // 2 if outline else 0
    d.rounded_rectangle((inset, inset, w * K - 1 - inset, h * K - 1 - inset), radius=r * K, fill=fill, outline=outline, width=width * K)
    return img


def panel():
    save(rounded(64, 64, 14), "ui_panel", (64, 64))


def frame():
    save(rounded(64, 64, 14, fill=(0, 0, 0, 0), outline=(255, 255, 255, 255), width=3), "ui_frame", (64, 64))


def keycap():
    # A key: a light face over a darker lip, so it reads as a physical key when tinted.
    w, h = 64, 64
    img = Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 6 * K, w * K - 1, h * K - 1), radius=12 * K, fill=(150, 150, 150, 255))
    d.rounded_rectangle((0, 0, w * K - 1, (h - 7) * K), radius=12 * K, fill=(255, 255, 255, 255))
    save(img, "ui_keycap", (w, h))


def circle():
    img = Image.new("RGBA", (128 * K, 128 * K), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse((0, 0, 128 * K - 1, 128 * K - 1), fill=(255, 255, 255, 255))
    save(img, "ui_circle", (128, 128))


def ring():
    img = Image.new("RGBA", (128 * K, 128 * K), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse((4 * K, 4 * K, 124 * K, 124 * K), outline=(255, 255, 255, 255), width=8 * K)
    save(img, "ui_ring", (128, 128))


def glow():
    n = 128
    img = Image.new("RGBA", (n, n), (255, 255, 255, 0))
    px = img.load()
    for y in range(n):
        for x in range(n):
            dx, dy = (x - n / 2 + 0.5) / (n / 2), (y - n / 2 + 0.5) / (n / 2)
            r = (dx * dx + dy * dy) ** 0.5
            a = max(0.0, 1.0 - r) ** 2
            px[x, y] = (255, 255, 255, int(255 * a))
    img.save(os.path.join(DST, "ui_glow.png"))


def diamond():
    img = Image.new("RGBA", (128 * K, 128 * K), (0, 0, 0, 0))
    c = 64 * K
    ImageDraw.Draw(img).polygon([(c, 4 * K), (124 * K, c), (c, 124 * K), (4 * K, c)], fill=(255, 255, 255, 255))
    save(img, "ui_diamond", (128, 128))


def shadow():
    # Soft drop shadow behind cards (nine-sliced).
    n = 96
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    ImageDraw.Draw(img).rounded_rectangle((20, 20, n - 21, n - 21), radius=14, fill=(0, 0, 0, 200))
    img = img.filter(ImageFilter.GaussianBlur(9))
    img.save(os.path.join(DST, "ui_shadow.png"))


if __name__ == "__main__":
    os.makedirs(DST, exist_ok=True)
    for f in (panel, frame, keycap, circle, ring, glow, diamond, shadow):
        f()
    print("UI kit written to", DST)
