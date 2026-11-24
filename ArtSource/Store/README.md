# コンビニの絵（2026/11/24）

`codex/` の 24 枚は Codex が描いた物（`D:\TK230088\Leica\brain\tmp_roomies_store_art_20261124` から写した。依頼の内容は `codex/REQUEST.md`、
作り方は `codex/GENERATION.md`）。実在の店・メーカー・商品の名前・ロゴは入っていない。

## アトラスに入れるまで

1. プロジェクト直下で `python ArtSource/Store/make_store_art.py`：`codex/` の絵を、アトラスに入れる大きさに縮めて `atlas/` に書く
   （高品質の縮小。透明のある絵は、色に透明度を掛けてから縮め、透明な所をその絵の下に来る色で埋める）。
2. Unity で `Roomies/Store/1 Build Building` → `2 Build Gimmick Prefabs` → `3 Place In Open Scene`：
   カジノのアトラス（`Assets/_Art/Casino/casino_atlas.png`）の空いている所（左下と右下）へ入れて、形に貼る。
   場所と大きさは `RoomiesCasinoArt.cs` の `Images` の表（`Img.Store〇〇`）。縮める大きさ（`make_store_art.py` の `SIZES`）と同じにする。

マテリアルは増やしていない（カジノの `casino_atlas` をそのまま使う）。アトラスの取り込みは、最大 2048・ミップマップ・トライリニア・異方性 16・
ミップの切り替えを遅らせる（-0.5）・高品質の圧縮。

## どこに貼っているか（`RoomiesCasinoArt.Store.cs`）

| 絵 | 貼る所 |
|---|---|
| `store_logo` | 看板と横の帯の店の印（`StoreEmblem`）、レジ袋の印 |
| `label_drink_1`〜`6` | ボトル・缶のまわりに 1 周巻く（`lp.Wrap`） |
| `label_snack_1`〜`4` | お菓子の袋の表、レジの裏の棚の小箱 |
| `label_cup_1`〜`3` | カップめんの側面に巻く |
| `label_bread_1`〜`2` | パンの袋の上 |
| `label_onigiri` | おにぎりの前のシール |
| `lid_bento_1`〜`2` | お弁当のふた（上から見た絵） |
| `label_milk` | 牛乳パックの前と後ろ |
| `poster_1`〜`2` | ガラスの内側（外向き）と、売り場の仕切り。近くで見てもくっきりするよう、アトラスではなく専用の 1 枚（`atlas/store_posters.png` → `Assets/_Art/Store/store_posters.png`、1024×1024 に 512×683 を 2 枚。マテリアル `Assets/_Materials/Store/store_posters.mat`） |
| `nobori_1`〜`2` | 店先ののぼり（両面） |

絵を差し替えるときは、同じ名前で `codex/` に置いて、上の 1・2 を実行し直す。
