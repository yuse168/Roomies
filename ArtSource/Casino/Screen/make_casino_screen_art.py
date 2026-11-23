"""スロットの画面の絵と、トランプ・チップの絵を作る。

使い方（プロジェクトの直下で）:
    python ArtSource/Casino/Screen/make_casino_screen_art.py

・ArtSource/Casino/Screen/codex/ に同じ名前の絵（Codex 製。名前は README.md の表）があれば、それを使う。
  無い物は、このファイルのコードで描いた絵になる（数字札・スート・文字のロゴは、いつもコードで描く）。
・出力：Assets/_Art/Casino/Slot/*.png（画面の部品。Unity ではスプライト）と Assets/_Art/Casino/casino_cards.png（トランプ 52 枚＋裏面）。
・何度実行しても同じ絵になる。絵を替えたら、Unity で Roomies/Casino/2 Build Gimmick Prefabs を実行し直す。
"""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
CODEX = os.path.join(HERE, "codex")
OUT_SLOT = os.path.join(ROOT, "Assets", "_Art", "Casino", "Slot")
OUT_CARDS = os.path.join(ROOT, "Assets", "_Art", "Casino", "casino_cards.png")
FONT_LOGO = os.path.join(ROOT, "Assets", "TextMesh Pro", "Examples & Extras", "Fonts", "Bangers.ttf")
FONT_BOLD = os.path.join(ROOT, "Assets", "TextMesh Pro", "Examples & Extras", "Fonts", "Anton.ttf")
FONT_CARD = os.path.join(ROOT, "Assets", "TextMesh Pro", "Examples & Extras", "Fonts", "Roboto-Bold.ttf")

INK = (34, 26, 44)
GOLD, GOLD_D, GOLD_L = (217, 164, 65), (154, 107, 34), (255, 224, 150)
RED, RED_D = (217, 54, 62), (124, 31, 43)
PINK = (247, 101, 186)
CARD_RED, CARD_BLACK = (200, 40, 62), (30, 28, 40)
random.seed(20261124)


def codex(name):
    p = os.path.join(CODEX, name)
    return Image.open(p).convert("RGBA") if os.path.exists(p) else None


def save(img, name):
    os.makedirs(OUT_SLOT, exist_ok=True)
    img.save(os.path.join(OUT_SLOT, name))
    print(name, img.size)


