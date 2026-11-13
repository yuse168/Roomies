# 町の作り直し（2026/11/13）で使った素材の出どころ

外から取り込んだ素材（モデル・テクスチャ・音など）は使っていない。すべて自作。

| 物 | 作り方 | 場所 |
|---|---|---|
| 家・店の外壁・屋根・窓・屋上の設備・通りの小物・灯具 | Unity の箱と円柱を配置ツールで並べたもの | `Assets/_Scripts/Editor/RoomiesTownRebuild*.cs` |
| 妻の三角のメッシュ | コードで作ったメッシュ | `Assets/_Art/Restyle/Town/town_gable.asset` |
| まとめたメッシュ | 上の部品を Unity の CombineMeshes でまとめたもの | `Assets/_Art/Restyle/Town/Combined/` |
| レンガ・店内・部屋の窓・ビルの窓のテクスチャ | コードで1画素ずつ描いた手続きテクスチャ | `Assets/_Art/Restyle/Textures/tpl_brick*`・`town_*` |
| 素材（マテリアル） | 既存の素材の型（`Assets/_Materials/Templates/T_*.mat`）の子 | `Assets/_Materials/Templates/Variants/*/Town/` |

ライセンスの確認が必要な外部素材はない。
