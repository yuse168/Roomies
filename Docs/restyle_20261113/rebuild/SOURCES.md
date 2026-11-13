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
