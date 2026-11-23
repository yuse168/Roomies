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

見た目は `Visual/Table`（台 1 つのメッシュ。`RoomiesCasinoArt.BlackjackTable()`）。台の当たり判定はルートの `BoxCollider` 1 つ。
カード・チップ・点数の札は、台の上の実物（ルートの直下の `Pivot_Card_P1〜8`・`Pivot_Card_D1〜8`・`Pivot_Chips_Bet / Double / Payout`・`Anchor_Score_Player / Dealer` と、位置の `Anchor_Shoe`・`Anchor_Discard`・`Anchor_PlayerHand`・`Anchor_DealerHand`・`Anchor_Bet`・`Anchor_Payout`・`Anchor_Tray`）。`BlackjackTableView` が動かす。実行時には作らない。
上のモニターは `Anchor_Display`（賭け金と結果だけの補助）。どちらも `Roomies/Casino/2 Build Gimmick Prefabs` が置く（そのあと `3 Place In Open Scene` も実行する）。位置は、プレハブの中で直接直してよい（置き直すと配置ツールの値に戻る）。
