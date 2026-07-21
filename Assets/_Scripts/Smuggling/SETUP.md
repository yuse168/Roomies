# 闇バイト「運び屋」セットアップ

スクリプトのコンパイル後、仮プレハブは `Assets/_Prefabs/Smuggling` に自動生成されます。
見つからない場合は Unity のメニューから `Tools > Roomies > 闇バイト > 仮プレハブを生成・修復` を実行してください。

## GameRoom に置くもの

1. `Giver_Placeholder` を町はずれへ置く。
2. `Dealer_Placeholder` を家の裏へ置く。
3. `Police_Placeholder` を2体置く。夜ごとに1〜2体がランダムで活動する。
4. 各警察の子にある `Point 1` と `Point 2` を、往復させたい道の両端へ移動する。
5. `JailPoint` を牢屋内のプレイヤー出現位置へ置く。
6. `JailLabor_Placeholder` を牢屋内へ置く。

`Player.prefab` には生成処理が `SmugglingPlayer` を自動追加し、紙袋の仮プレハブも設定します。

## 本番モデルへの差し替え

- 渡し人、売人、警察、紙袋のプレハブを開く。
- ルートの `SmugglingAppearance` にある「本番モデル」へ Prefab または FBX を指定する。
- 必要なら位置、回転、スケールを同じコンポーネントで調整する。
- 元の当たり判定とゲーム用コンポーネントはルート側に残す。

本番モデル内の Collider は誤検知防止のため実行時に無効化されます。

## 現在の牢屋労働（仮）

逮捕された翌朝、牢屋の作業台へ向けて E を10回押すと釈放されます。回数は `SmugglingConfig.JailLaborCount` で変更できます。