def fit(img, size):
    """絵を、縦横比を保って size の正方形（または矩形）の真ん中に収める。"""
    w, h = size if isinstance(size, tuple) else (size, size)
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    k = min(w / img.width, h / img.height)
    r = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
    out.alpha_composite(r, ((w - r.width) // 2, (h - r.height) // 2))
    return out


def outlined(draw_fn, size=256, outline=10, ss=3):
    """draw_fn(draw, S) で描いた形に、墨の太い縁を付ける（ss 倍で描いて縮める）。"""
    S = size * ss
    body = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(body), S)
    alpha = body.split()[3]
    grown = alpha.filter(ImageFilter.MaxFilter(outline * ss // 2 * 2 + 1))
    edge = Image.new("RGBA", (S, S), INK + (255,))
    edge.putalpha(grown)
    edge.alpha_composite(body)
    return edge.resize((size, size), Image.LANCZOS)


def text_image(text, font_path, px, fill, stroke_fill, stroke, pad=24, gradient=None):
    font = ImageFont.truetype(font_path, px)
    tmp = ImageDraw.Draw(Image.new("RGBA", (8, 8)))
    box = tmp.textbbox((0, 0), text, font=font, stroke_width=stroke)
    w, h = box[2] - box[0] + pad * 2, box[3] - box[1] + pad * 2
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.text((pad - box[0], pad - box[1]), text, font=font, fill=stroke_fill, stroke_width=stroke, stroke_fill=stroke_fill)
    face = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(face).text((pad - box[0], pad - box[1]), text, font=font, fill=fill)
    if gradient:
        g = Image.new("RGBA", (w, h))
        gd = ImageDraw.Draw(g)
        for y in range(h):
            k = y / max(1, h - 1)
            gd.line([(0, y), (w, y)], fill=tuple(round(gradient[0][i] + (gradient[1][i] - gradient[0][i]) * k) for i in range(3)) + (255,))
        g.putalpha(face.split()[3])
        face = g
    img.alpha_composite(face)
    return img


# ---------------------------------------------------------------- スロットのシンボル
def sym_cherry(d, S):
    k = S / 256
    d.line([(150 * k, 40 * k), (96 * k, 150 * k)], fill=(63, 122, 62), width=round(12 * k))
    d.line([(150 * k, 40 * k), (176 * k, 140 * k)], fill=(63, 122, 62), width=round(12 * k))
    d.polygon([(150 * k, 36 * k), (214 * k, 30 * k), (190 * k, 78 * k)], fill=(86, 160, 78))
    for cx, cy in ((92, 172), (176, 164)):
        d.ellipse([(cx - 52) * k, (cy - 52) * k, (cx + 52) * k, (cy + 52) * k], fill=RED)
        d.ellipse([(cx - 52) * k, (cy - 8) * k, (cx + 52) * k, (cy + 52) * k], fill=RED_D)
        d.ellipse([(cx - 50) * k, (cy - 50) * k, (cx + 50) * k, (cy + 30) * k], fill=RED)
        d.ellipse([(cx - 32) * k, (cy - 36) * k, (cx - 8) * k, (cy - 14) * k], fill=(255, 190, 190))


def sym_bell(d, S):
    k = S / 256
    d.ellipse([108 * k, 22 * k, 148 * k, 62 * k], fill=GOLD_D)
    d.polygon([(128 * k, 40 * k), (186 * k, 84 * k), (196 * k, 170 * k), (222 * k, 196 * k), (34 * k, 196 * k), (60 * k, 170 * k), (70 * k, 84 * k)], fill=GOLD)
    d.polygon([(128 * k, 40 * k), (186 * k, 84 * k), (196 * k, 170 * k), (222 * k, 196 * k), (128 * k, 196 * k)], fill=GOLD_D)
    d.polygon([(128 * k, 52 * k), (176 * k, 88 * k), (184 * k, 166 * k), (128 * k, 166 * k)], fill=GOLD)
    d.polygon([(92 * k, 84 * k), (112 * k, 70 * k), (104 * k, 150 * k), (84 * k, 156 * k)], fill=GOLD_L)
    d.rectangle([30 * k, 188 * k, 226 * k, 206 * k], fill=GOLD_L)
    d.ellipse([104 * k, 200 * k, 152 * k, 240 * k], fill=RED)


def sym_bar(d, S):
    k = S / 256
    d.rounded_rectangle([18 * k, 70 * k, 238 * k, 186 * k], radius=22 * k, fill=GOLD_D)
    d.rounded_rectangle([18 * k, 62 * k, 238 * k, 174 * k], radius=22 * k, fill=GOLD)
    d.rounded_rectangle([34 * k, 76 * k, 222 * k, 160 * k], radius=12 * k, fill=INK)
    font = ImageFont.truetype(FONT_BOLD, round(74 * k))
    box = d.textbbox((0, 0), "BAR", font=font)
    d.text((128 * k - (box[2] + box[0]) / 2, 118 * k - (box[3] + box[1]) / 2), "BAR", font=font, fill=GOLD_L)


def sym_crown(d, S):
    k = S / 256
    d.polygon([(30 * k, 190 * k), (22 * k, 78 * k), (78 * k, 128 * k), (128 * k, 44 * k), (178 * k, 128 * k), (234 * k, 78 * k), (226 * k, 190 * k)], fill=GOLD)
    d.polygon([(128 * k, 44 * k), (178 * k, 128 * k), (234 * k, 78 * k), (226 * k, 190 * k), (128 * k, 190 * k)], fill=GOLD_D)
    d.polygon([(128 * k, 62 * k), (170 * k, 138 * k), (222 * k, 98 * k), (216 * k, 180 * k), (128 * k, 180 * k)], fill=GOLD)
    d.rectangle([28 * k, 184 * k, 228 * k, 222 * k], fill=GOLD_L)
    d.rectangle([28 * k, 206 * k, 228 * k, 222 * k], fill=GOLD_D)
    for cx, c in ((70, RED), (128, (126, 105, 190)), (186, RED)):
        d.ellipse([(cx - 13) * k, 190 * k, (cx + 13) * k, 216 * k], fill=c)
    for cx, cy in ((22, 78), (128, 44), (234, 78)):
        d.ellipse([(cx - 15) * k, (cy - 15) * k, (cx + 15) * k, (cy + 15) * k], fill=PINK)


def sym_seven(d, S):
    k = S / 256
    pts = [(46, 34), (214, 34), (214, 76), (136, 226), (78, 226), (150, 86), (46, 86)]
    d.polygon([(x * k, (y + 10) * k) for x, y in pts], fill=RED_D)
    d.polygon([(x * k, y * k) for x, y in pts], fill=RED)
    d.polygon([(x * k, y * k) for x, y in [(56, 44), (204, 44), (204, 56), (56, 56)]], fill=(255, 150, 150))
    d.polygon([(x * k, y * k) for x, y in [(160, 96), (176, 96), (112, 216), (96, 216)]], fill=(255, 120, 120))


def make_symbols():
    for name, fn, glow in (("cherry", sym_cherry, False), ("bell", sym_bell, False), ("bar", sym_bar, False), ("crown", sym_crown, False), ("seven", sym_seven, True)):
        src = codex(f"slot_sym_{name}.png")
        img = fit(src, 256) if src else outlined(fn)
        save(img, f"sym_{name}.png")
        # 回っている間に使う、縦にぶれた絵
        blur = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        for i in range(-6, 7):
            layer = ImageChops.offset(img, 0, i * 7)
            layer.putalpha(layer.split()[3].point(lambda a: a // 9))
            blur.alpha_composite(layer)
        save(blur.filter(ImageFilter.GaussianBlur(1.2)), f"sym_{name}_blur.png")


# ---------------------------------------------------------------- 背景
def gradient(size, top, bottom):
    w, h = size
    img = Image.new("RGBA", size)
    d = ImageDraw.Draw(img)
    for y in range(h):
        k = y / (h - 1)
        d.line([(0, y), (w, y)], fill=tuple(round(top[i] + (bottom[i] - top[i]) * k) for i in range(3)) + (255,))
    return img


def make_backgrounds():
    size = (384, 560)
    src = codex("slot_bg_normal.png")
    if src:
        bg = src.resize(size, Image.LANCZOS)
    else:
        bg = gradient(size, (58, 36, 88), (94, 28, 42))
        d = ImageDraw.Draw(bg, "RGBA")
        for row in range(-1, 16):
            for col in range(-1, 9):
                cx, cy = col * 48 + (24 if row % 2 else 0), row * 40
                d.polygon([(cx, cy - 14), (cx + 12, cy), (cx, cy + 14), (cx - 12, cy)], fill=(255, 255, 255, 14))
        d.rectangle([0, 0, size[0], 6], fill=GOLD + (255,))
        d.rectangle([0, size[1] - 6, size[0], size[1]], fill=GOLD + (255,))
    save(bg, "bg_normal.png")

    src = codex("slot_bg_fever.png")
    if src:
        bg = src.resize(size, Image.LANCZOS)
    else:
        bg = Image.new("RGBA", size, (120, 20, 70, 255))
        d = ImageDraw.Draw(bg, "RGBA")
        cx, cy, n = size[0] / 2, size[1] * 0.47, 28
        for i in range(n):
            a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 0.5) / n
            d.polygon([(cx, cy), (cx + math.cos(a0) * 900, cy + math.sin(a0) * 900), (cx + math.cos(a1) * 900, cy + math.sin(a1) * 900)],
                      fill=(255, 200, 80, 255) if i % 2 == 0 else (247, 101, 186, 255))
        glow = Image.new("RGBA", size, (0, 0, 0, 0))
        ImageDraw.Draw(glow).ellipse([cx - 170, cy - 170, cx + 170, cy + 170], fill=(255, 240, 190, 210))
        bg.alpha_composite(glow.filter(ImageFilter.GaussianBlur(60)))
        d = ImageDraw.Draw(bg, "RGBA")
        d.rectangle([0, 0, size[0], 6], fill=GOLD_L + (255,))
        d.rectangle([0, size[1] - 6, size[0], size[1]], fill=GOLD_L + (255,))
    save(bg, "bg_fever.png")


# ---------------------------------------------------------------- 光・コイン・きらめき
def make_effects():
    src = codex("slot_fx_sunburst.png")
    if src:
        sun = fit(src, 512)
    else:
        S = 1024
        sun = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(sun)
        n = 16
        for i in range(n):
            a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 0.42) / n
            d.polygon([(S / 2, S / 2), (S / 2 + math.cos(a0) * S, S / 2 + math.sin(a0) * S), (S / 2 + math.cos(a1) * S, S / 2 + math.sin(a1) * S)], fill=255)
        fall = Image.new("L", (S, S), 0)
        fd = ImageDraw.Draw(fall)
        for r in range(S // 2, 0, -4):
            fd.ellipse([S / 2 - r, S / 2 - r, S / 2 + r, S / 2 + r], fill=round(255 * (1 - r / (S / 2)) ** 0.8))
        sun = ImageChops.multiply(sun, fall).resize((512, 512), Image.LANCZOS)
        white = Image.new("RGBA", (512, 512), (255, 255, 255, 255))
        white.putalpha(sun)
        sun = white
    save(sun, "fx_sunburst.png")

    src = codex("slot_fx_coin.png")
    if src:
        coin = fit(src, 128)
    else:
        def draw(d, S):
            d.ellipse([S * .06, S * .06, S * .94, S * .94], fill=GOLD_D)
            d.ellipse([S * .1, S * .08, S * .9, S * .88], fill=GOLD)
            d.ellipse([S * .22, S * .2, S * .78, S * .76], fill=GOLD_L)
            d.ellipse([S * .26, S * .24, S * .74, S * .72], fill=GOLD)
            d.polygon([(S * .34, S * .62), (S * .32, S * .4), (S * .43, S * .5), (S * .5, S * .34), (S * .57, S * .5), (S * .68, S * .4), (S * .66, S * .62)], fill=GOLD_D)
        coin = outlined(draw, 128, 6)
    save(coin, "fx_coin.png")

    src = codex("slot_fx_sparkle.png")
    if src:
        sp = fit(src, 128)
    else:
        S = 512
        m = Image.new("L", (S, S), 0)
        d = ImageDraw.Draw(m)
        c = S / 2
        d.polygon([(c, 10), (c + 26, c - 26), (S - 10, c), (c + 26, c + 26), (c, S - 10), (c - 26, c + 26), (10, c), (c - 26, c - 26)], fill=255)
        m = ImageChops.lighter(m, m.filter(ImageFilter.GaussianBlur(18))).resize((128, 128), Image.LANCZOS)
        sp = Image.new("RGBA", (128, 128), (255, 255, 255, 255))
        sp.putalpha(m)
    save(sp, "fx_sparkle.png")

    # やわらかい丸い光（ランプ・フラッシュ・リーチの縁）
    S = 256
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    for r in range(S // 2, 0, -2):
        d.ellipse([S / 2 - r, S / 2 - r, S / 2 + r, S / 2 + r], fill=round(255 * (1 - r / (S / 2)) ** 1.6))
    glow = Image.new("RGBA", (S, S), (255, 255, 255, 255))
    glow.putalpha(m)
    save(glow, "fx_glow.png")

    # 斜めに走る光の帯
    w, h = 256, 512
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    for x in range(w):
        k = 1 - abs(x - w / 2) / (w / 2)
        d.line([(x, 0), (x, h)], fill=round(255 * k ** 2.2))
    sweep = Image.new("RGBA", (w, h), (255, 255, 255, 255))
    sweep.putalpha(m)
    save(sweep, "fx_sweep.png")

    # 白い四角（色を付けて使う板）
    save(Image.new("RGBA", (8, 8), (255, 255, 255, 255)), "fx_white.png")


def make_logos():
    gold = (GOLD_L, (240, 150, 40))
    pink = ((255, 200, 235), (240, 70, 160))
    red = ((255, 190, 150), (225, 40, 50))
    for name, text, grad, px in (("logo_bigwin", "BIG WIN", gold, 190), ("logo_jackpot", "JACKPOT", red, 170), ("logo_fever", "FEVER", pink, 190),
                                 ("logo_reach", "REACH!", red, 170), ("logo_win", "WIN", gold, 170), ("logo_title", "LUCKY ROOM", gold, 110)):
        save(text_image(text, FONT_LOGO, px, (255, 255, 255, 255), INK + (255,), 14, 30, grad), name + ".png")


# ---------------------------------------------------------------- トランプ（52 枚＋裏面を 1 枚の絵に）
# トランプの絵：4096×2048 に、幅 372・高さ 409 のマスを 11 列 × 5 段（左上から右へ ♠A〜K、♥A〜K、♦A〜K、♣A〜K、53 番目が裏面）。
# 前は 2048×1024 に 146×256 のマスで、遊ぶ人の目の高さから数字がぼやけていた。マスを横 2.5 倍・縦 1.6 倍にした
ATLAS_W, ATLAS_H = 4096, 2048
CELL_W, CELL_H, COLS, ROWS = 372, 409, 11, 5
K = 2                                               # 描く大きさの倍率（下の数字は、360×504 のときの値）
CW, CH = 360 * K, 504 * K                           # 1 枚を描く大きさ（縦横比 5:7）。あとでマスの大きさに縮める
PAPER = (247, 243, 232, 255)
CARD_RADIUS = .07                                   # 角の丸みの半径（横幅に対する割合）。板の形（RoomiesCasinoArt.Cards.cs の CardCorner）と同じ値にする


def k(v):
    return int(round(v * K))


def suit_shape(suit, size, color):
    """スートの形（0=スペード 1=ハート 2=ダイヤ 3=クラブ）。"""
    S = size * 4
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = color + (255,)
    if suit == 1:
        d.ellipse([S * .06, S * .1, S * .54, S * .58], fill=c)
        d.ellipse([S * .46, S * .1, S * .94, S * .58], fill=c)
        d.polygon([(S * .08, S * .42), (S * .92, S * .42), (S * .5, S * .95)], fill=c)
    elif suit == 2:
        d.polygon([(S * .5, S * .03), (S * .9, S * .5), (S * .5, S * .97), (S * .1, S * .5)], fill=c)
    elif suit == 0:
        d.ellipse([S * .06, S * .36, S * .54, S * .84], fill=c)
        d.ellipse([S * .46, S * .36, S * .94, S * .84], fill=c)
        d.polygon([(S * .08, S * .54), (S * .92, S * .54), (S * .5, S * .03)], fill=c)
        d.polygon([(S * .5, S * .6), (S * .66, S * .97), (S * .34, S * .97)], fill=c)
    else:
        for cx, cy in ((.5, .27), (.27, .58), (.73, .58)):
            d.ellipse([S * (cx - .23), S * (cy - .23), S * (cx + .23), S * (cy + .23)], fill=c)
        d.polygon([(S * .5, S * .5), (S * .66, S * .97), (S * .34, S * .97)], fill=c)
    return img.resize((size, size), Image.LANCZOS)


PIPS = {
    1: [(.5, .5)],
    2: [(.5, .2), (.5, .8)],
    3: [(.5, .2), (.5, .5), (.5, .8)],
    4: [(.3, .2), (.7, .2), (.3, .8), (.7, .8)],
    5: [(.3, .2), (.7, .2), (.5, .5), (.3, .8), (.7, .8)],
    6: [(.3, .2), (.7, .2), (.3, .5), (.7, .5), (.3, .8), (.7, .8)],
    7: [(.3, .2), (.7, .2), (.5, .35), (.3, .5), (.7, .5), (.3, .8), (.7, .8)],
    8: [(.3, .2), (.7, .2), (.5, .35), (.3, .5), (.7, .5), (.5, .65), (.3, .8), (.7, .8)],
    9: [(.3, .2), (.7, .2), (.3, .4), (.7, .4), (.5, .5), (.3, .6), (.7, .6), (.3, .8), (.7, .8)],
    10: [(.3, .2), (.7, .2), (.5, .3), (.3, .4), (.7, .4), (.3, .6), (.7, .6), (.5, .7), (.3, .8), (.7, .8)],
}


def card_paper():
    """札の紙。角は板の形のほうで丸く切るので、絵はマスいっぱいに紙の色を敷き、ふちの線だけ角丸で描く。"""
    img = Image.new("RGBA", (CW, CH), PAPER)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([k(2), k(2), CW - 1 - k(2), CH - 1 - k(2)], radius=round(CW * CARD_RADIUS) - k(2), outline=(196, 186, 170, 255), width=k(3))
    return img, d


def card_face(rank, suit):
    color = CARD_RED if suit in (1, 2) else CARD_BLACK
    img, d = card_paper()
    label = {1: "A", 11: "J", 12: "Q", 13: "K"}.get(rank, str(rank))
    # 角の数字とスート：遊ぶ人の目の高さから読めるよう、大きく太く
    font = ImageFont.truetype(FONT_CARD, k(86 if rank != 10 else 70))
    corner = Image.new("RGBA", (k(100), k(172)), (0, 0, 0, 0))
    cd = ImageDraw.Draw(corner)
    box = cd.textbbox((0, 0), label, font=font)
    cd.text((k(50) - (box[2] + box[0]) / 2, k(4) - box[1]), label, font=font, fill=color + (255,), stroke_width=k(1), stroke_fill=color + (255,))
    corner.alpha_composite(suit_shape(suit, k(56), color), (k(22), k(104)))
    img.alpha_composite(corner, (k(12), k(14)))
    img.alpha_composite(corner.rotate(180), (CW - k(112), CH - k(186)))

    if rank >= 11:
        d.rounded_rectangle([k(116), k(96), CW - k(116), CH - k(96)], radius=k(14), outline=color + (255,), width=k(5), fill=(255, 250, 236, 255))
        court = codex({11: "card_court_j.png", 12: "card_court_q.png", 13: "card_court_k.png"}[rank])
        if court:
            art = fit(court, (CW - k(248), CH - k(208)))
            tint = Image.new("RGBA", art.size, color + (255,))
            tint.putalpha(art.split()[3])
            img.alpha_composite(tint, (k(124), k(104)))
        else:
            big = ImageFont.truetype(FONT_BOLD, k(176))
            box = d.textbbox((0, 0), label, font=big)
            d.text((CW / 2 - (box[2] + box[0]) / 2, CH / 2 - (box[3] + box[1]) / 2 - k(16)), label, font=big, fill=color + (255,))
            img.alpha_composite(suit_shape(suit, k(58), color), (CW // 2 - k(29), CH - k(180)))
    elif rank == 1:
        img.alpha_composite(suit_shape(suit, k(190), color), (CW // 2 - k(95), CH // 2 - k(95)))
    else:
        size = k(58)
        for x, y in PIPS[rank]:
            pip = suit_shape(suit, size, color)
            if y > .5:
                pip = pip.rotate(180)
            px = CW / 2 + (x - .5) * k(230)       # 3 列（.3・.5・.7）を、角の数字の内側に並べる
            py = k(100) + (CH - k(200)) * (y - .2) / .6
            img.alpha_composite(pip, (round(px - size / 2), round(py - size / 2)))
    return img


def card_back():
    src = codex("card_back.png")
    if src:
        # 届いた絵は角が透明（丸く切ってある）。角は板の形で切るので、透明な所は絵のふちの色で埋める（にじみ防止）
        src = src.resize((CW, CH), Image.LANCZOS)
        edge = src.getpixel((CW // 2, k(6)))
        flat = Image.new("RGBA", (CW, CH), edge[:3] + (255,))
        flat.alpha_composite(src)
        return flat
    img, d = card_paper()
    d.rounded_rectangle([k(20), k(20), CW - 1 - k(20), CH - 1 - k(20)], radius=k(18), fill=(138, 43, 60, 255))
    pattern = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    pd = ImageDraw.Draw(pattern)
    for row in range(-1, 14):
        for col in range(-1, 9):
            cx, cy = k(22 + col * 44 + (22 if row % 2 else 0)), k(22 + row * 38)
            pd.polygon([(cx, cy - k(16)), (cx + k(14), cy), (cx, cy + k(16)), (cx - k(14), cy)], outline=GOLD + (255,), width=k(3))
    mask = Image.new("L", (CW, CH), 0)
    ImageDraw.Draw(mask).rounded_rectangle([k(30), k(30), CW - 1 - k(30), CH - 1 - k(30)], radius=k(14), fill=255)
    pattern.putalpha(ImageChops.multiply(pattern.split()[3], mask))
    img.alpha_composite(pattern)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([k(30), k(30), CW - 1 - k(30), CH - 1 - k(30)], radius=k(14), outline=GOLD + (255,), width=k(4))
    d.ellipse([CW / 2 - k(56), CH / 2 - k(56), CW / 2 + k(56), CH / 2 + k(56)], fill=(94, 28, 42, 255), outline=GOLD + (255,), width=k(5))
    img.alpha_composite(suit_shape(0, k(64), GOLD), (CW // 2 - k(32), CH // 2 - k(32)))
    return img


def make_cards():
    atlas = Image.new("RGBA", (ATLAS_W, ATLAS_H), PAPER)

    def put(index, card):
        cell = card.resize((CELL_W, CELL_H), Image.LANCZOS)
        flat = Image.new("RGBA", cell.size, PAPER)
        flat.alpha_composite(cell)
        atlas.paste(flat, ((index % COLS) * CELL_W, (index // COLS) * CELL_H))

    for suit in range(4):
        for rank in range(1, 14):
            put(suit * 13 + rank - 1, card_face(rank, suit))
    put(52, card_back())
    atlas.convert("RGB").save(OUT_CARDS)
    print("casino_cards.png", atlas.size)


if __name__ == "__main__":
    make_symbols()
    make_backgrounds()
    make_effects()
    make_logos()
    make_cards()
