# 部屋の小物の元の絵（2026/11/23）

Unity には取り込まない元の画像。`Roomies/Art/Restyle/Build Room`（タイトルは `Build Title Room`）を実行すると、
`RoomiesRoomRestyleBuilder.PropsAtlas.cs` の表のとおりに切り出して、`Assets/_Art/Restyle/Props/props_atlas.png`（2048×2048）に組む。

| ファイル | 使う所 | 切り出し |
|---|---|---|
| can_labels_atlas.png | 缶のラベル 6 種（周り 1 周） | 2 列×3 段、各 2.75:1 |
| bottle_labels_atlas.png | 瓶のラベル 3 種（周り 1 周） | 上から 3 本、各 5.5:1 |
| magazine_covers_atlas.png | 雑誌の表紙 4 種 | 2 列×2 段 |
| fridge_notes_atlas.png | 冷蔵庫の扉のメモ・机の付せん 6 種（アルファ） | 3 列×2 段 |
| paper_scraps_atlas.png | 床の紙くず 4 種（アルファ。いちばん大きいかたまりだけ残す） | 2 列×2 段 |
| pizza_toppings_atlas.png | ピザの上面に重ねるペパロニ・ピーマン・チーズ | 段ごとに 4 つ |
| plant_leaves_atlas.png | 観葉植物の葉（付け根を下にそろえて 3 種） | 付け根と先の位置で回して切り出す |

絵を描き直すときは、同じ名前・同じ並びで上書きして Build Room を実行し直す。並びを変えたら `PropsAtlas.cs` の `Src*` の表（左上が原点の px）を直す。
元の説明は README_codex.md。床・壁・布・段ボール・ラグのタイルは `Assets/_Art/Restyle/Textures/Tiles/`、ネコのネオンは `Assets/_Art/Restyle/Textures/Neon/`（どちらも Unity に取り込む）。
