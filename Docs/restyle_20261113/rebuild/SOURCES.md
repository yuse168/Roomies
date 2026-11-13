# 町の作り直し（2026/11/13）で使った素材の出どころ

外から取り込んだ素材（モデル・テクスチャ・音など）は使っていない。すべて自作。

| 物 | 作り方 | 場所 |
|---|---|---|
| 家・店の外壁・屋根・窓・屋上の設備・通りの小物・灯具 | Unity の箱と円柱を配置ツールで並べたもの | `Assets/_Scripts/Editor/RoomiesTownRebuild*.cs` |
| 妻の三角のメッシュ | コードで作ったメッシュ | `Assets/_Art/Restyle/Town/town_gable.asset` |
| まとめたメッシュ | 上の部品を Unity の CombineMeshes でまとめたもの | `Assets/_Art/Restyle/Town/Combined/` |
| レンガ・店内・部屋の窓・ビルの窓のテクスチャ | コードで1画素ずつ描いた手続きテクスチャ | `Assets/_Art/Restyle/Textures/tpl_brick*`・`town_*` |
| 素材（マテリアル） | 既存の素材の型（`Assets/_Materials/Templates/T_*.mat`）の子 | `Assets/_Materials/Templates/Variants/*/Town/` |

## 自然物の作り直し（2026/11/13 午後）

外から取り込んだ素材は使っていない（Poly Haven・ambientCG・Quaternius なども使っていない）。購入もしていない。すべて自作。

| 物 | 作り方 | 場所 |
|---|---|---|
| 木（幹・枝・葉の板・針葉樹の枝）と LOD | コードで組み立てたメッシュ | `Assets/_Scripts/Editor/RoomiesNature.Trees.cs`、`Assets/_Art/Restyle/Nature/Trees.asset`、`Assets/_Prefabs/Environment/Nature/` |
| 樹皮・葉のかたまり・針葉の枝のテクスチャ | コードで描いた手続きテクスチャ（葉は1枚ずつ形を塗ったもの） | `Assets/_Art/Restyle/Nature/Textures/nature_bark*`・`nature_leaves*`・`nature_needles*` |
| 山（形・草地・森・岩肌・雪の塗り分け） | ノイズで作った高さの場のメッシュと、焼いたテクスチャ | `RoomiesNature.Mountains.cs`、`Nature/Mountains.asset`、`nature_mountain_*` |
| 町の芝生（草・土・小石・花）・草の細かい模様・境界の外の地面 | 手続きテクスチャ（ノーマルマップは高さから作成） | `RoomiesNature.Ground.cs`、`nature_ground_*`・`nature_grass_*` |
| 草の束・起伏のある地面 | コードで組み立てたメッシュ | `Nature/Ground.asset` |
| シェーダー | URP 標準の Lit（Unity の URP パッケージ同梱） | — |

ライセンスの確認が必要な外部素材はない。

## 作り直し2（2026/11/13 夕方）：葉の揺れ・牢屋・メインの家・カジノ・道路

外から取り込んだ素材は使っていない。購入もしていない。すべて自作。

| 物 | 作り方 | 場所 |
|---|---|---|
| 葉と草の揺れ | 自作シェーダー（URP の標準のライブラリだけを使う） | `Assets/_Art/Shaders/RoomiesFoliageLit.shader` |
| 牢屋・メインの家・カジノ・道路の部品 | Unity の箱・円柱・板を配置ツールで並べたもの | `Assets/_Scripts/Editor/RoomiesTownRebuild.Jail/MainHouse/Casino/Road.cs` |
| カジノの絨毯・アスファルト・歩道の平板のテクスチャ | コードで描いた手続きテクスチャ（ノーマルマップは高さから作成） | `Assets/_Art/Restyle/Nature/Textures/casino_carpet*`・`road_asphalt*`・`sidewalk_slabs*` |
| 素材（マテリアル） | 既存の素材の型の子、URP Lit | `Assets/_Materials/Templates/Variants/*/Town/`、`Assets/_Materials/Nature/` |

ライセンスの確認が必要な外部素材はない。

## 作り直し3（2026/11/13 夜）：カジノ・留置所・メインの家を建物として0から

外から取り込んだ素材は使っていない。購入もしていない。すべて自作。

| 物 | 作り方 | 場所 |
|---|---|---|
| カジノの建物（外壁・ひさし・シャンデリア・バー・両替所など） | Unity の箱・円柱・板を配置ツールで並べたもの | `Assets/_Scripts/Editor/RoomiesTownRebuild.CasinoBuilding.cs` |
| 留置所の建物（外壁・待合室・廊下） | 同上 | `RoomiesTownRebuild.JailStation.cs`・`RoomiesTownRebuild.Jail.cs` |
| メインの家（外壁・板張り・日よけ・階段・踊り場） | 同上 | `RoomiesTownRebuild.MainHouse.cs`・`RoomiesTownRebuild.MainHouseParts.cs` |
| 寄棟の屋根のメッシュ | コードで作ったメッシュ | `Assets/_Art/Restyle/Town/house_roof.asset` |
| 留置所の市松のタイルのテクスチャ | 手続きテクスチャ | `Assets/_Art/Restyle/Nature/Textures/station_checker*` |
| 看板の文字（CASINO・CASHIER・SLOTS・BLACKJACK・POLICE・VISITORS など） | 町のプレハブにあった看板の文字（TextMeshPro・LiberationSans）を写したもの | 町のプレハブ |

ライセンスの確認が必要な外部素材はない。
