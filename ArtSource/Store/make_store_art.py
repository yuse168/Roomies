"""コンビニの絵（Codex 製、24 枚）を、アトラスに入れる大きさに縮める（2026/11/24）。

使い方（プロジェクトの直下で）:
    python ArtSource/Store/make_store_art.py

・元の絵は ArtSource/Store/codex/（REQUEST.md の名前）。縮めた絵を ArtSource/Store/atlas/ に書く。
・縮めた絵は、Unity の Roomies/Art/... の作り直し（RoomiesCasinoArt の Images の表）がカジノのアトラス（Assets/_Art/Casino/casino_atlas.png）の
  空いている所へ入れる。大きさと場所は RoomiesCasinoArt.cs の表と同じにする（下の SIZES）。
・透明のある絵は、透明な所を、その絵の下に来る色で埋める（アトラスのマテリアルは透けないので、透明のまま入れると黒や白のふちが出る）。
  色に透明度を掛けてから縮めて、ふちの黒ずみを防ぐ。
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "codex")
OUT = os.path.join(HERE, "atlas")

# 名前 → (幅, 高さ, 透明な所を埋める色)
SIZES = {
    "store_logo": (224, 224, (168, 67, 47)),
    "label_onigiri": (80, 80, (246, 242, 232)),
    "lid_bento_1": (160, 120, (42, 34, 48)),
    "lid_bento_2": (160, 120, (42, 34, 48)),
    "label_milk": (64, 128, None),
    "poster_1": (210, 280, None),
    "poster_2": (210, 280, None),
    "nobori_1": (70, 280, None),
    "nobori_2": (70, 280, None),
}
for i in range(1, 7):
    SIZES[f"label_drink_{i}"] = (128, 64, None)
for i in range(1, 5):
    SIZES[f"label_snack_{i}"] = (128, 160, None)
for i in range(1, 4):
    SIZES[f"label_cup_{i}"] = (128, 64, None)
for i in range(1, 3):
    SIZES[f"label_bread_{i}"] = (128, 80, None)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, (w, h, fill) in SIZES.items():
        p = os.path.join(SRC, name + ".png")
        if not os.path.exists(p):
            print("missing", name)
            continue
        img = Image.open(p).convert("RGBA")
        # 色に透明度を掛けてから縮める（透明な所の色が、ふちへにじまないように）
        small = img.convert("RGBa").resize((w, h), Image.LANCZOS).convert("RGBA")
        if fill is not None:
            flat = Image.new("RGBA", (w, h), fill + (255,))
            flat.alpha_composite(small)
            small = flat
        small.convert("RGB").save(os.path.join(OUT, name + ".png"))
        print(name, small.size)


POSTER_SHEET = os.path.join(OUT, "store_posters.png")
POSTER_SIZE = (512, 683)       # 1 枚の大きさ（元の 768×1024 の 2/3）。1024×1024 の 1 枚に、左右に 2 枚並べる（上は余白）


def make_poster_sheet():
    """ポスター 2 枚だけの 1 枚（近くで見てもくっきりするように、アトラスとは別にする）。Unity の側は Assets/_Art/Store/store_posters.png に写す。"""
    sheet = Image.new("RGB", (1024, 1024), (244, 236, 218))
    for i in range(2):
        p = os.path.join(SRC, f"poster_{i + 1}.png")
        if not os.path.exists(p):
            print("missing", p)
            continue
        img = Image.open(p).convert("RGB").resize(POSTER_SIZE, Image.LANCZOS)
        sheet.paste(img, (i * 512, 1024 - POSTER_SIZE[1]))
    sheet.save(POSTER_SHEET)
    print("store_posters", sheet.size)


if __name__ == "__main__":
    main()
    make_poster_sheet()
