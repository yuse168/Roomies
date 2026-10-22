# 寝る仕組みと「今日のおねがい」

3日ごとの家賃だけだと日によってやることが変わらないので、
**夜の終わり方**と**その日の目標**を足したもの。

## 寝る（ベッド）

- 夜の自由行動中、ベッドに近づいて **E で寝る**。もう一度 E で起きる
- 寝ている間は動けない（本人の画面だけで操作を止める）
- **全員が寝たら、残り時間を待たずに朝になる**（`SleepManager`）
  - 「あと ○人 で朝になる」と寝るたびに告知が出る
  - 1人で遊んでいるときも寝たら朝になる（`Allow Solo Skip` で切れる）
- 朝まで寝ていた人は、その朝のあいだ**体が軽い**（既定 1.15倍・180秒）

| 置き場所 | 何 |
|---|---|
| `Assets/_Prefabs/Night/Bed.prefab` | ベッド（`Visual/Placeholder` に仮の見た目、`Anchor_Sleep` が寝る位置） |
| シーンの `Beds` | 5台（最大人数ぶん）。家の西側の壁ぎわ |
| シーンの `SleepManager` | 何人寝たかを数えて朝にする係 |

置き直しは `Roomies/Night/Rebuild Bed Prefab` と `Roomies/Night/Place Beds In Open Scene`。
台数・位置は `RoomiesSleepBuilder` の定数か、置いたあとシーンビューで動かす。
倍率・秒数・全員寝たときの間は `SleepManager` のインスペクターで変えられる。

**本アセットに差し替えるとき**：`Visual/Placeholder` を消して本物を `Visual` の下に置き、
`Anchor_Sleep` を寝る位置に合わせ直すだけでよい（スクリプトは触らない）。

## 今日のおねがい

朝に1つ出る、その日だけの目標。達成すると共同口座にボーナスが入る。
**できなくても罰は無い**（3日の緊張感を増やしすぎないため）。

| 種類 | 内容 | 数え方 |
|---|---|---|
| `EarnMoney` | 今日 ¥○ 稼ぐ | 配達・密輸・スロット・BJ・採掘の入金 |
| `DeliverCount` | 配達を○件 | `DeliveryReward` の回数 |
| `GambleWins` | ギャンブルで○回勝つ | `SlotReward` と `BlackjackPayout` の回数 |
| `BuyFurniture` | 家具を○個置く | `FurniturePurchase` の回数 |
| `EveryoneSleeps` | 全員で寝て朝にする | `SleepManager` からの報告 |

- 1日目は分かりやすい2種類（稼ぐ・配達）からしか出ない
- 必要数は日が進むと上がる（`Target Growth`、既定1.15倍ずつ）
- 報酬は `GameBalance` の夜の倍率が掛かる（既定の基本額 ¥300）
- 半分まで進んだときにも告知する（`Announce Halfway` で切れる）

進み具合はサーバーが数える。入金の**理由**（`SharedMoneyReason`）を見ているだけなので、
新しい稼ぎ方を足すときは、入金時に正しい理由を渡せばそのまま数えられる。

置き場所はシーンの `DailyRequestManager`（`Roomies/Night/Place Beds In Open Scene` が一緒に置く）。
必要数・報酬・告知の有無はインスペクターで変えられる。

### まだ無いもの

- **HUDに進み具合が出ない**。いまは朝の告知・半分・達成の3回しかバナーが出ない。
  HUDを実体化したら、`DailyRequestManager.StatusText` を1行出すとよい
- 寝ている間の見た目（目を閉じる・画面を暗くするなど）は無い
