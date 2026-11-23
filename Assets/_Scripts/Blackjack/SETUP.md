# ブラックジャック設置方法

`Assets/_Prefabs/BlackjackTable.prefab` を `GameRoom` シーンへ配置する。
追加のInspector設定やNetworkPrefab登録は不要。

## 操作

- 照準を台へ合わせて `E`：ゲーム開始／HIT
- 照準を台へ合わせて `R`：STAND
- ゲーム開始前にマウスホイール：BET変更（10R／50R／100R）
- 照準を台へ合わせて `F`（持つキー）：DOUBLE（ダブルダウン。`allowDoubleDown` を入れた台だけ。最初の 2 枚のときに、賭け金を倍にして 1 枚だけ引く）

## ルール

- 1台につき同時に1人がプレイする
- 賭け金・カード抽選・勝敗・配当はServer側で処理する
- 賭け金と配当は共有口座とプレイヤー個人収支へ反映する
- Dealerは17以上になるまでカードを引く
- 通常勝利は賭け金込み2倍返し、Blackjackは2.5倍返し、PUSHは賭け金を返す
- 上の階の台は Prefab Variant（`Assets/_Prefabs/Casino/BlackjackTable_2F`・`_3F`）。賭け金・ダブルダウン・Blackjack の倍率だけが違う（2026/11/23）

## 見た目の差し替え

`BlackjackTable.prefab` ルートにある以下のコンポーネントは残す。

- `NetworkObject`
- `BlackjackTable`
- `BoxCollider`

見た目は `Visual/Art`。`Visual` 直下の古い部品（表示を切ってある）は台の当たり判定を持っているので、消さない。
画面（`RuntimeDisplay`）は、いまは `BlackjackTable` が実行時に作っている（AGENTS.md の「実行時に作らない」に合っていない。カジノの見た目の作り直しでプレハブに置く形に直す予定）。
