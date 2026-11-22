# Roomies テクスチャ

生成画像を元にしたゲーム用素材。アトラスは上から見た配置です。透過部分を含むため、切り出し時は余白とアルファを確認してください。

ノーマルは色画像をグレースケールの高さとみなし、ガウスぼかし後、周期境界の中心差分で勾配を求めて正規化し、R=X、G=Y（OpenGL）、B=Z に符号化。明るい部分を高く扱います。色の変化に由来する凹凸を含みます。Unity では Normal Map として取り込み、方向を確認してください。タイル画像は対辺を幅96pxでブレンドし、2×2確認画像を同梱。

| ファイル | 用途・配置 | 解像度 | タイル |
|---|---|---|---|
| floor_wood_albedo.png | 木の床 色 | 1024×1024 | あり |
| floor_wood_normal.png | 木の床 ノーマル OpenGL | 1024×1024 | あり |
| floor_wood_albedo_2x2.png | 木の床 albedo 2×2確認 | 2048×2048 | なし |
| floor_wood_normal_2x2.png | 木の床 normal 2×2確認 | 2048×2048 | なし |
| wall_plaster_albedo.png | しっくいの壁 色 | 1024×1024 | あり |
| wall_plaster_normal.png | しっくいの壁 ノーマル OpenGL | 1024×1024 | あり |
| wall_plaster_albedo_2x2.png | しっくいの壁 albedo 2×2確認 | 2048×2048 | なし |
| wall_plaster_normal_2x2.png | しっくいの壁 normal 2×2確認 | 2048×2048 | なし |
| fabric_albedo.png | 布 色 | 1024×1024 | あり |
| fabric_normal.png | 布 ノーマル OpenGL | 1024×1024 | あり |
| fabric_albedo_2x2.png | 布 albedo 2×2確認 | 2048×2048 | なし |
| fabric_normal_2x2.png | 布 normal 2×2確認 | 2048×2048 | なし |
| cardboard_albedo.png | 段ボール 色 | 1024×1024 | あり |
| cardboard_normal.png | 段ボール ノーマル OpenGL | 1024×1024 | あり |
| cardboard_albedo_2x2.png | 段ボール albedo 2×2確認 | 2048×2048 | なし |
| cardboard_normal_2x2.png | 段ボール normal 2×2確認 | 2048×2048 | なし |
| rug_albedo.png | ラグ 色 | 1024×1024 | あり |
| rug_normal.png | ラグ ノーマル OpenGL | 1024×1024 | あり |
| rug_albedo_2x2.png | ラグ albedo 2×2確認 | 2048×2048 | なし |
| rug_normal_2x2.png | ラグ normal 2×2確認 | 2048×2048 | なし |
| plant_leaves_atlas.png | 観葉植物の葉：3列×2段 | 1024×683 | なし |
| can_labels_atlas.png | 缶ラベル：2列×3段 | 1024×683 | なし |
| bottle_labels_atlas.png | 瓶ラベル：上から3種 | 1024×512 | なし |
| magazine_covers_atlas.png | 雑誌表紙：2列×2段 | 936×1024 | なし |
| fridge_notes_atlas.png | 冷蔵庫のメモ：3列×2段 | 1024×683 | なし |
| paper_scraps_atlas.png | 紙くず：2列×2段 | 1024×683 | なし |
| pizza_toppings_atlas.png | ピザの具：上段ペパロニ、中段ピーマン、下段チーズ | 1024×683 | なし |
| cat_neon_pink.png | ネコのネオンサイン：ピンク色 | 1024×683 | なし |
| cat_neon_mask.png | ネコのネオンサイン：白線の発光マスク | 1024×683 | なし |
