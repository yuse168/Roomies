"""部屋の PC の絵（2026/11/25）。

使い方（プロジェクトの直下で）:
    python ArtSource/Room/pc/make_pc_art.py

・Codex の絵が D:\\TK230088\\Leica\\brain\\tmp_roomies_pc_art_20261125\\ にあれば、ArtSource/Room/pc/codex/ に写してから使う。
  ファイル名に入っている言葉で見分ける（wallpaper／furniture・shop／stock／effect／standby・monitor・lock）。
・無い絵は、仮の絵（このスクリプトが描く。文字は M PLUS U）を使う。仮の絵はあとで Codex の絵に差し替わる。
・書き出し先：Assets/_Art/UI/Pc/pc_wallpaper_room/town.png（1920×1080）・pc_icon_furniture/stocks/effects.png（512×512、透明あり）・
  pc_standby.png（1024×640）・pc_boot_mark.png（512×512、透明あり）
  そのあと Unity で Roomies/PC/1 Build Prefab → 3 Build Desktop UI を実行すると貼り直される。
"""
import os
import shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
INBOX = r"D:\TK230088\Leica\brain\tmp_roomies_pc_art_20261125"
CODEX = os.path.join(HERE, "codex")
OUT = os.path.join(ROOT, "Assets", "_Art", "UI", "Pc")
FONT = os.path.join(ROOT, "Assets", "TextMesh Pro", "Resources", "Fonts & Materials", "MPLUSU-Black.ttf")

# 名前 → (幅, 高さ, Codex のファイル名)。サイズは Codex の絵のまま（Unity の取り込みで最大の大きさを決める）
TARGETS = {
    "pc_wallpaper_room": (1920, 1080, "wallpaper_room.png"),   # ホーム画の壁紙（既定）
    "pc_wallpaper_town": (1920, 1080, "wallpaper_town.png"),   # ホーム画の壁紙（「壁紙を変える」で選べる）
    "pc_icon_furniture": (512, 512, "icon_shop.png"),
    "pc_icon_stocks": (512, 512, "icon_stocks.png"),
    "pc_icon_effects": (512, 512, "icon_buffs.png"),
    "pc_standby": (1024, 640, "monitor_home.png"),             # 机のモニターの待ち受け
    "pc_boot_mark": (512, 512, "boot_mark.png"),               # 起動の画面のしるし（ROOMIES OS の字はゲームの側で重ねる）
}

PLUM, PLUM_DARK, INK, CREAM, WARM, PINK, PURPLE = (45, 36, 69), (24, 20, 40), (21, 21, 27), (255, 248, 236), (255, 179, 92), (247, 101, 186), (142, 105, 190)


def font(size):
    return ImageFont.truetype(FONT, size)


def gradient(w, h, top, bottom):
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img


def placeholder_wallpaper(w, h):
    """夜の部屋の窓ごしの町：濃い紫の空、下に暖かい町の明かり、窓の格子。"""
    img = gradient(w, h, (30, 26, 52), (62, 44, 78))
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    g = ImageDraw.Draw(glow)
    for i in range(14):
        x = int(w * (i + 0.5) / 14)
        bh = int(h * (0.12 + 0.18 * ((i * 7) % 5) / 4))
        g.rectangle([x - 46, h - bh, x + 46, h], fill=(26, 22, 40, 255))
        for k in range(4):
            if (i + k) % 3:
                g.rectangle([x - 26, h - bh + 24 + k * 34, x - 6, h - bh + 40 + k * 34], fill=WARM + (200,))
                g.rectangle([x + 8, h - bh + 24 + k * 34, x + 28, h - bh + 40 + k * 34], fill=(255, 224, 161, 160))
    img.paste(glow, (0, 0), glow)
    haze = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(haze).ellipse([w * 0.1, h * 0.72, w * 0.9, h * 1.25], fill=WARM + (40,))
    img.paste(haze.filter(ImageFilter.GaussianBlur(60)), (0, 0), haze.filter(ImageFilter.GaussianBlur(60)))
    return img


def placeholder_standby(w, h):
    """机のモニターの待ち受け：ROOMIES OS の字と、3 つのアプリの丸い印。時計は Unity の文字が上に重なるので、右上はあける。"""
    img = placeholder_wallpaper(w, h).convert("RGBA")
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([40, 40, 470, 150], 34, fill=(21, 21, 27, 170))
    d.text((72, 58), "ROOMIES OS", font=font(64), fill=CREAM)
    for i, (glyph, col) in enumerate([("家", (211, 168, 67)), ("株", (82, 117, 92)), ("効", PURPLE)]):
        x = 60 + i * 140
        d.rounded_rectangle([x, 380, x + 112, 492], 30, fill=col + (255,))
        d.text((x + 56, 436), glyph, font=font(66), fill=CREAM, anchor="mm")
    d.text((60, 520), "E で使う", font=font(34), fill=(255, 248, 236, 200))
    return img.convert("RGB")


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(CODEX, exist_ok=True)
    for name, (w, h, file) in TARGETS.items():
        src = os.path.join(INBOX, file)
        kept = os.path.join(CODEX, file)
        out = os.path.join(OUT, name + ".png")
        if os.path.exists(src):
            shutil.copy2(src, kept)          # 元の絵は消さない。リポジトリの ArtSource に控えを置く
        if os.path.exists(kept):
            img = Image.open(kept)
            img = img.convert("RGBA" if ("icon" in name or "boot" in name) else "RGB")
            if img.size != (w, h):
                img = img.resize((w, h), Image.LANCZOS)
            img.save(out)
            print(name, "<- codex", file, img.size)
        elif name.startswith("pc_wallpaper"):
            placeholder_wallpaper(w, h).save(out); print(name, "placeholder")
        elif name == "pc_standby":
            placeholder_standby(w, h).save(out); print(name, "placeholder")
        else:
            if os.path.exists(out):
                os.remove(out)   # 仮のアイコン・しるしは作らない（Unity の側で、色の板と字で出す）
            print(name, "missing")


if __name__ == "__main__":
    main()
