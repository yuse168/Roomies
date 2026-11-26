"""家具ショップのカードの絵（Codex、12 種、2026/11/26）を取り込む。

使い方（プロジェクトの直下で）:
    python ArtSource/Room/pc/make_furniture_art.py

・D:\\TK230088\\Leica\\brain\\tmp_roomies_furniture_art_20261126\\ の PNG を ArtSource/Room/pc/codex_furniture/ に控え（元のファイルは消さない）、
  Assets/_Art/UI/Pc/Furniture/pc_furn_<家具の id>.png（256×256、透明あり）に書く。
・どの絵がどの家具かは、ファイル名に家具の id が入っているか、README.md の表（ファイル名と家具の名前）で決める。
・そのあと Unity で Roomies/PC/3 Build Desktop UI を実行すると、カードに絵が出る（絵が無い家具は、色の板と頭の字のまま）。
"""
import os
import re
import shutil
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
INBOX = r"D:\TK230088\Leica\brain\tmp_roomies_furniture_art_20261126"
KEEP = os.path.join(HERE, "codex_furniture")
OUT = os.path.join(ROOT, "Assets", "_Art", "UI", "Pc", "Furniture")

# FurnitureCatalog.cs の並び（id → 名前）
FURNITURE = {
    "coffee_maker": "コーヒーメーカー", "energy_drink_fridge": "エナドリ冷蔵庫", "comfy_bed": "ふかふかベッド", "welcome_mat": "げんかんマット",
    "piggy_bank": "ちょきんばこ", "wall_clock": "かべ掛け時計", "delivery_terminal": "配達端末", "lucky_cat": "まねきねこ",
    "vending_machine": "じはんき", "disco_ball": "ミラーボール", "trampoline": "トランポリン", "arcade_machine": "アーケード筐体",
}


def readme_map():
    """README.md の表から、ファイル名 → id を作る（行の中に .png の名前と、家具の名前か id があれば対応とみなす）。"""
    path = os.path.join(INBOX, "README.md")
    result = {}
    if not os.path.exists(path):
        return result
    for line in open(path, encoding="utf-8"):
        files = re.findall(r"[\w\-]+\.png", line)
        if not files:
            continue
        for fid, name in FURNITURE.items():
            if name in line or re.search(r"\b" + re.escape(fid) + r"\b", line):
                result[files[0]] = fid
                break
    return result


def main():
    if not os.path.isdir(INBOX):
        print("not arrived:", INBOX)
        return
    os.makedirs(KEEP, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    by_readme = readme_map()
    done = {}
    for name in sorted(os.listdir(INBOX)):
        if not name.lower().endswith(".png") or name.lower().startswith("sheet"):
            continue
        stem = os.path.splitext(name)[0].lower()
        fid = by_readme.get(name) or next((f for f in FURNITURE if f == stem or f in stem), None)
        if fid is None:
            print("unknown:", name)
            continue
        shutil.copy2(os.path.join(INBOX, name), os.path.join(KEEP, name))
        img = Image.open(os.path.join(KEEP, name)).convert("RGBA")
        if img.size != (256, 256):
            img = img.resize((256, 256), Image.LANCZOS)
        img.save(os.path.join(OUT, f"pc_furn_{fid}.png"))
        done[fid] = name
        print(f"{fid:20s} <- {name}")
    missing = [f for f in FURNITURE if f not in done]
    print(f"{len(done)} / {len(FURNITURE)}", "missing: " + ", ".join(missing) if missing else "")
    for f in ("README.md", "prompts.json"):
        if os.path.exists(os.path.join(INBOX, f)):
            shutil.copy2(os.path.join(INBOX, f), os.path.join(KEEP, f))


if __name__ == "__main__":
    main()
