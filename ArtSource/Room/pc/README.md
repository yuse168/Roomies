# 部屋の PC の絵（2026/11/25）

`codex/` は Codex が描いた 7 枚（元は D:\TK230088\Leica\brain\tmp_roomies_pc_art_20261125。依頼と生成の文は codex/README.md・prompts.json）。
画像の中に文字は無い。アプリ名・時刻・残高・OS 名はゲームの側で重ねる。

| 絵 | 使う所 | Unity の中の名前 |
|---|---|---|
| wallpaper_room.png | ホーム画の壁紙（既定） | Assets/_Art/UI/Pc/pc_wallpaper_room.png |
| wallpaper_town.png | ホーム画の壁紙（「壁紙」のボタン） | pc_wallpaper_town.png |
| icon_shop.png / icon_stocks.png / icon_buffs.png | アプリのアイコン（家具ショップ・株トレード・家具の効果） | pc_icon_furniture / stocks / effects.png |
| monitor_home.png | 机のモニターの待ち受け（RoomComputer の Visual/Screen） | pc_standby.png |
| boot_mark.png | 起動の画面のしるし | pc_boot_mark.png |

差し替えるとき：同じ名前で Leica のフォルダに置く → `python ArtSource/Room/pc/make_pc_art.py` → Unity で `Roomies/PC/1 Build Prefab` → `3 Build Desktop UI`。
