# Play Mode の確認（2026/10/29）

ゲームの流れ（3日のサイクル・夜のイベント・セーブ/ロード・お金/HUD・ベッドと今日のおねがい）を、
**自動のテストで見る部分**と**人が手で見る部分**に分けた。

## 1. 自動の PlayMode テスト

`Assets/Tests/PlayMode/RoomiesPlayLoopTests.cs`（アセンブリ `Roomies.PlayModeTests`）。
ホスト1人・Steam なし（ソケットを使わないテスト用トランスポート）で、GameRoom を NGO の SceneManager で読み込む。
本番と同じく GameRoomUI はホストが Additive で読み込む。

| 順 | テスト | 見ていること |
|---:|---|---|
| 1 | `Boot_GameRoomAndUiAreLoadedOnce` | GameRoom と GameRoomUI が1つずつ・EventSystem 1つ・1日目の朝・HUD に「DAY 1」 |
| 2 | `Money_SharedAccountAndHudFollow` | 共同口座の入金/出金、足りない額は引けない、共同口座の文字と HUD の所持金が同じ額 |
| 3 | `DailyRequest_IsPickedAndShownOnHud` | 1日目のおねがいが「稼ぐ」か「配達」、HUD の「いまの目標」におねがいの進み具合が出る |
| 4 | `Night_ArrivesAndEventBannerPlays` | 夜になる、HUD が「夜」、夜のイベントのバナーが出て見出しが空でない |
| 5 | `Bed_SleepingAloneEndsTheNight` | ベッド5台、夜にベッドの近くで「寝る」→ 1人なら全員寝たことになり 2日目の朝、朝に起きる |
| 6 | `Rent_Day3IsPaidOnceAndDay4Starts` | 3日目の夜の終わりに家賃が1回だけ引かれ、4日目の朝になる |
| 7 | `Save_AutosaveHasTheCurrentState` | オートセーブの日付・残高が今の状態と同じ |
| 8 | `Load_ContinueRestoresDayMoneyAndFurniture` | 通信を止めて「続きから」と同じ手順で始め直すと、セーブの日付・残高・家具が戻る |
| 9 | `GameOver_RentUnpaidShowsScreenAndDeletesSave` | 家賃が払えないとゲームオーバー画面、残高は減らない、HUD が「GAME OVER」、セーブが消える |
| 100 | `NoErrorsWereLogged` | 上の流れの間にエラーログ・例外が1件も出ない |

- 日付は `DayManager` のサーバー側の処理（夜にする・朝へ飛ばす）で進める。N キーやマウスは使わない
- 朝の演出は `Time.timeScale = 4` で早送りする
- **セーブの場所は元のプロジェクトと共通**（`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Roomies\roomies_save.json`）。
  テストの前に `.playmode-test-backup` へ退避し、終わったら元に戻す。退避ファイルが残っていたらテストは始まらない（中身を見て手で戻す）
- `BootSceneBuilder` がエディターの Play 開始シーンを BootScene に固定しているので、テストのあいだだけ外して戻す（`IPrebuildSetup`）

### 実行のしかた
- エディター：Window > General > Test Runner > PlayMode > `RoomiesPlayLoopTests` を Run
- バッチモード（エディターを開いていない別フォルダで）：
  ```
  Unity.exe -batchmode -projectPath <プロジェクト> -runTests -testPlatform PlayMode -testResults <結果.xml> -logFile <ログ>
  ```
  環境変数 `ROOMIES_CAPTURE_DIR` を入れると、各段階のスクショ（`pm_01_…png` など）をそこへ保存する

### 結果（2026/10/29・worktree `leica/roomies-20261029`・バッチモード）
- 11件中 11成功（Roomies の10件＋UnitySkills の1件）。エラーログ0
- ログに出た警告（前からのもの）：`pCube4` の巨大な三角形（Physics）、オーディオリスナーが無い（シーンの切り替え中）
- スクショ：`D:\Bonchan\Roomies_save_reports\leica_20261029\screens\playmode_run1\`

## 2. 手で見る部分（自動にできない・していない）

自動テストはサーバー側の処理を直接呼んでいるので、**入力・見た目・音・2人以上**は見ていない。
GameRoom を開かずに **BootScene から Play**（ふだんどおり）。1回 15〜20 分。

### A. 入力と操作
1. タイトル →「あそぶ」→「部屋をつくる」→ ロビー →「ゲーム開始」を**マウスで**押せる
2. WASD・ジャンプ・しゃがみ・E で持つ/投げる が効く
3. Esc でポーズが開く／閉じる。設定のスライダー（感度・音量）を動かして、もう一度開いたとき値が残っている
4. キー設定で1つ変えて効く。「初期設定に戻す」の確認ダイアログが出る
5. N キー（ホスト）で昼→夜→朝が進む

### B. 夜とベッド
6. 夜に家の西側のベッドへ近づくと「寝る」と出る。E で寝ると動けなくなり、もう一度 E で起きる
7. 寝たとき「あと○人で朝」の告知が出る。1人なら約2秒で朝になる
8. 朝まで寝たあと、少しのあいだ移動が速い（ぐっすり）
9. ベッドが壁や家具にめり込んでいない（見た目）
10. 夜のイベントを何晩か見て、停電で部屋が暗くなる・ブレーカーで戻る、冷蔵庫の扉が開く

### C. お金・おねがい・家賃
11. 配達で稼ぐと、共同口座と左下の「いまの目標」の数字が増える。達成でボーナスの告知
12. 3日目の夜を越すと家賃の画面（引き落とし中→支払い完了）→ 4日目の朝
13. 6日目（2周目）の家賃が上がっている
14. 払えないとゲームオーバー →「もう一度」で1日目から、「メインメニューへ」でタイトル

### D. セーブ・続きから
15. 2日目以降でタイトルへ戻ると「続きから」が出る。押すとその日・残高・置いた家具が戻る
16. ゲームオーバーのあとは「続きから」が出ない

### E. 2人以上（Steam・同じコミットのビルド2台）
17. クライアントでも GameRoomUI が1つだけ（HUD が2重にならない）
18. クライアントが寝る・家賃の画面・ゲームオーバー画面が同じように出る
19. クライアント側の「メインメニューへ」

### 返し方
番号ごとに ○／×（×はスクショか一言）。
