# ブラックジャック設置方法

`Assets/_Prefabs/BlackjackTable.prefab` を `GameRoom` シーンへ配置する。
追加のInspector設定やNetworkPrefab登録は不要。

## 操作

- 照準を台へ合わせて `E`：ゲーム開始／HIT
- 照準を台へ合わせて `R`：STAND
- ゲーム開始前にマウスホイール：BET変更（10R／50R／100R）

## ルール

- 1台につき同時に1人がプレイする
- 賭け金・カード抽選・勝敗・配当はServer側で処理する
- 賭け金と配当は共有口座とプレイヤー個人収支へ反映する
- Dealerは17以上になるまでカードを引く
- 通常勝利は賭け金込み2倍返し、Blackjackは2.5倍返し、PUSHは賭け金を返す

## 見た目の差し替え

`BlackjackTable.prefab` ルートにある以下のコンポーネントは残す。

- `NetworkObject`
- `BlackjackTable`
- `BoxCollider`

子オブジェクトの仮テーブルモデルは削除・差し替え可能。
画面は `BlackjackTable` がランタイム生成するため、PrefabへUIを追加する必要はない。
