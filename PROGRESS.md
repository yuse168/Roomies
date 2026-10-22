# PROGRESS.md

## 2026/10/22｜指摘された10点の修正（足音・NPC・UI・音の被りなど）

### 今回の変更
1. **足音が重なってうるさい** → 足音専用の再生口を1本使い回す `GameAudio.PlayExclusive` を追加。
   次の足音が鳴るとき前の音を鳴らし直すので重ならない。最短間隔 `footstepMinInterval`（既定0.26秒）も追加
2. **足音の素材を差し替え** → 「ぽよぽよした子供の足音」から革靴の単発音2種へ。
   `player/footstep_1` `_2` の連番でランダムに鳴り分ける（旧 `footstep.wav` は削除）
3. **床の材質で足音を変える** → `Resources/Audio/FootstepSurfaces.asset`。
   足音のたびに足元へレイを1本飛ばし、当たったマテリアル名でピッチと音量を引く（木・芝・金属など13種）
4. **NPCの足音** → `NpcMotionFeedback` を警察・売人・運び屋のプレハブへ追加。位置から鳴る
5. **NPCがAポーズ** → 同じコンポーネントがモデルのAnimatorへ `Speed` を入れて歩かせる。
   プレハブのAnimatorにプレイヤーと同じ `Bob_animation.controller` を割り当てた
6. **物を落としたときの音** → `CarryableObject` が手を離したときに `player/drop` を全員へ
7. **スロット回転中の音** → 回っている間 `slot/reel` を鳴らす（重ならないよう同じ口を使い回す）
8. **朝の演出とBGMの被り** → `GameMusic` が朝の切替演出（`DayManager.IsTransitioning`）の
   終了を待ってから朝の曲を鳴らす。待ち時間はインスペクターで調整できる
9. **家具ショップのキーが分からない** → 夜に家の中にいる間だけ「[P] 家具ショップを開く」を常時表示。
   判定範囲 `houseBounds` はインスペクターで調整できる
10. **ポーズ画面が中央からずれている** → ポーズカードの位置を中央へ戻した（以前は -470 で左寄せ）。
    `Card Offset` で微調整できる

### 変更ファイル
- 新規：`Assets/_Scripts/Audio/FootstepAudio.cs`、`FootstepSurfaces.cs`、
  `Assets/_Scripts/Smuggling/NpcMotionFeedback.cs`
- 新規（ツール）：`FootstepSurfacesBuilder`、`NpcMotionSetup`
- 新規（音源）：`player/footstep_1.wav`、`player/footstep_2.wav`、`player/drop.wav`、`slot/reel.wav`
- 削除：`player/footstep.wav`（ぽよぽよした旧素材）
- 変更：`GameAudio`、`GameMusic`、`PlayerMovement`、`CarryableObject`、`SlotMachine`、
  `DayManager`（`IsTransitioning` 公開）、`FurnitureEditController`、`EscMenuUI`
- 変更：NPCプレハブ3体、`Resources/Audio/SfxLevels.asset`、README、CREDITS

### 重要な仕様
- 足音は「素材1つ＋ピッチと音量」で床の違いを出す。音源を増やさずに済ませる方針
- NPCのAnimatorは、コントローラーに**ある**パラメーターだけ触る（無い物を触ると警告が出るため）
- `Bob_animation.controller` には Speed / Blend / Emote 系しか無い。
  **ジャンプのアニメーションはコントローラー側に未実装**なので、NPCにも入っていない
- 家具ショップのキー案内は一時メッセージ表示中は出さない（`hintCoroutine` が走っている間は譲る）

### 確認状況
- Unityでのコンパイル：**確認済み**（エラー0）
- Console：**確認済み**（エラー0）
- 音源の長さ：**確認済み**（足音0.63/0.69秒、落下1.31秒、リール3.0秒）
- NPCプレハブ：**確認済み**（3体に `NpcMotionFeedback` とコントローラー割り当てが入った）
- **Play Modeでの確認は未実施**：足音の重なり具合、材質ごとの違い、NPCの歩き、
  スロットの回転音、朝のBGMの間、家具ショップの案内、ポーズ画面の位置はすべて未検証
- **ポーズ画面のずれは原因を断定できていない**（中央に戻しただけ）。直っていなければ画面の写真が要る

### 未完了・次の作業
- 上記をPlay Modeで確認し、音量（`SfxLevels.asset`）と材質の値（`FootstepSurfaces.asset`）を詰める
- NPCのジャンプ用アニメーションはコントローラーに追加が必要
- テスト失敗15件の切り分け（前回からの持ち越し）

## 2026/10/22｜閉鎖中の看板に「CLOSED」の紙を貼った

### 今回の変更
- 店名の看板（`ONE MORE ROCK`、`ONE MORE?`）に、閉店した店のように
  **紙をテープで貼った見た目**を足した（`Closed notice`：紙＋テープ2枚＋「CLOSED」の文字）
- 紙の大きさは看板の `TextMeshPro` の実寸から決めるので、看板を替えても追従する
- 看板そのものは消していない。`Roomies/Mining/Reopen Mine (remove gate)` で紙を剥がせば元に戻る

### 変更ファイル
- 変更：`Assets/_Scripts/Editor/MiningClosedGateBuilder.cs`
- 変更：`Assets/_Scenes/GameRoom.unity`（紙の実体を配置・保存済み）

### 確認状況
- Unityでのコンパイル：**確認済み**（エラー0）
- Console：**確認済み**（エラー0）
- 見た目：**確認済み**（シーンビューで2か所の看板に紙が貼られ、「CLOSED」が読めることを目視）
- 紙の大きさの好み（看板をどれだけ隠すか）は**未調整**。`AddPaperNotice` の倍率で変えられる

## 2026/10/22｜採掘入口から操作のヒントを消す・メニューにBGM・テストを初実行

### 今回の変更
- **閉鎖中の採掘場で「何ができる場所か」を見せないようにした**
  - 隠したもの：買取機 `mine_cashier`、`ORE BUYBACK` と `HOLD ORE + E` の看板、
    `E / DOWN`（E / CLOSED）の案内、昇降ボタン
  - 入口の小看板は `CLOSED` だけに変えた（`ONE MORE ROCK` の店名は残す）
  - `PlayerInteract` も閉鎖中は採掘関連の案内を一切出さない（Eのプロンプトも出ない）
  - 戻す用に メニュー `Roomies/Mining/Reopen Mine (remove gate)` を追加
    （シャッターを消し、隠した物と看板の文言を元に戻す）
- **メニューシーン（MainMenuSteam）にBGMを配置**して保存した
- **EditModeテストを初めて実行した**（これまで一度も実行されていなかった）

### 変更ファイル
- 変更：`Assets/_Scripts/Editor/MiningClosedGateBuilder.cs`（ヒントの隠し／戻し）
- 変更：`Assets/_Scripts/PlayerInteract.cs`（閉鎖中は採掘の案内と操作を出さない）
- 変更：`Assets/_Scenes/GameRoom.unity`（買取機などを非表示・看板の文言・保存済み）
- 変更：`Assets/_Scenes/MainMenuSteam.unity`（`GameMusic` を配置・保存済み）

### 重要な仕様
- `Transform.Find` は名前の中の `/` をパス区切りとして扱うため、
  「Sign - E  /  DOWN」のような名前は見つけられない。子を総当たりで比較している
- 隠した物は削除していない。アップデートで開放するときは Reopen メニューで戻せる

### 確認状況
- Unityでのコンパイル：**確認済み**（エラー0）
- 入口の見た目：**確認済み**（シーンビューのキャプチャで「ONE MORE ROCK / CLOSED」と
  シャッターだけになり、買取機・Eの案内が消えたことを目視）
- メニューシーンのBGM：**確認済み**（`GameMusic` が1つ・シーン保存済み）
- 新規テスト `RoomiesBalanceTests`：**5/5 成功**
- EditMode全体：**229/245 成功・15失敗・1スキップ**

### 未完了・次の作業：テストの失敗15件の切り分け
このリポジトリでテストを実行したのは今回が初めてで、**変更前の基準が無い**。
失敗が今回の変更によるものか、元から落ちていたのかは未判定。
- 採掘関連（6件）：`MiningSceneConfigurationPassesExistingValidation`、
  `MiningCannotSellLooseOreOrAnotherPlayersCargo`、`MiningClosureConfiscates...`、
  `MiningFailedDepositKeepsOreForRetry`、`MiningForcedDropReleases...`、`MiningSaleCredits...`
  → 採掘場を閉鎖した影響の可能性が高い
- 家賃関連（3件）：`RentCyclesContinueBeyondDayThree`、`RentSuccessAdvances...`、
  `RentFailurePreserves...` → 家賃を `GameBalance` 経由にした影響の可能性
- 配達・家具（5件）：`DeliveryCreatesInitialBoxesOnNetworkSpawn`、`FurniturePurchase...`2件、
  `MissingFurniturePrefabDoesNotCharge`、`UntaggedChildColliders...`
  → 今回触っていない範囲なので、元から落ちていた可能性が高い
- `UnitySkills.Tests.Core...` 1件はパッケージ側のテストで本件と無関係
- **次回：1件ずつ原因を確認し、仕様変更なら期待値を直し、不具合なら修正する**

## 2026/10/22｜夜の設備（冷蔵庫・ブレーカー）を追加して夜イベントとつないだ

### 今回の変更
- 夜イベントが「バナーが出るだけ」だったので、**触れる設備**を2つ足した
  - **冷蔵庫 `NightFridge`**：夜に E で食材を補充（既定¥300）。食費の夜イベントが来たとき
    在庫があれば請求を肩代わりして在庫が消える。夜イベント「冷蔵庫の故障」で壊れ、
    壊れている間は補充できない（修理は夜に E・既定¥400）
  - **ブレーカー `HouseBreaker`**：停電の夜だけ触れる。上げ直すと朝を待たずに明かりが戻る
- 共通の形 `HomeAppliance` を追加（今後の夜ギミックはこれを継ぐだけで足せる）
  - `PlayerInteract` への追加は1か所だけ（表示と実行の2分岐）
- 夜イベント側に2種類を追加：`BlackoutFixed`（復旧した）、`FoodBillCovered`（冷蔵庫で肩代わり）
- プレハブはAGENTS.mdの規約どおり：見た目は `Visual/Placeholder`、扉は `Pivot_Door/Visual`、
  レバーは `Pivot_Lever/Visual`、触る判定はルートのコライダー1つ、原点は足元・正面+Z

### 変更ファイル
- 新規：`Assets/_Scripts/Night/HomeAppliance.cs`、`NightFridge.cs`、`HouseBreaker.cs`
- 新規：`Assets/_Scripts/Editor/NightApplianceBuilder.cs`（`Roomies/Night/...`）
- 新規：`Assets/_Prefabs/Night/{NightFridge,HouseBreaker}.prefab`
- 変更：`Assets/_Scripts/Night/NightEventManager.cs`、`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scenes/GameRoom.unity`（設備2つを配置・**未保存**）

### 重要な仕様
- 共有状態（在庫・故障・停電）はServerだけが変える。操作はRPC1往復で、距離もServerで確認する
- 金額・距離・扉の角度はすべて `[SerializeField]`。インスペクターで調整できる
- 設備が無いシーンでも夜イベントはこれまで通り動く（`Instance` が無ければ素通り）
- 置き場所は共用スペースの北側の壁ぎわ（冷蔵庫 21.8/3.3/22.9、ブレーカー 19.3/4.7/23.2）。
  **現物合わせは未調整**なので、シーンビューで動かして詰める想定

### 確認状況
- Unityでのコンパイル：**確認済み**（エラー0）
- Unity Console：**確認済み**（エラー0）
- プレハブ生成と配置：**確認済み**（2つとも1つずつシーンに存在）
- 見た目：**ざっくり確認**（室内のキャプチャで壁ぎわに置かれているのを確認。細かい位置は未調整）
- **Play Modeでの動作（補充・故障・停電復旧）：未実施**
- **シーンは未保存**

### 未完了・次の作業
- Unityで Ctrl+S（設備2つの配置）
- UnitySkillsのモードはまだ `auto` のまま。Bypassにすればテスト実行・シーン保存・
  メニューシーンへのBGM配置をこちらで行える
- ベッド（寝て夜を早送り）は未着手
- 残り：UIの実体化、ワールドの実行時生成の実体化、ミニバイト拡充

## 2026/10/22｜BGM導入・逮捕演出の映像化・音量と数値の集約・ゲームオーバー画面

### 今回の変更
- **BGMを導入した**（これまで音量スライダーだけで再生の仕組みが無かった）
  - Audiostockから3曲を取得（リポジトリを軽くするためMP3）。`Assets/_Audio/Music/` に配置
  - `GameMusic`（プレハブ）がシーンに常駐し、朝＝`day`、夜＝`night`、メニュー＝`menu` をクロスフェード
  - 音量は `GameSettings.BgmVolume`（設定画面のスライダー）がそのまま効く
  - ゲームオーバーでBGMを止め、`day/gameover` を鳴らす
- **逮捕演出を映像にした**（これまでIMGUIで青い四角と文字を出すだけだった）
  - 旧 `SmugglingArrestOverlay` を削除し、`ArrestCinematic`（プレハブ）に置き換え
  - 流れ：サイレン＋赤青フラッシュ → カメラが後ろ上へ引いて自分が映る → 白フラッシュ＋
    「ARRESTED」スタンプ（手錠SE）→ 暗転（サーバーのテレポート1.7秒に合わせる）→ 牢屋で明転
  - 追跡中の赤い脈動と警告文も同じ実体へ移した
  - 操作ロックを1.8秒→2.9秒にし、明転までカメラが戻らない間の誤操作を防いだ
- **効果音の音量をコードから外した**
  - `Resources/Audio/SfxLevels.asset`（31キー）に集約。インスペクターで調整する
  - 未配線だったSEを配線：ブラックジャック（配り始め・勝ち・負け）、家具（持ち上げ・設置）、
    家賃（成功・失敗）、起動ロゴ
  - 効果音を追加取得：`police/siren`、`police/arrest`、`day/gameover`
- **数値を `GameBalance.asset` に集約し、難易度上昇を入れた**
  - 家賃は周回ごとに上がる（既定：1周目500 → +150ずつ → 上限2000）
  - 夜イベントの金額倍率・「何も起きない夜」の重みの下がり方もここで調整する
  - `SmugglingConfig` の報酬・罰金・労働回数も同じ表から読むようにした
- **ゲームオーバー画面を作った**
  - `GameOverScreen`（プレハブ）。家賃がいくら払えなかったかを表示
  - ホストだけに「もう一度」（Day1の朝からやり直す。家具と所持金は残す）、全員に「メインメニューへ」
- **EditModeテストを6件追加**（`RoomiesBalanceTests`）：家賃の値上がりと上限、夜イベントの倍率と上限、
  平和な夜の重みの下限、音量表の未登録キーとマイナス値の扱い

### 変更ファイル
- 新規：`Assets/_Scripts/Audio/GameMusic.cs`、`Assets/_Scripts/Audio/SfxLevels.cs`、
  `Assets/_Scripts/GameBalance.cs`、`Assets/_Scripts/UI/GameOverScreen.cs`、
  `Assets/_Scripts/Smuggling/ArrestCinematic.cs`
- 新規（ツール）：`GameMusicBuilder`、`SfxLevelsBuilder`、`GameBalanceBuilder`、
  `GameOverScreenBuilder`、`ArrestCinematicBuilder`
- 新規（アセット）：`_Audio/Music/{menu,day,night}.mp3`、`Resources/Audio/police/{siren,arrest}.wav`、
  `Resources/Audio/day/gameover.wav`、`Resources/Audio/SfxLevels.asset`、
  `Resources/Balance/GameBalance.asset`、`_Prefabs/Audio/GameMusic.prefab`、
  `_Prefabs/UI/{ArrestCinematic,GameOverScreen}.prefab`
- 新規（テスト）：`Assets/Tests/Editor/RoomiesBalanceTests.cs`
- 削除：`Assets/_Scripts/Smuggling/SmugglingArrestOverlay.cs`
- 変更：`DayManager`、`NightEventManager`、`SmugglingPlayer`、`SmugglingConfig`、`GameAudio`、
  `PlayerMovement`、`SlotMachine`、`BlackjackTable`、`DeliveryButton`、`NetworkFurniture`、
  `LNStudioSplash`、`UITheme`、`HudThemer`、`Assets/Resources/Audio/README.md`、`Assets/_Audio/CREDITS.md`

### 重要な仕様
- 新しい物はすべて**シーン／プレハブの実体**。実行時に見た目を作らない（AGENTS.mdの新ルール）
- 曲の差し替えは `GameMusic` プレハブの3つのスロットを入れ替えるだけ
- 効果音の音量は `SfxLevels.asset`、ゲームの数値は `GameBalance.asset` に分けた
- `GameBalance.asset` が無くてもコード側の既定値で動く（壊れない）
- 家賃の値引き（大家の気まぐれ）は「その周の基本家賃の半額」までが下限

### 確認状況
- Unityでのコンパイル：**確認済み**（エラー0。テスト用アセンブリ込み）
- Unity Console：**確認済み**（エラー0）
- プレハブの参照割り当て：**確認済み**（`GameMusic` の3曲とAudioSource、`ArrestCinematic` の8要素）
- GameRoomへの配置：**確認済み**（`GameMusic`・`ArrestCinematic`・`GameOverScreen` が各1つ）
- `dotnet build`：**確認済み**（`Roomies.RegressionTests` もエラー0）
- **テストの実行：未実施**（`test_run_by_name` はUnitySkillsのautoモードで禁止）
- **Play Modeでの確認・音の試聴：未実施**（変わらず）
- **メニューシーンへのBGM配置：未実施**（`scene_load` がautoモードで禁止）
- **シーンは未保存**（`GameOverScreen` を置いた分）

### 未完了・次の作業
- Unityで Ctrl+S（`GameOverScreen` の配置）
- `MainMenuSteam` を開いて `Roomies/Audio/Place Music In Open Scene` を実行する
- UnitySkillsをBypassモードにすれば、シーンの保存・切り替え・テスト実行をこちらで行える
- 残り：UIの実体化（HUD→ロビー→ESC/設定）、ワールドの実行時生成の実体化、
  夜の中身（冷蔵庫・ブレーカー・ベッド）、ミニバイト拡充
- 効果音は未試聴のまま。`SfxLevels.asset` で音量調整が必要

## 2026/10/22｜「実行時に作らない」ルールを追加し、閉鎖シャッターを実体化

### 今回の変更
- `AGENTS.md` に **「実行時に作らない（エディタで全部触れる形にする）」** を追加した
  - 見た目・当たり判定・配置をスクリプトから新規生成しない。シーンかプレハブに実体を置く
  - 実行時のスクリプトは、置いてある物の表示切り替え・値の反映・アニメだけ行う
  - 数が実行時に決まる物（プレイヤー・鉱石・家具・配達物）だけ例外。その場合もプレハブを `Instantiate` する
  - 並べるのが大変な物はエディタ拡張（`Roomies/〜` メニュー）で生成してシーンに保存する
  - 数値は `[SerializeField]` にしてインスペクターから調整できるようにする
- 直前に入れた閉鎖シャッターが「Play Modeでしか出ない」作りだったので、ルールに合わせて作り直した
  - `MiningSite` の実行時生成（`BuildClosedGate` / `GatePart`）を削除
  - 代わりに `[SerializeField] closedGate` を追加し、`serviceClosed` に合わせて `SetActive` するだけにした
  - シーンへ置くツール `MiningClosedGateBuilder`（メニュー `Roomies/Mining/Build Closed Gate`）を追加
  - Unityでツールを実行し、`Mine entrance beside house/Closed gate` として実体を配置済み（シャッター1＋横板6＋警告帯2）

### 変更ファイル
- 変更：`AGENTS.md`
- 変更：`Assets/_Scripts/Mining/MiningSite.cs`
- 新規：`Assets/_Scripts/Editor/MiningClosedGateBuilder.cs`
- 変更：`PROGRESS.md`
- **未保存**：`Assets/_Scenes/GameRoom.unity`（`Closed gate` の実体と看板2枚の文言。Unityで Ctrl+S が必要）

### 重要な仕様
- シャッターはシーン上の実体なので、Play Modeに入らずに位置・大きさ・マテリアルを直せる
- `MiningSite.closedGate` に実体を割り当て済み。スクリプトは名前で探さない
- ツールは何度実行しても同じ結果（既にあれば作り直す）。アップデートで開放するときは
  `Closed gate` を消して `serviceClosed` をOFFにする
- 既定値はツール内の定数（幅3.4／高さ3.3／奥行0.16／z=-0.7）。シーン側で動かした分はツール再実行で戻る

### 確認状況
- C#コンパイル：確認済み（`dotnet build` の `Assembly-CSharp` / `Assembly-CSharp-Editor` ともエラー0、
  Unity側の再コンパイルもエラー0）
- ツールの実行：確認済み（`Closed gate` がシーンに生成され、`MiningSite.closedGate` へ参照が入った）
- **見た目：確認済み**（Play Modeに入らず、シーンビューのキャプチャでシャッター・横板・警告帯・
  「CLOSED FOR RENOVATION」「E / CLOSED」の表示を目視。確認用画像は削除済み）
- **シーンは未保存**。Ctrl+Sを押すまでディスクには反映されていない
- Play Mode・音の試聴・5人接続：未実施（変わらず）

### 未完了・次の作業：実行時生成が残っている箇所
新ルールの対象外（当面そのまま）だが、見つけたら実体化していく。
- **ワールドの見た目（優先度：高）**
  `BlackjackTable`（カード・チップ）、`CharacterCloset`（見た目プレビュー）、
  `SmugglingContact` / `SmugglingPlayer`（NPC周り）、`FurnitureEditController`（設置プレビュー）、
  `MiningFeedback`（破片・ガス）、`NetworkFurniture`（`Visual` 子）、`MiningPlayer`（ツルハシ）
- **UI（優先度：中・まとめて作業する前提）**
  `UITheme` / `HudThemer` / `MenuThemer` / `UIThemeBootstrap` / `EscMenuUI` /
  `MainMenuSettingsUI` / `EmoteWheelUI` / `LobbyUIManager` / `PlayerEarningListUI` /
  `DayResultUI` / `DayTransitionUI` / `RentPaymentUI` / `NightEventUI` / `LNStudioSplash` ほか
- **実体不要（見た目ではないので対象外）**
  `GameAudio` のAudioSourceプール、`SteamManager` の常駐オブジェクト、`NetworkDebugOverlay`

## 2026/10/22｜Unityで確認：コンパイル・採掘入口・音源の取り込み（Play Modeは未実施）

### 今回やったこと
- 起動済みのUnity（UnitySkills REST、port 8091）で、前の作業分を実機確認した
- `GameRoom.unity` を外部から書き換えたせいで「シーンが外部で変更された」ダイアログが出てエディタが停止していた。
  ユーザーが **Reload** を選択して解消（＝エディタ側の未保存のシーン編集は破棄された）
- シャッターが入口の看板（y=3.54）を隠す位置だったので、高さを 3.8→3.3、中心を 1.9→1.65 に下げた
- 入口の看板の文言を実機で変更（メモリ上のみ・**未保存**）
  - `MINING CO.  /  OPEN UNTIL SUNSET` → `CLOSED FOR RENOVATION`
  - `E  /  DOWN` → `E  /  CLOSED`

### 変更ファイル
- 変更：`Assets/_Scripts/Mining/MiningSite.cs`（シャッターの高さと警告帯の位置）
- 変更：`PROGRESS.md`
- 新規：`Assets/Resources/Audio/**/*.wav.meta` ほか（Unityが取り込み時に生成）
- **未保存**：`Assets/_Scenes/GameRoom.unity`（看板2枚の文言。エディタでCtrl+Sが必要）

### 確認状況
- Unityでのコンパイル：**確認済み**（2回とも `errorCount: 0`。警告1件は `EmoteWheelUI.cs` の既存の非推奨APIのみ）
- Unity Console：**確認済み**（リロード後のエラー0件）
- 採掘の閉鎖：**確認済み**（`MiningSite` の `IsServiceClosed=True` / `IsOpen=False` をエディタ上で読み出した）
- シャッターの座標：**確認済み（計算と実測の突き合わせ）**
  - 入口の実座標 (29,0,14)、`Roomies_Mining` ルートは原点、呼び出しボタンは (27.8,1.1,12.9)
  - シャッター面は z=13.22〜13.38 なので、ボタン（z=12.75〜13.05）は手前に残り押せる
  - 幅3.4は左右の支柱(x=27.35/30.65)の間に収まり、高さ3.3で開口部（y≈0〜3.3）をふさぐ
  - **ただしシャッター自体の見た目は未確認**（実行時生成なのでPlay Modeでしか出ない）
- 音源の取り込み：**確認済み**（9本すべて `AudioClip` として認識。`Resources/Audio/...` のパスも一致）
- 音の長さ・音量：**実測済み**（長さ1.2〜6.3秒。ただし新しい9本は既存の `money/*` よりRMSで約10dB大きい。
  `delivery/complete`・`night/lucky`・`slot/spin` はピークがほぼ0dBFS）
- **実際に音を聴いての確認：未実施**
- **Play Mode・5人接続・夜イベントの体感：未実施**（Play ModeはUnitySkillsのautoモードでは実行不可）
- スポーン地点：5か所あり、最小間隔1.4mで重なりなしを**確認済み**（出現の見た目は未確認）

### 未完了・次の作業
- Unityで **Ctrl+S** を押して看板2枚の文言を保存する（保存しないと元の「OPEN UNTIL SUNSET」に戻る）
- Play Modeでシャッターの見た目と、押したときの案内文を確認する
- 効果音9本を実際に鳴らして音量バランスを取る。大きすぎる場合は再生側の音量
  （`GameAudio.Play(key, volume)` の第2引数）を下げる
- 3D再生する音（`slot/*`）はステレオのままなので、必要なら Force To Mono を検討する
- 5人接続・夜イベントの体感調整は引き続き未着手

## 2026/10/22｜採掘を一旦閉鎖・5人対応・夜イベント拡充・効果音9本を追加

### 今回の変更
- **採掘場を閉鎖した**（リリース後のアップデートで開放する前提）
  - `MiningSite.serviceClosed`（既定ON）を追加。ONの間はエレベーター・ツルハシ・買取機・岩のどれも反応しない
  - 入口の建物はそのまま残し、昇降口にシャッターと警告帯をランタイム生成して「閉まっている」見た目にした
  - 呼び出しボタンは押せる位置に残し、押すと「採掘場はシャッターが降りている。アップデートで開放予定」と出る
- **5人プレイに対応した**
  - Steamロビーの上限を4→5に変更（コードの既定値と`MainMenuSteam`の設定値の両方）
  - ロビーの名札が画面幅に収まらないときだけ一列まとめて縮小する処理を追加。長すぎる名前は末尾を省略
- **夜イベントを拡充した**（6種→13種、良いことも起きるようにした）
  - 追加：空き巣／冷蔵庫の故障／隣人の騒音／宝くじ当選／大家の気まぐれ（家賃値引き）／落とし物／謎の小包（中身は半々）
  - Dayが進むほど金額が上がり（上限2.5倍）、「何も起きない夜」の確率が下がる
  - Day2以降はまれに2件同時に起きる。ただし出費イベントは同じ夜に重ならない
  - 直前の夜と同じイベントは出さない。Day1では家賃値上げを出さない
  - 冷蔵庫が壊れた次の食費は2倍。隣人の騒音があった翌朝は移動速度が0.85倍（夜になると戻る）
- **効果音を9本追加して配線した**（Audiostockから取得、Chrome経由）
  - 夜イベント3種（trouble/lucky/peace）、日付切替2種（morning/night）、スロット3種（spin/stop/win）、配達1種（complete）

### 変更ファイル
- 変更：`Assets/_Scripts/Mining/MiningSite.cs`（閉鎖フラグ・シャッター生成・閉鎖中の案内）
- 変更：`Assets/_Scripts/Network/SteamLobby.cs`、`Assets/_Scenes/MainMenuSteam.unity`（上限5人）
- 変更：`Assets/_Scripts/LobbyUIManager.cs`（名札のはみ出し対策）
- 変更：`Assets/_Scripts/Night/NightEventManager.cs`（イベント13種・日数スケール・2件抽選・寝不足）
- 変更：`Assets/_Scripts/DayManager.cs`（`CurrentDay`/`MaxDay`公開、家賃の値引き対応、朝夜の効果音）
- 変更：`Assets/_Scripts/PlayerMovement.cs`（`speedScale`：一時的な移動速度倍率）
- 変更：`Assets/_Scripts/SharedMoneyManager.cs`（`NightTrouble`/`NightLuck`の理由を追加）
- 変更：`Assets/_Scripts/Slot/SlotMachine.cs`、`Assets/_Scripts/Delivery/DeliveryButton.cs`（効果音の配線）
- 変更：`Assets/_Scenes/GameRoom.unity`（`MiningSite.serviceClosed: 1` を明示）
- 変更：`Assets/Resources/Audio/README.md`、`Assets/_Audio/CREDITS.md`
- 新規：`Assets/Resources/Audio/{night,day,slot,delivery}/`（WAV 9本）

### 重要な仕様
- 採掘の再開は `MiningSite.serviceClosed` をOFFにするだけ。シーンのオブジェクト・地下構造・鉱石プレハブは消していない
- 閉鎖中のシャッターは各クライアントがローカルに生成する見た目だけのもの。ネットワーク同期はしない
- 家賃は値引きもできるようになった（`ServerAddRentSurcharge`に負値）。ただし基本家賃の半額までが下限
- 寝不足の速度低下は各クライアントが自分のプレイヤーにだけ掛ける（RPCは増やしていない）
- 夜イベントのRPCは既存の`DayManager.ServerSendNightEvent`のまま。種類をbyteで足しただけなので通信量は変わらない
- 効果音は`Resources/Audio/`に置くだけで鳴る方式のまま。未入荷のキーは黙って無視される

### 影響範囲
- 採掘ジョブ全体（事実上プレイ不可になる）
- Steamロビー・参加人数・ロビーUI
- 夜の進行、共同口座、個人所持金、次回家賃、朝の移動速度
- スロット、配達、日付切替の音

### 確認状況
- C#コンパイル：確認済み（`dotnet build` で `Assembly-CSharp` / `Assembly-CSharp-Editor` / `Roomies.RegressionTests` の3つともエラー0。
  警告1件は `EmoteWheelUI.cs` の既存の非推奨API警告で今回とは無関係）
- 効果音9本のダウンロードとファイル配置：確認済み（WAV、長さ1.2〜6.3秒）
- Audiostockの作品番号・クレジット名：確認済み（ダウンロードモーダルの表示から取得）
- **Unity Editor／Play Modeでの確認：未実施**（UnityMCPが接続できずEditorを操作できなかった）
- **効果音は未試聴**。タイトルと用途タグで選んだだけで、音は聴いていない
- **閉鎖シャッターの見た目：未確認**。座標は`RoomiesMiningBuilder`の入口位置(29,0,14)から計算したもので、実際の位置合わせは未検証
- **5人での接続：未確認**。スポーン地点はシーンに5つあることだけ確認（配置の妥当性は未検証）
- **夜イベントの体感・金額バランス：未確認**

### 未完了・次の作業
- Unityを開いてコンパイルとConsoleを確認し、閉鎖シャッターの位置・見た目を目視で直す
- 採掘入口に「アップデートで開放予定」の看板テキストを出すか決める（今は音・バナーの文言のみ）
- 5人で接続し、スポーン5地点の重なり・ロビー名札・HUDの表示を確認する
- 夜イベントを一通り引いて、金額・2件同時・寝不足の体感を調整する
- 効果音9本を実際に鳴らし、長い／合わないものを差し替える（特に`day/morning`6.3秒、`night/peace`4.5秒）
- No.949044（17秒のファンファーレ）はダウンロード済みだが長すぎて不採用。`Downloads`に残っている
- ライセンス確認（Audiostock定額制エンタープライズの無料トライアル）の件は未解決のまま

## 2026/10/22｜家具の仮アセットを作り直した（無地の板 → 特徴のある形）

### 今回の変更
- 家具12種の仮アセットを、プリミティブ組み立て方式で作り直した
- 生成ツール `FurniturePlaceholderBuilder` を追加（メニュー `Roomies/Furniture/Rebuild Placeholder Art`）
- 調査ツール `FurnitureArtInspector` を追加（読み込み状況の確認と、一列に並べての目視確認）
- 既存パレット（`RoomiesArt/Materials` の13色）を流用し、新規マテリアルは作っていない

### 変更ファイル
- 新規：`Assets/_Scripts/Editor/FurniturePlaceholderBuilder.cs`
- 新規：`Assets/_Scripts/Editor/FurnitureArtInspector.cs`
- 変更：`Assets/Resources/RoomiesArt/Furniture/*.prefab`（12件すべて）
- 変更：`PROGRESS.md`

### 経緯：なぜ「箱」だったのか
- 旧アセットは頂点数が768〜11,089あり、数値だけ見ると造形されているように見えた
- しかし実際にレンダリングすると、面が細分化されているだけで**表面は完全に無地**だった
  （自販機はボタン・窓・取り出し口が一切ない赤い板）
- 頂点数を造形の指標にしたのが誤りで、目視確認で初めて判明した

### 重要な仕様
- 見た目はUnityプリミティブ（Cube/Sphere/Cylinder）を組み合わせて作る。メッシュアセットは生成しない
- **原点は中心のまま。**足元原点に変えると既存の設置位置計算が全部ずれるため、あえて規約の「足元原点」は適用していない
- 当たり判定はルートの `BoxCollider` ひとつだけ。飾りの子からはコライダーを削除している
- `BoxCollider` のサイズは `FurnitureCatalog` の `placeholderSize` と一致させる（12件とも一致を確認済み）
- 正面は +Z
- このプレハブ自体が「見た目」。`NetworkFurniture` が `Visual` という名前の子として生成するため、中に `Visual` を作らない（AGENTS.mdの家具の扱いに合わせた）
- 本アセットへ差し替えるときは `Assets/Resources/RoomiesArt/Furniture/<id>.prefab` を置き換えるだけでよい。カタログは `Resources.Load` で自動解決するのでコード変更は不要
- `FurnitureArtInspector` のレポートにある bounds 列はメッシュのローカル値なので、実寸の判断には使えない（実寸はルートの BoxCollider を見る）

### パーツ構成（主なもの）
- 自販機：本体・窓・ボタン3・缶9・取り出し口・看板（17パーツ）
- エナドリ冷蔵庫：本体・ガラス扉・取手・天板・棚3・缶9（16パーツ）
- ミラーボール：球体＋鏡タイル50枚＋吊り棒（52パーツ）
- ちょきんばこ：胴・鼻・コイン投入口・耳2・脚4（9パーツ）
- かべ掛け時計：枠・文字盤・長針・短針・軸・目盛4（9パーツ）

### 影響範囲
- 夜の家具ショップのプレビュー表示
- 設置済み家具の見た目
- 当たり判定のサイズは変わらないため、設置・持ち運び・効果判定への影響はない

### 確認状況
- C#コンパイル：確認済み（エラー0）
- 12件すべての `Resources.Load` 成功：確認済み（プレハブ無し0件）
- レンダラー数が1→3〜52へ増えたこと：確認済み
- `BoxCollider` のサイズがカタログ指定と一致：確認済み（12件すべて、不一致0）
- パーツ名の構成：確認済み（自販機・冷蔵庫・時計・ちょきんばこを目視）
- 作り直し前の見た目が無地の板だったこと：確認済み（シーンビューで撮影して確認）
- **作り直し後の見た目：未確認**（シーンビューカメラの構図合わせがうまくいかず、全12件を並べた画で確認できていない）
- Unity Playでの表示：未確認
- GameRoomシーン：変更なし（一時配置したプレビューは削除済み、ファイル差分0件）

### 未完了・次の作業
- Unityで `Roomies/Furniture/Preview Row (place in scene)` を実行し、12件の見た目を目視で確認する（確認後は `Preview Row (clear)` で消す）
- 形が分かりにくい家具があれば `FurniturePlaceholderBuilder` の該当メソッドを調整して再生成する
- 旧アセットが参照していた `Assets/Resources/RoomiesArt/Meshes/*.asset` のうち、家具12件ぶんが未使用になった。他が参照していないか確認してから整理する
- 本アセットが用意できたら `Furniture/<id>.prefab` を置き換える

## 2026/10/21｜効果音を本番音源で導入・足音/UI/お金へ配線

### 今回の変更
- 採用が決まった効果音7本を本番WAVで取得し`Resources/Audio/`へ配置
- 足音・ジャンプ・着地・UI決定・UIキャンセル・入金・出金に実音を配線
- 足音を距離積算式で鳴らす処理を追加（速く動くほど間隔が詰まる）
- しゃがみ移動は歩幅を1.4倍・音量を約半分にして忍び足にした
- ジャンプ音を既存の`PlayJumpRpc`へ相乗りさせ、全クライアントで鳴るようにした
- 着地音用に`PlayLandRpc`を追加（ジャンプ1回につき1度なので低頻度）
- `UIButtonJuice`の押下時にUI音を鳴らす処理を追加
- ボタン名とラベルから「戻る・閉じる系」を推定し、キャンセル音へ自動で振り分けるようにした
- 共同口座の増減（`SharedMoneyManager.SharedMoneyChanged`）へ入金・出金音を配線
- クレジット表記用に`Assets/_Audio/CREDITS.md`を追加

### 変更ファイル
- 新規：`Assets/Resources/Audio/player/footstep.wav`（No.805951）
- 新規：`Assets/Resources/Audio/player/jump.wav`（No.69491）
- 新規：`Assets/Resources/Audio/player/land.wav`（No.52495）
- 新規：`Assets/Resources/Audio/ui/confirm.wav`（No.146290）
- 新規：`Assets/Resources/Audio/ui/cancel.wav`（No.139690）
- 新規：`Assets/Resources/Audio/money/gain.wav`（No.134475）
- 新規：`Assets/Resources/Audio/money/spend.wav`（No.1014552）
- 新規：`Assets/_Audio/CREDITS.md`
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/Resources/Audio/README.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- 音源はAudiostockの定額制エンタープライズプランで取得した本番WAV（48kHz/24bit中心）。試聴用サンプルMP3は使っていない
- 足音は`PlayerMovement.footstepDistance`（既定1.55m）だけ水平移動するごとに1回鳴る。Inspectorから調整できる
- **足音は自分のぶんだけローカルで鳴る。** 高頻度でRPCを打たないため（AGENTS.mdの「不正対策だけを目的とした高頻度RPCは避ける」と同じ理由で通信量を抑えた）
- ジャンプ・着地はRPC経由なので他プレイヤーのぶんも聞こえる
- UI音は押した瞬間（`OnPointerDown`）に鳴らす。離した時より反応が良く感じるため
- キャンセル音の振り分けは`UIButtonJuice.CancelNameHints`による名前・ラベルの推定。外した場合は生成側で`useCancelSound`を立てる
- 足音は1本のみ採用のため、`GameAudio`のピッチ揺らぎ（±9%）で単調さを散らしている。増やす場合は`footstep_1`〜`_8`へ改名して並べればコード変更なしで鳴り分ける
- 音源の差し替えは`Resources/Audio/`のファイルを置き換えるだけでよい。Inspector配線は無い

### 影響範囲
- プレイヤーの移動・ジャンプ・着地
- `UIButtonJuice`が付く全ボタン（メインメニュー・ESC・ロビー・設定・クローゼット等）
- 共同口座の増減表示
- マルチプレイ（ジャンプ・着地のRPCが各1種増える）

### 確認状況
- C#コンパイル：確認済み（エラー0、警告1件は`EmoteWheelUI.cs`の既存の非推奨API警告で今回とは無関係）
- Unity Console：確認済み（エラー0件）
- WAV7本のインポート：確認済み（`.meta`生成済み）
- `AudioClip`として認識されていること：確認済み（footstep/jump/confirm/gainをUnity側で型確認）
- `Resources.Load`のパス整合：コード上で確認済み（`Audio/` ＋ キー）
- Audiostockのクレジット名：確認済み（ダウンロード履歴から取得）
- **Unity Playでの実際の発音：未確認**（Play Mode開始はUnity Skillsの権限区分で現在のautoモードから実行できないため）
- 音量バランス：未調整（コード上の初期値のまま）
- Host/Client間でのジャンプ・着地音:未確認
- しゃがみ忍び足の体感：未確認

### 未完了・次の作業
- Unity Playで7音すべてが鳴ることと、音量バランスを確認する
- 足音の歩幅（`footstepDistance`）とUI音の音量を実プレイで調整する
- キャンセル音の名前推定が外しているボタンを洗い出し、`useCancelSound`を直接立てる
- 他プレイヤーの足音をどう鳴らすか決める（NetworkAnimator経由かローカル位置差分か。RPCは使わない方針）
- 第2段階：採掘4音（`mining/hit` `break` `sale` `warning`）の候補選定と差し替え。現在の`Assets/_Audio/Mining/`はコード生成のサイン波プレースホルダ
- 未配線の箇所：スロット・ブラックジャック・配達・家具・日付切替・家賃・夜イベント・密輸/警察・エモート・起動ロゴ
- **ライセンス確認：**定額制エンタープライズプランは現在**無料トライアル**（2026/10/01〜2027/03/31）。Steam配布への同梱と契約終了後の継続利用が認められるか担当営業に確認する
- ライセンス証明書の発行（マイページ > 購入者情報 の設定が必要）

## 2026/10/21｜既存プレハブを「見た目は Visual の下」の形にそろえた

### 今回の変更
- 見た目を `Visual` の下にまとめ、ロジック・通信・判定はその外に残した（オブジェクトは移動と名前変更だけ、参照は維持）
  - CharacterCloset：`Appearance_REPLACE_ME` → `Visual`
  - Slot：`Model` → `Visual`（画面の `Canvas` は外のまま）
  - BlackjackTable：テーブル・レール・脚・プレートを新しい `Visual` の下へ
  - DeliveryBox：`NormalVisual`／`RareVisual` → `Visual/Normal`／`Visual/Rare`
  - MiningOre_0〜10：鉱石モデルを `Visual` の下へ
  - Dealer／Giver／Police／PaperBag：`VisualRoot` → `Visual`
  - JailLabor：仮の見た目を `Visual/Placeholder` へ
- 名前で探していたエディタ用ツールを新しい名前に対応（旧名でも動く）
- CharacterCloset を実行時に作る場合も、見た目を `Visual` の下に作るようにした
- `AGENTS.md`：「ぶつかるだけ」のコライダーは `Visual` の中に置いてよいと明記

### 変更ファイル
- 新規：`Assets/_Scripts/Editor/RoomiesPrefabConventionFixer.cs`
- 変更：上記プレハブ17個
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`Assets/_Scripts/Editor/RoomiesExistingArtFinisher.cs`
- 変更：`Assets/_Scripts/Editor/RoomiesPlayabilityPolish.cs`
- 変更：`Assets/_Scripts/Smuggling/Editor/SmugglingPrefabGenerator.cs`
- 変更：`AGENTS.md`、`PROGRESS.md`

### 確認
- 確認済み：コンパイルエラーなし。コンソールにエラーなし。GameRoom に置かれた各プレハブ（スロット3台、NPC、ブラックジャック、クローゼット）が新しい形で読み込まれている。
- 確認済み：プレハブの差分は名前・親子関係の変更と `Visual` の追加だけ（消えたオブジェクトはない）。
- 未確認：自動テスト（Unity側のツールの安全モードで実行不可）。プレイでの動作確認。

## 2026/10/21｜プレハブのルールを決定（見た目の差し替え前提）

### 今回の変更
- `AGENTS.md` に「プレハブと仮アセット」の決まりを追加（コード・プレハブ・シーンは変更なし）
  - 背景・環境は直接置いて丸ごと置き換えてよい
  - ギミック付きはプレハブにし、見た目は子の `Visual` の下だけに置く
  - ギミックが使う位置は `Visual` の外の `Anchor_` オブジェクトにする
  - ドアが開く冷蔵庫などもギミック付きに含める。動く部品は軸 `Pivot_〇〇` をルート直下に置き、見た目は `Pivot_〇〇/Visual` に入れる

### 変更ファイル
- 変更：`AGENTS.md`
- 変更：`PROGRESS.md`

### 未対応
- 既存プレハブは旧名のまま：`Appearance_REPLACE_ME`、`Model`、`ModelRoot`、`NormalVisual`／`RareVisual`
- BlackjackTable は見た目の部品がルート直下にあり、差し替えられる形になっていない

## 2026/10/21｜効果音の再生基盤を追加・Audiostockから候補選定

### 今回の変更
- 効果音の再生口 `GameAudio` を新規追加
- `Resources/Audio/` 以下へファイルを置くだけで鳴る方式にした（Inspector配線なし）
- 連番ファイル（`key_1`〜`key_8`）をランダムに鳴り分ける仕組みを追加
- `GameSettings` へ効果音音量 `SfxVolume` とBGM音量 `BgmVolume` を追加
- 音声ファイルの置き場とファイル名規則を `Assets/Resources/Audio/README.md` に記載
- Audiostockで第1段階ぶんの効果音候補を選定（未DL・未採用）

### 変更ファイル
- 新規：`Assets/_Scripts/Audio/GameAudio.cs`
- 新規：`Assets/Resources/Audio/README.md`
- 新規：`Assets/Resources/Audio/{player,ui,money,mining}/`（空フォルダ）
- 変更：`Assets/_Scripts/UI/GameSettings.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 効果音は `GameAudio.Play("ui/confirm")` / `GameAudio.PlayAt("player/land", pos)` で鳴らす
- キーは `Assets/Resources/Audio/` からの相対パス。**Inspector配線は不要**
- 連番は1から詰めて置く。`key_3` が欠けると `key_4` 以降は読まれない
- **クリップが未入荷のキーは黙って無視する。**音が無くてもゲームは壊れない
- AudioMixerは使わない。Masterは既存の `AudioListener.volume`、効果音は `GameSettings.SfxVolume` を再生側で掛ける（AGENTS.mdの「軽さ・分かりやすさ優先」に合わせた）
- AudioSourceは12個を使い回す。1音ごとにGameObjectを作らない
- `Assets/_Audio/Mining/` の既存4ファイルは `RoomiesMiningBuilder.Tone()` が生成したサイン波＋ノイズのプレースホルダ。第2段階で差し替える

### 影響範囲
- 効果音の再生基盤（新規。既存挙動への影響なし）
- `GameSettings`（音量項目の追加のみ。既存の Master 音量の挙動は変更していない）
- まだどの呼び出し元にもフックしていないため、ゲームの見た目・音は現状変わらない

### 確認状況
- **C#コンパイル：未確認**（Unity自体は起動中。Unity MCPが `Connection revoked` で未承認、かつUnitySkills RESTサーバーが未起動のため接続できない）
- Audiostockの契約確認：確認済み（定額制エンタープライズプラン・無料トライアル、2026/10/01〜2027/03/31、`tk230088` でログイン）
- 定額制なのでダウンロードに追加課金が無いこと：確認済み（購入作品0件）
- 候補の作品番号・尺・作者：確認済み（検索結果から取得）
- 音声ファイルのダウンロード：**未実施**
- 各候補の実際の音：**未試聴**（私は音を聞けないため、採用判断はユーザー側）
- 呼び出し元へのフック：未実施

### 未完了・次の作業
- 候補を試聴して採用を決める（ユーザー）
- 採用分をDLし `Resources/Audio/` 配下へ規則どおりの名前で配置する
- 呼び出し元へフックする：`PlayerMovement`（足音・ジャンプ・着地）、`UIButtonJuice`／`EscMenuUI`（決定・キャンセル）、`SharedMoneyManager.SharedMoneyChanged`（入金・出金）
- 第2段階として採掘4音を差し替え、`MiningSite` の `AudioClip` 参照を `GameAudio` 経由へ寄せるか検討
- Unity側で接続を復帰させてからコンパイル確認とPlayでの音量バランス調整
  - Unity MCP：`Project Settings > AI > Unity MCP` で接続を承認する
  - UnitySkills REST：`Window > UnitySkills` でサーバーを起動する（自動起動の設定も可）
- **ライセンス確認：**定額制プランの利用規約で、Steam配布のゲームへの同梱と、契約終了後の継続利用が認められるかを確認する（無料トライアル中の取得ぶんの扱いも）

## 2026/09/24｜main・採掘・アニメーションの統合

### 今回の変更
- main（523d7be）、codex/astra-debug-20260905（b0d4b00）、feat/add-animation-test（cfb5f2d）を統合。
- 採掘・街並み・家具/日付処理とHumanoid/エモート/ジャンプを併存。
- 非OwnerにもAnimatorを初期化し、未定義IsSprintingパラメーターの書き込みを削除。
- メニュー/操作ロック/Despawn時のエモートホイールを閉じ、歩行中からしゃがみに遷移できるよう補修。
- シーン/Prefab/採掘構成とアニメーション統合部分のEditorテストを追加。

### 確認状況
- Gitのブランチ統合と静的参照検査：実施。対象のGUID重複・ローカル参照切れなし。
- Unityバッチ検証：開始後、ユーザーが実機確認を担当する指示により中止。コンパイル/テスト/Host・Client動作の完了は未確認。
- 既存のリモート歩行・しゃがみアニメーション同期の制限は今回の統合では変更しない。

### 作業保全
- 元の未コミット変更は別途バックアップし、統合後も未コミット状態で保持する。
- 復元用ブランチ：backup/pre-merge-20260924。

## 2026/09/06｜地下採掘を既存ジョブ・運搬・経済へ統合

- 既存Progress/TODO、操作・運搬・重量・資金・Day・Scene/Prefab・NGO構成を確認して着手。
- 家の東に入口と買取機、GameRoom内に地下3層・左右採掘室・階層リフト・線路と留置トロッコを配置。既存の家→牢屋→配送区画を保持。
- ツルハシ、18鉱脈、鉱石/ガラクタ11種、現物ドロップ、既存F運搬/投げる/重量、地上帰還、共同口座への売却を実装。
- 昼の残り時間を閉鎖時計に使用。日没時の手持ち鉱石没収と帰還、翌朝の鉱脈復旧、深層の停電/ガス/落盤を追加。
- 巨大金鉱・鑑定価格の壺・レア演出、既存HUD内の時間/重量/価格/売却通知を追加。
- 岩HP・生成・換金・イベントはサーバーで確定。既存DefaultNetworkPrefabsへ登録。運搬の同時保持と投げた直後の別人取得時のCollider復元も補強。
- コンパイルとEditorの参照/物理検査を実施。採掘の回帰テスト5件を追加。実行テスト・Host/Client通しプレイはUnitySkillsのAllowlist待ちで未検証。
- 走行トロッコ、爆薬、ツルハシ強化、2人運搬は未実装。詳細・操作・価格・検証の区別は `ROOMIES_MINING_2026-09-06.md`。

## 2026/09/05｜スロット距離判定・UI・牢屋の配置修正

- スロットのネットワーク親原点が筐体から約4940m離れていたため、ServerのBET距離判定を筐体Colliderへの最短距離に変更。Prefabで参照を明示し、見た目とCollider寸法も整合。
- スロット盤面を角丸の3リール、BET、結果、操作案内で構成し直し、筐体前面へ配置。再テーマ適用時にリールが背景に隠れない描画順へ修正。
- HUDを左上の時間、右上の共同口座・家賃進捗、左下の現在目標に整理。共通UIを落ち着いた青緑系へ変更。時計の折り返し・口座ラベルの重なりを解消。
- 家の隣に格子・屋根・寝台・作業台つきの牢屋を建設。JailPointを内部へ配置し既存の逮捕・労働・釈放処理へ接続。
- DeliveryZoneはx=-3.8へ移し、ボタン・看板・区画線・干渉する植栽も移動。「家→牢屋→デリバリー」の配置に分離。
- Unityコンパイル: エラー0、警告0。スロット3台の近距離許可・遠距離/NaN拒否、牢屋と納品判定の分離、牢屋出現位置のCapsule重なりなしを検査済み。実Play ModeのBET・逮捕から釈放までの通し操作は未検証。
- 再適用: `Roomies/Polish/Repair Slot and Build Jail`。HUDのEditor確認画像は `Roomies/Polish/Render HUD Review`。保存画像と検査結果は `ArtReview/2026-09-05/`。

## 2026/09/05｜背景・家具・NPC外観の制作

- GameRoomに家の内装・屋根・階段、店舗外観、街路、植栽、遠景を配置。背景は `RoomiesNeighborhood.prefab` として保存。
- 購入家具12種と小物・設備17種をメッシュPrefab化し、購入カタログへ接続。既存NPC3種はBobモデルを再利用し、スロット外装も置換。
- 朝夜に連動する太陽光を追加し、家の出現位置を5地点に調整。
- Unityコンパイル: エラー0・警告0。家具12種、実際のCharacterController寸法による出現位置5地点、NPC3種のアセット検査に合格。背景のMissing Script 0。
- Play Mode・マルチPC・性能検証は未実施。制作範囲・再生成方法・未検証事項は `ROOMIES_ART_2026-09-05.md`、確認画像は `ArtReview/2026-09-05/`。

## 2026/09/05｜Astra デバッグ：開始・購入・配送の安定化

### 今回の変更
- Progress／TODO／各セットアップと現在の実装・Scene・Prefab設定を照合。詳細は `ROOMIES_REVIEW_2026-09-05.md`。
- ロビー開始の多重起動、開始待ち中の退出、退出後の遅延入室、開始済みロビー参加、Host離脱処理を修正。
- NGOのShutdown完了を待ってメニューへ戻すよう変更。開始・失敗メッセージと開始ボタンのBusy判定を修正。
- 家具購入をServerで検証し、商品indexだけをClientから受信。配達地点と全注文共通の配置順をServerで決定。
- 家具は生成成功後だけ決済。朝・夜終了演出中・Game Over・不正Prefab等では引き落とさない。
- DayManagerのネットワーク購読と初期状態反映を修正。遷移中フラグを同期。
- 配送箱の初期生成をOnNetworkSpawnへ移し、Untaggedの子Colliderと複数Colliderの出入りを修正。
- Serverロジックの回帰テスト8件を追加。家具TODOの古い状態を明示し、現行手順を更新。

### 確認状況
- Unityコンパイル：確認済み（エラー0・警告0）。
- Unity Test Runnerの検出：追加した8件すべてRunnableを確認（実行成功とは別）。
- `_Scenes`／`_Prefabs` の直接のMonoScript GUID参照：未解決0件。
- 回帰テスト実行：未確認。UnitySkills Autoモードの `MODE_FORBIDDEN` により、`test_run_by_name` のAllowlist登録待ち。
- Steam Host/Client実機・通しプレイ・見た目：今回未確認。
- ブランチ：`codex/astra-debug-20260905`。コミット／pushは未実施。

### 次の作業
- 回帰テスト → 2アカウントで接続・同時購入・切断・家賃成功／失敗を確認。
- 1〜5人仕様に対し現SceneのSteamロビー上限は4人。上限と5人時のスポーン／UIを確認する。
- 仮家具の本番化、Game Over導線、食費と共同資金、難易度上昇を既存システムで仕上げる。

## 2026/08/07｜1回目

### 今回の変更
- しゃがみキーを離した際、立ち姿勢ぶんのカプセル空間が空いているか確認する処理を追加
- 天井・家具・階段裏などで立ち上がれない間は、移動速度・カメラ・アニメーションを含めてしゃがみ状態を維持
- 頭上判定は立ち上がり要求中だけ実行し、`OverlapCapsuleNonAlloc` と再利用バッファでGC負荷を回避
- 自分自身と子オブジェクトのCollider、Triggerを判定対象から除外

### 変更ファイル
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 現在のCharacterController下端を固定した仮の立ち姿勢カプセルで空間を確認する
- 判定カプセルは`skinWidth`ぶん内側へ縮め、床との接触を天井障害として誤検出しない
- 立ち上がり途中に空間が塞がった場合も、次フレームからしゃがみ姿勢へ戻す
- 判定対象レイヤーは`stanceCollisionMask`で調整可能（初期値は全レイヤー）

### 確認状況
- 変更差分：確認済み
- `Assembly-CSharp.csproj`代替コンパイル：成功（警告0・エラー0）
- Unity MCP経由のConsole確認：未確認（現在のCodexセッションにUnity MCPツールが公開されていないため）
- 低い天井・家具の下での実プレイ確認：未確認

### 未完了・次の作業
- Unity上で低い天井の下からしゃがみキーを離し、姿勢が維持されることを確認
- 頭上空間が空いた直後に自然に立ち上がることを確認
- Host/Clientそれぞれで姿勢・移動速度・アニメーション表示を確認

## 2026/08/05｜1回目

### 今回の変更
- 警察の追跡中に、対象プレイヤーだけへ赤い画面端の脈動を表示
- 画面上部へ「警察に追われている！」と逃走案内を常時表示
- 複数警察の追跡状態をServer側で軽量に管理し、全員から逃げ切るまで表示を維持
- 逃走成功、紙袋消失、朝、逮捕、警察消滅時に追跡表示を自動解除

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingPolicePatrol.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingPlayer.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingArrestOverlay.cs`
- 変更：`Assets/_Scripts/Smuggling/SETUP.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- 追跡表示は対象プレイヤーのOwnerだけに表示する
- 追跡開始・終了時だけRPCを送り、毎フレーム通信しない
- 既存の発見バナー、逃走成功バナー、逮捕演出は維持する
- 追加のPhysics判定、全体検索、Scene配置、Prefab登録は不要

### 影響範囲
- 闇バイトの警察AI
- 追跡中HUD
- 逮捕・逃走演出
- Host/ClientのOwner向け表示同期

### 確認状況
- 変更差分：確認済み
- コンパイル：確認済み（変更3スクリプトすべてエラー0）
- Console：確認済み（エラー0・警告0）
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- Host/Clientで追跡開始・逃走・逮捕時の表示切り替えを実プレイ確認
- 実解像度で上部表示のサイズと視認性を確認

## 2026/08/03｜5回目

### 今回の変更
- Roomiesのマルチプレイがフレンド招待中心であることをAI開発ルールへ明記
- 重いアンチチートより軽さ・反応・遊びやすさを優先する方針を追加
- 維持する基本検証と、避ける過剰検証の境界を明文化

### 変更ファイル
- 新規：`AGENTS.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- 野良プレイヤーとの公開マッチングは現時点で想定しない
- 低負荷なNull・範囲・所有者確認と、共有状態のServer権限は維持する
- 高頻度RPC、毎フレーム距離検証、重複Physics判定、過剰ログは避ける
- 通常プレイに影響しない悪意ある操作だけの対策は過剰実装しない

### 影響範囲
- 今後のマルチプレイ実装
- Server検証
- グリッチ対策
- 通信量と実行時パフォーマンス
- AIによるコードレビューと実装判断

### 確認状況
- ルール記載：確認済み
- 既存コード変更：なし
- コンパイル：対象外
- Host/Client動作：対象外

### 未完了・次の作業
- 今後の機能追加時に本ルールへ沿って検証コストを判断する

## 2026/08/03｜4回目

### 今回の変更
- スロットのServer側距離拒否を削除し、通常操作でBETが誤拒否される問題を修正
- デバッグ用Nキーのターン送りを通常ビルドでも一時的に再有効化
- Nキー機能を初期状態で有効化

### 変更ファイル
- 変更：`Assets/_Scripts/Slot/SlotMachine.cs`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- スロットは距離では拒否しない
- スロットのPlayer所有者検証、BET値検証、共同口座の支払い判定は維持する
- Nキーのターン送りはHostのみ実行できる
- Nキー機能は製品版確定前に無効化または削除する

### 影響範囲
- スロット
- 朝夜ターン進行
- Hostデバッグ操作
- マルチプレイのServer検証

### 確認状況
- 変更差分：確認済み
- コンパイル：確認済み（`SlotMachine.cs`・`DayManager.cs`ともエラー0）
- Console：確認済み（エラー0・警告0）
- `pCube4`警告：Domain Reload後も再発なし
- Host動作：修正後未確認
- Client動作：修正後未確認

### 未完了・次の作業
- HostでスロットBETとNキーを実プレイ確認
- ClientからスロットBETできることを確認
- 製品版前にNキーデバッグ機能を削除

## 2026/08/03｜3回目

### 今回の変更
- `pCube4`の巨大三角形警告が無効化後もScene読み込み時に出る問題を根本修正
- メインメニュー背景専用`pCube4`から不要なMeshColliderを削除

### 変更ファイル
- 変更：`Assets/_Scenes/MainMenuSteam.unity`
- 変更：`PROGRESS.md`

### 重要な仕様
- `pCube4`のMeshRenderer、MeshFilter、Transform、Groundタグは維持する
- MainMenuSteamにはRigidbodyと`pCube4`を参照するスクリプトがなく、物理判定は使用しない
- GameRoomの地面・Colliderには変更を加えない

### 影響範囲
- メインメニュー背景Sceneの読み込み
- Unity EditorのConsole警告

### 確認状況
- 修正対象Sceneの未ロード・未Dirty状態：確認済み
- Scene YAML参照整合性：確認済み
- MainMenuSteam再インポート：確認済み
- コンパイル：確認済み（エラー0）
- Console：確認済み（エラー0・警告0）
- `pCube4`警告：再発なし

### 未完了・次の作業
- 実ゲーム起動時のMainMenuSteam表示を最終確認

## 2026/08/03｜2回目

### 今回の変更
- メインメニュー背景の巨大三角形MeshCollider警告を解消
- メインメニューの未収録文字「▶」を外し、TMPの文字欠け警告を解消
- Player Prefab内の重複した`PlayerNameDisplay`を削除し、Netcodeスポーン警告を解消

### 変更ファイル
- 変更：`Assets/_Scenes/MainMenuSteam.unity`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Prefabs/Player.prefab`
- 変更：`PROGRESS.md`

### 重要な仕様
- 名前表示Canvasは非表示のまま維持する
- プレイヤー名とBobBody色の同期を担当するルート側`PlayerNameDisplay`は維持する
- `pCube4`はメインメニュー背景の表示を維持し、不要なMeshColliderのみ無効化する

### 影響範囲
- メインメニュー背景
- メインメニューのPLAYボタン
- プレイヤー名・キャラクター色のマルチプレイ同期

### 確認状況
- 修正前Host動作：ユーザー確認済み
- 変更差分：確認済み
- コンパイル：確認済み（エラー0）
- Console：確認済み（エラー0・警告0）
- Missing Script：確認済み（Scene・Prefabとも0件）
- Host動作：修正後未確認
- Client動作：修正後未確認

### 未完了・次の作業
- Host/Clientでキャラクター色同期と警告解消を確認

## 2026/07/30｜20回目

### 今回の変更
- 持ち運び、ドア、納品、スロット、ブラックジャックのServer側RPC検証を強化
- 持ち運びの同時取得競合と、所持中Client切断後に箱が残る問題を修正
- Clientから日付を進められる処理と、製品版で動くデバッグキーを修正
- 家具ショップのInspector設定とServer側価格・効果が一致しない問題を修正
- 配送箱の子Collider判定、破棄済み参照、スポーン位置調整を改善
- クローゼットをEscapeで閉じた直後にESCメニューが開く競合を修正
- 納品成功・失敗を既存のゲーム内バナーで分かりやすく表示
- Unity 6.4の非推奨API警告を解消
- 全体レビュー結果と次回候補を詳細レポートへ記録

### 変更ファイル
- 変更：`Assets/_Scripts/CarryableObject.cs`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scripts/DoorInteract.cs`
- 変更：`Assets/_Scripts/SharedMoneyManager.cs`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scenes/GameRoom.unity`
- 変更：`Assets/_Scripts/Furniture/FurnitureCatalog.cs`
- 変更：`Assets/_Scripts/Furniture/FurnitureEditController.cs`
- 変更：`Assets/_Scripts/Delivery/DeliveryButton.cs`
- 変更：`Assets/_Scripts/Delivery/DeliveryZone.cs`
- 変更：`Assets/_Scripts/Slot/SlotMachine.cs`
- 変更：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/PlayerEarningListUI.cs`
- 変更：`Assets/_Scripts/Night/NightEventManager.cs`
- 新規：`ROOMIES_NIGHTLY_REVIEW_2026-07-30.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- 日付進行、所持状態、施設利用、報酬反映の最終判定はServer側で行う
- 朝・夜は各180秒を初期値とし、`DayManager`のInspectorから変更可能
- 家具カタログはScene上の`FurnitureEditController`の設定をServerと設置家具で共有する
- 納品演出は`Show Result Banner`で無効化可能
- 納品成功の全員通知は`Announce Success To Everyone`で切り替え可能
- 配送箱の散らばりと高さは`DeliveryZone`のInspectorから変更可能

### 影響範囲
- マルチプレイ同期とRPC検証
- 持ち運び
- 日付・朝夜進行
- 共同口座
- 家具ショップと設置家具
- デリバリーバイト
- スロット、ブラックジャック、ドア
- クローゼットとESCメニュー

### 確認状況
- コンパイル：確認済み（エラー0・警告0）
- BootScene開始：確認済み
- ロゴ後のMainMenuSteam遷移：確認済み
- Play停止：確認済み
- Missing Script：確認済み（Scene・Prefabとも0件）
- Build Settings：確認済み（BootScene 0 / MainMenuSteam 1 / GameRoom 2）
- Host動作：未確認
- Client動作：未確認
- Steamロビー・再接続：未確認
- 実ビルド：未確認

### 未完了・次の作業
- Host/Clientで同じ箱を同時に拾う競合テスト
- 箱を所持したClientの切断テスト
- 朝夜、家賃、DAY 4以降の通しプレイ
- Steam終了・離脱・再接続の実ビルド確認
- 実行時生成UIのPrefab化
- `DayManager`の残り時間同期頻度を削減

## 2026/07/30｜19回目

### 今回の変更
- 起動ロゴ演出を約2.8秒から約4.6秒へ延長
- ロゴとStudio表記を見せる保持時間、光沢演出、フェードアウトのタイミングを調整
- Unity EditorでどのSceneを開いていても、Play時は製品版と同じ`BootScene`から開始するように設定
- Build Settingsの先頭が`BootScene`になっていることを再確認

### 変更ファイル
- 変更：`Assets/_Scripts/UI/LNStudioSplash.cs`
- 変更：`Assets/_Scripts/Editor/BootSceneBuilder.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ビルドのScene Index 0は`Assets/_Scenes/BootScene.unity`
- 起動順は`BootScene` → `MainMenuSteam`
- EditorのPlay開始Sceneも`BootScene`へ固定
- クリック、Space、Enter、Escape、ゲームパッドによるスキップ機能は維持

### 影響範囲
- ゲーム起動時のロゴ演出
- EditorでのPlay開始Scene
- ビルドScene順

### 確認状況
- コンパイル：確認済み（エラー0）
- Build Settings：確認済み（`BootScene`がIndex 0）
- Unity Play開始直後：確認済み（`BootScene`）
- ロゴ後のScene遷移：確認済み（`MainMenuSteam`）
- Play停止：確認済み

### 未完了・次の作業
- 実ビルドしたexeでの起動表示は未確認

## 2026/07/30｜18回目

### 今回の変更
- 起動ロゴ「LN Studio」を、サンセリフから明朝相当のセリフ体へ変更
- ロゴ用のTMPフォントアセットを生成する仕組みを追加（`Roomies/Build Splash Logo Fonts`）
- セリフ体向けにロゴの組みを調整：擬似ボールドを外してRegularのまま使用
- `LN`の字間を-4から+14へ広げ、サイズを210→200へ
- `Studio`の字間を18から30へ広げ、サイズを44→38へ、擬似ボールドを解除
- `LN`と`Studio`の間に細い罫線（1px）を追加し、`Studio`と一緒に中央から横へ開く演出にした
- セリフの細い線が潰れないよう、ボケ→シャープの滲み量を弱めた（alpha 0.90/0.55→0.70/0.40、scale 1.055/1.115→1.035/1.075）
- 光沢の強さを0.55→0.42へ弱めた
- ロゴ本体の色を`#D7DCE6`から`#E4E7EE`へ上げ、高コントラストなセリフが細く見えすぎないようにした

### 変更ファイル
- 新規：`Assets/_Scripts/Editor/SplashFontBuilder.cs`
- 新規：`Assets/_Fonts/TimesNewRoman.ttf`
- 新規：`Assets/_Fonts/PalatinoLinotype.ttf`
- 新規：`Assets/Resources/Fonts/LogoSerif SDF.asset`
- 新規：`Assets/Resources/Fonts/LogoSerifAlt SDF.asset`
- 変更：`Assets/_Scripts/UI/LNStudioSplash.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ロゴのフォントは`Resources/Fonts/LogoSerif SDF`（Times New Roman由来）
- 差し替え候補として`LogoSerifAlt SDF`（Palatino Linotype由来、より柔らかい印象）も生成済み。`LNStudioSplash.FontResourcePath`の1行変更で切り替わる
- フォントアセットはダイナミック（`m_AtlasPopulationMode: 1`）。ロゴで使う文字は"LNStudio"の8文字だけなのでアトラスの事前ベイクは不要
- セリフ体には擬似ボールドをかけない。細い線が潰れて明朝らしさが失われるため
- セリフ体は太らせず字間を開けて見せる（`LN`は+14、`Studio`は+30）
- `SplashFontBuilder`は冪等。出力先に`.asset`が既にあれば何もしない
- **ライセンス注意：現在の元TTFはWindowsのシステムフォント（Monotype / Linotype）で、ゲームへ同梱して配布する権利は無い。** 製品ビルド前にOFLのフォント（Shippori Mincho / Playfair Display / Cormorant など）へ差し替える必要がある。差し替えは`Assets/_Fonts`へ.ttfを置き`SplashFontBuilder.SourceFonts`へ追記するだけ
- ダイナミックフォントアセットは元TTFを参照するため、TTFもビルドに含まれる（上のライセンス注意はビルド成果物にも及ぶ）

### 影響範囲
- 起動ロゴ演出の見た目のみ
- 他のUI・ゲーム機能には影響しない（フォントはロゴ専用で、既存UIは`MPLUSU`のまま）

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity Console：確認済み（エラー0件）
- TTFのインポート：確認済み（`.meta`生成済み）
- TMPフォントアセット2種の生成：確認済み
- フォントアセットの元TTF参照GUID一致：確認済み（Times / Palatino ともに一致）
- ダイナミックモード（`m_AtlasPopulationMode: 1`）：確認済み
- **Unity Playでの実表示：未確認**（Play Mode開始はUnity Skillsの権限区分で現在のautoモードから実行できないため）
- セリフ体の字面・字間・罫線幅の体感：未確認
- 200pxでのSDF品質（サンプリング90pxからの拡大）：未確認

### 未完了・次の作業
- Unity PlayでBootSceneを再生し、セリフ体の印象と字間・罫線のバランスを確認する
- Times（`LogoSerif SDF`）とPalatino（`LogoSerifAlt SDF`）を見比べてどちらを採用するか決める
- 200px表示でSDFのエッジが甘い場合、`SplashFontBuilder`のサンプリングサイズを上げて再生成する
- 製品ビルド前にOFLフォントへ差し替える（ライセンス対応）
- 12回目で変更したプレイ中UIの実表示確認（前回からの持ち越し）

## 2026/07/30｜17回目

### 今回の変更
- 起動時のスタジオロゴ演出「LN Studio」を追加
- 専用の`BootScene`を新規作成し、Build SettingsのIndex 0へ登録（`MainMenuSteam`は1、`GameRoom`は2へ繰り下げ）
- ロゴを大きな`LN`＋その下に小さな`Studio`の中央揃えロックアップで構成
- フォントに既存の`MPLUSU-Black SDF`（M PLUS、モダンな幾何学サンセリフ）を使用
- 背景を中央`#17191F`／外側`#08090C`の弱いラジアルグラデーションにした
- 0.4秒の黒画面 → `LN`フェードイン（Scale 95%→100%、ボケ→シャープ）→ `Studio`が遅れて出現 → 約1秒保持 → フェードアウト（Scale→102.5%）→ `MainMenuSteam`へ遷移、の2.8秒の演出を実装
- 保持中の控えめな演出として、`LN`の表面を細い光が1回だけ横切る演出とロゴの1%拡大を追加
- クリック・Space・Enter・Escape・ゲームパッド(South/Start)でスキップできるようにした
- 保持中に遷移先シーンを裏で非同期読み込みし、演出の最後で切り替えるようにした
- `UITheme`にラジアルグラデーション生成`RadialGradient`を追加

### 変更ファイル
- 新規：`Assets/_Scenes/BootScene.unity`
- 新規：`Assets/_Scripts/UI/LNStudioSplash.cs`
- 新規：`Assets/_Scripts/Editor/BootSceneBuilder.cs`
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`ProjectSettings/EditorBuildSettings.asset`
- 変更：`PROGRESS.md`

### 重要な仕様
- Build Index 0は`BootScene`。ゲーム起動時はここから始まる
- `BootScene`の中身は`Boot Camera`と`LNStudioSplash`の2つだけ。Canvas以下は`LNStudioSplash`がランタイム生成する（既存UIと同じ方針）
- `Boot Camera`は`cullingMask=0`の黒クリア専用。UIは`ScreenSpaceOverlay`なので描画には使わず、「No cameras rendering」警告を消すためだけに置く
- 演出はDOTweenやTimelineではなくコルーチン1本で持つ。DOTweenは未導入で、既存演出（`DayTransitionUI`等）も同じ手法のため合わせた
- 演出の時間はすべて`Time.unscaledDeltaTime`で進める
- 毎フレーム経過時間から全チャンネルを算出する方式にしている。`LN`（0.4〜1.0秒）と`Studio`（0.8〜1.2秒）の表示区間が重なるため、逐次実行のコルーチンでは表現できない
- ボケ→シャープはシェーダーではなく、`LN`の複製2枚を大きめ・薄めに重ねて中盤だけ滲ませる方式。マテリアルを触らないのでフォントアセットを壊さず、マテリアルインスタンスも増えない
- 光沢は`RectMask2D`（softness 52）の細いマスクを横へ動かし、中の`LN`複製を逆方向へオフセットして文字を止めて見せる
- `BootSceneBuilder`は冪等。シーンが既にあり、Index 0に登録済みなら何もしない
- 既存シーンから`LNStudioSplash`が失われていた場合は`BootSceneBuilder`が付け直す
- 手動実行は`Roomies/Build Boot Scene`から可能
- 遷移先がBuild Settingsに無い場合はエラーログを出して遷移しない（黒画面で止まらないよう`SceneExists`で事前確認する）
- プロジェクト内のシーン遷移はすべてシーン名指定なので、Index繰り下げの影響を受けない

### 影響範囲
- ゲーム起動フロー（起動 → ロゴ → メインメニュー）
- Build Settingsのシーン順
- `UITheme`（グラデーション生成の追加のみ。既存の見た目には影響しない）

### 確認状況
- C#コンパイル：確認済み（エラー0、既存警告19件）
- Unity Console：確認済み（エラー0件）
- `BootScene.unity`の生成：確認済み
- `BootScene`の内容（`Boot Camera` ＋ `LNStudioSplash`）：確認済み
- `nextSceneName: MainMenuSteam`のシリアライズ：確認済み
- Build SettingsのIndex 0が`BootScene`：確認済み
- シーン遷移がすべて名前指定であること：確認済み（grep）
- **Unity Playでの演出の実表示：未確認**（Play Mode開始はUnity Skillsの権限区分で現在のautoモードから実行できないため）
- ロゴの字面バランス・タイミングの体感：未確認
- ビルド実機での起動：未確認

### 未完了・次の作業
- Unity PlayでBootSceneを再生し、2.8秒の流れと`MainMenuSteam`への遷移を確認する
- `LN`と`Studio`の横幅バランス、字間、光沢の強さを実画面で最終調整する
- ボケ→シャープの滲み量（複製2枚のalphaとscale）が狙いどおりか確認する
- スキップ操作4種（クリック／Space／Enter／Escape）を確認する
- 12回目で変更したプレイ中UIの実表示確認（前回からの持ち越し）

## 2026/07/30｜16回目

### 今回の変更
- 15回目で追加した施設のプレイ中移動機能を撤回
- クローゼットを固定施設Prefabとして作成し、GameRoomへ実体配置
- UnityのSceneビューとHierarchyから位置・回転・サイズを直接調整可能に変更
- 見た目差し替え用の`Appearance_REPLACE_ME`子オブジェクトを追加

### 変更ファイル
- 新規：`Assets/_Prefabs/Facilities/CharacterCloset.prefab`
- 新規：`Assets/_Materials/Facilities/ClosetBody.mat`
- 新規：`Assets/_Materials/Facilities/ClosetDoor.mat`
- 新規：`Assets/_Materials/Facilities/ClosetHandle.mat`
- 変更：`Assets/_Scenes/GameRoom.unity`
- 変更：`PROGRESS.md`

### 重要な仕様
- クローゼットはプレイヤーが持ち運ぶ物ではなく、Sceneに固定配置する施設
- 位置調整はGameRoom内の`CharacterCloset`のTransformで行う
- 見た目を交換する場合は、機能を持つPrefabルートを残して`Appearance_REPLACE_ME`だけをFBXまたは別Prefabへ置き換える
- `CharacterCloset`コンポーネントとColliderを残せばカラー変更機能は維持される

### 影響範囲
- クローゼット
- GameRoomの施設配置
- キャラクターカラー変更

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Prefab作成・Scene保存：確認済み
- GameRoom上のPrefab接続：確認済み
- Play時のクローゼット重複：なし（1個のみ）
- Host/Client実プレイ：未確認

### 未完了・次の作業
- 本番FBXへ差し替え後、Collider範囲を調整

## 2026/07/30｜15回目

### 今回の変更
- 追加設備を直接持って位置調整できる共通システムを追加
- クローゼット、ブラックジャック台、スロット台をFで移動・設置、Rで45度回転できるように変更
- 設備の位置・向き・保持者をHostから全Clientへ同期
- プレイヤー離脱時に設備の保持状態が残らない復旧処理を追加

### 変更ファイル
- 新規：`Assets/_Scripts/Furniture/MovableRoomObject.cs`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 変更：`Assets/_Scripts/Slot/SlotMachine.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 操作は既存家具と統一し、Fで持つ・置く、Rで45度回転する
- 設備ごとのNetworkPrefab追加は不要で、`DayManager`の共有リストが位置と保持状態を同期する
- 今後追加する設備もColliderと`MovableRoomObject`を付ければ同じ方式で移動できる
- 配置位置の距離制限と操作権限はServer側で検証する

### 影響範囲
- クローゼット
- ブラックジャック
- スロット
- プレイヤーのインタラクト表示
- マルチプレイ同期

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity AssetDatabase反映：確認済み
- 操作案内の競合防止：確認済み
- Host/Client実プレイ：未確認

### 未完了・次の作業
- Host/Clientで設備の移動と同時表示を実プレイ確認

## 2026/07/30｜14回目

### 今回の変更
- ゲーム本編の朝を鮮やかな青空と白い雲のスカイドームへ変更
- 夜を月明かりが残る深い青のスカイドームへ変更
- 朝夜ごとに環境光と反射の強さを切り替える処理を追加
- GameRoomの初期Skyboxも朝空へ統一し、開始直後の表示差を解消

### 変更ファイル
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scenes/GameRoom.unity`
- 変更：`PROGRESS.md`

### 重要な仕様
- 朝は`Epic_BlueSunset`、夜は`Cold Night`を使用する
- 夜の環境光は暗くしつつ、移動や警察回避に必要な視認性を残す
- Skybox参照と明るさは`DayManager`のInspectorから交換・調整できる

### 影響範囲
- ゲーム本編の朝夜背景
- 環境光
- 環境反射

### 確認状況
- C#コンパイル：確認済み（エラー0）
- GameRoomのInspector参照：確認済み
- 朝夜Skybox素材の画像確認：確認済み
- PlayerカメラのSkybox描画設定：確認済み
- Host/Client実プレイ：未確認

### 未完了・次の作業
- Host/Clientで朝夜切り替え時の最終的な明るさを確認

## 2026/07/30｜13回目

### 今回の変更
- メインメニューの背景を青・ピンク系の夕焼けスカイドームへ変更
- Skyboxの色を環境光と反射へ反映
- メニューカメラをSkybox描画へ変更

### 変更ファイル
- 変更：`Assets/_Scripts/UI/MainMenuRoomShowcase.cs`
- 変更：`Assets/_Scripts/Editor/MainMenuShowcaseBuilder.cs`
- 変更：`Assets/_Scenes/MainMenuSteam.unity`
- 変更：`PROGRESS.md`

### 重要な仕様
- メインメニューは`Epic_GloriousPink`のSkyboxを使用する
- ゲーム本編の朝夜Skybox設定には影響しない
- Skybox素材はInspector参照として`MainMenuSteam`へ保存する

### 影響範囲
- メインメニュー背景
- メニューの環境光・反射

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity Play表示：確認済み
- Skybox表示：確認済み

### 未完了・次の作業
- なし

## 2026/07/30｜12回目

### 今回の変更
- プレイ中UIをメインメニューと同じデザイン言語（濃色の面＋太い白フチ＋厚みピル）へ統一
- HUDのDAYカードと共同口座カードから白いカードを廃止
- DAYカードとタイマーを1枚に統合し、文字を大きくした（DAY 34px / 残り時間 40px / 残高 46px）
- 残り時間バーを5pxの罫線から16pxの太いバーへ変更し、残り20%で赤へ寄せるようにした
- 共同口座の表示を「残高が主役、必要額は添え物」へ変更し、不足時は残高を赤にして不足額を表示
- 家賃の期限をチップ化し、残り2日で橙、残り1日以下で赤く鼓動するようにした
- 共同口座カードの1px罫線（Divider）を削除
- 共同口座の増減表示を厚みのあるピルへ変更
- インタラクト表示を白いチップから濃色の厚みチップへ変更し、文字を22px→26pxへ拡大
- 全画面演出3種（朝・収支・家賃）の背景を、バラバラなベタ塗りから共通の「濃紫の遮光＋演出色のグラデ」へ統一
- 収支ランキングの行を、タブ揃えの表からメダルの丸＋名前＋金額の名札へ変更
- 家賃支払い画面の白い巨大カード1120x650を濃色パネル940x560へ縮小し、請求額を76pxの主役にした
- 夜イベントバナーを白いトースト通知から濃色の厚みバナー＋色付き丸バッジへ変更
- 夜イベントの種類色を暗い色から明るい色（シアン／赤／ピンク／ライム）へ変更し、タイトルは常に白へ
- TAB長押しの稼ぎランキングを濃色パネル＋丸メダルの名札行へ変更
- ESCメニューの白カードを濃色パネルへ変更し、全ボタンを厚みピルへ変更
- ESCメニュー左上の意味のない5pxアクセントバーを削除
- キャラクターカラー画面を濃色パネル＋厚みピルへ変更

### 変更ファイル
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/DayTransitionUI.cs`
- 変更：`Assets/_Scripts/DayResultUI.cs`
- 変更：`Assets/_Scripts/RentPaymentUI.cs`
- 変更：`Assets/_Scripts/Night/NightEventUI.cs`
- 変更：`Assets/_Scripts/PlayerEarningListUI.cs`
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- プレイ中UIの面は`UITheme.Surface`（濃色＋太い白フチ＋影）で作る。白いカードは使わない
- 小さな情報表示は`UITheme.Chip`（カプセル型の面）で作る
- 全画面演出の背景は`UITheme.StageBackdrop`で統一する。ベタ塗りの板は使わない
- `DayTransitionUI`だけは`BlackoutDelay`の間にプレイヤーをリスポーンさせるため、`StageBackdrop`のopacityを1（完全遮光）にする。他の演出は0.9で部屋をうっすら残す
- 濃色パネルの上の文字は白（`Ink`）と白66%（`InkSoft`）を使い、`UITheme.TextMain`／`TextSub`は使わない
- ボタンは全画面で`UITheme.StylePill`を使い、押した時の沈み込みとバネの手触りを統一する
- 夜イベントの種類は「下端の太いキャップ」と「丸バッジ」の色で示し、タイトルの色は変えない
- `UITheme.MenuSurface`が作る内側の面`Fill`と`StylePill`が作る天面`Cap`には`LayoutElement.ignoreLayout`が付く。親にLayoutGroupがあっても全面へ張るため
- 記号（✓など）は日本語フォントに無い場合があるので使わず、言葉で示す
- プレイヤー一覧の再生成条件、家賃・共同口座の計算、ネットワーク処理は変更しない

### 影響範囲
- ゲーム中HUD（日数・朝夜・残り時間・共同口座・家賃期限）
- インタラクト表示
- 朝の日付切り替え演出
- 夜終わりの収支発表
- 家賃支払い演出
- 夜イベント通知
- TAB長押しの稼ぎランキング
- ESCメニュー・設定・キー設定・確認ダイアログ
- キャラクターカラー選択

### 確認状況
- コード全文の読み返しレビュー：実施済み
- 旧・白カードAPI（`UITheme.Card` / `StyleButton` / `TextMain` / `TextSub` / `Panel` / `PanelSoft`）の残存呼び出し：0件（grepで確認）
- C#コンパイル：**未確認**（書き込み系ツールの安全判定が断続的に停止しており、Unityへ接続できていない）
- Unity Play表示：未確認
- 各演出の実発生タイミングでの表示：未確認
- Host動作：未確認
- Client動作：未確認
- 16:9以外の解像度：未確認

### 未完了・次の作業
- Unityでコンパイルを通し、エラー0を確認する
- Unity PlayでHUDの文字サイズと、明るい部屋の上でのコントラストを確認する
- 朝の演出でリスポーンが見えないこと（完全遮光が効いていること）を確認する
- 収支発表・家賃支払い・夜イベントを実発生させて表示を確認する
- ESCメニューと設定・キー設定の全行が濃色パネル上で読めることを確認する
- `UITheme`の旧API（`Card` / `StyleButton` / 白系パレット）が未使用になったので、整理するか残すかを決める

## 2026/07/30｜11回目

### 今回の変更
- メインメニューを「3Dの部屋が主役、UIは画面の縁」という構成へ再設計
- ブランド・タイトル・タグライン・主操作・補助操作を左下ブロックへ集約し、画面上半分を3Dの部屋だけにした
- 白いカードUIを廃止し、3Dの上に乗る面を「濃い紫の面＋太い白フチ」へ変更
- 意味のない装飾（全面の半透明グラデ板`SurfaceTone`、左上の棒`SurfaceAccent`）を全画面から撤去
- フラットなボタンを、厚みのあるカプセル型ボタンへ変更（土台＋天面の2層構造）
- ボタンの反応を「ホバーで浮く／押すと天面が土台へ沈む／離すとバネで跳ね返る」へ刷新し、Z軸の傾き演出を廃止
- 表示時に下からバネで飛び込む入場演出を追加
- ロビーを白カード内の一覧から「部屋の内見」構成へ変更（コードは上中央、メンバーの名札は下端に横一列）
- 参加コード入力と設定画面を濃色パネル＋大型行へ変更
- メニューのカメラをキャラ寄りにし、注視点をキャラの左へずらしてキャラを画面右寄りに配置
- メニューのポストプロセスを専用のローカルVolumeへ分離し、ビネットを追加
- メニューのライティングを明るくし、キャラの輪郭を出すリムライトを追加

### 変更ファイル
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Scripts/UI/MainMenuSettingsUI.cs`
- 変更：`Assets/_Scripts/UI/MainMenuRoomShowcase.cs`
- 変更：`Assets/_Scripts/LobbyUIManager.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 画面の上半分にはUIを置かず、常に部屋とキャラが見える状態を保つ
- 1画面1決定。`あそぶ`を主操作とし、`部屋をつくる`・`部屋にはいる`へ段階展開する
- 3Dの上に乗る面は白いカードではなく、濃色の面＋太い白フチで表現する
- ボタンは`UITheme.StylePill`で土台（色を46%暗くした側面）と天面`Cap`の2層にする
- ラベルは自動で`Cap`の中へ移設され、押下時に文字も一緒に沈む
- 角丸半径は`UITheme.SetCornerRadius`でピクセル指定し、`pixelsPerUnitMultiplier`で9-slice境界を拡縮する
- `UITheme.AddSurfaceDetail`は装飾を生成せず、残っていれば削除する（呼び出し元12ファイルの互換のためシグネチャは維持）
- ゲーム中HUDが使う既存パレット（`Panel`・`Accent`など）と`StyleButton`は変更しない
- 初期非表示パネル内のTMPはマテリアル未生成のため、縁取りは`UITextOutline`が表示時に適用する
- プレイヤー名札はメンバー構成とアバター読込状況の署名が変化した時だけ作り直す
- メニューのポストプロセスは実行時生成プロファイルのローカルVolumeで持ち、共有VolumeProfileアセットを書き換えない
- メニューの注視点はキャラの実位置から毎回算出するため、キャラを動かしても構図が崩れない
- `MainMenuSteam.unity`は変更しない。`MainMenuManager`・`LobbyUIManager`のInspector配線を壊さないよう、RectTransformとGraphicの差し替えだけで見た目を作る
- `MainMenuRoomShowcase.CurrentBuildVersion`は据え置きのため、`MainMenuShowcaseBuilder`はシーンを再構築しない

### 影響範囲
- メインメニュー
- ホスト・参加導線
- Steamロビー
- 参加コード入力
- 設定画面
- MainMenuSteamのカメラ・ライティング・ポストプロセス
- `UITheme.AddSurfaceDetail`を呼ぶゲーム中UI全体（装飾の撤去のみ）
- `UIButtonJuice`を使う全ボタンの押下演出

### 確認状況
- コード全文の読み返しレビュー：実施済み
- C#コンパイル：**未確認**（Unity Skillsおよび書き込み系ツールの安全判定が停止中でUnityへ接続不可）
- Unity Skills接続：`/health`のみ確認済み（Unity 6000.4.7f1、mode=auto、エラー0）
- Unity Play表示：未確認
- ボタンの沈み込み・バネ演出：未確認
- ロビーの横一列名札：未確認
- 16:9以外の解像度：未確認
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- Unityでコンパイルを通し、エラー0を確認する
- Unity Playでメインメニュー・参加コード入力・設定画面の表示と見切れを確認する
- Host・Clientでロビーの名札、ルームコード、`ゲーム開始`導線を確認する
- ボタンの厚み・沈み込み量と入場演出の速さを実画面で最終調整する
- 既存不具合：`LobbyUIManager.joinErrorText`がJoinPanel内の`ErrorText`を参照しているため、ホストのロビー解散メッセージが非表示のJoinPanel内に出て見えない。メインメニュー側の`StatusText`へ向け直す

## 2026/07/30｜10回目

### 今回の変更
- メインメニューをWebサイト風のカード構成から3Dパーティーゲーム構成へ刷新
- GameRoomと同じ3Dルームを背景へ配置し、3人のRoomiesをメインビジュアル化
- 大きな`PLAY`から`HOST`・`JOIN`へ展開する操作フローを追加
- `SETTINGS`・`QUIT`を小さな補助操作へ整理
- 左側の短いコピー、読みやすさ用グラデーション、DOF、暖色・寒色ライトを追加
- キャラクターの色分け、待機中のバウンド、カメラの緩やかな揺れを追加

### 変更ファイル
- 新規：`Assets/_Scripts/UI/MainMenuRoomShowcase.cs`
- 新規：`Assets/_Scripts/UI/MainMenuSettingsUI.cs`
- 新規：`Assets/_Scripts/Editor/MainMenuShowcaseBuilder.cs`
- 変更：`Assets/_Scenes/MainMenuSteam.unity`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 3Dゲーム画面を主役にし、メニューUIは画面左側へ集約する
- 白い縦長カードと長い説明文は使用しない
- `PLAY`を主操作、`SETTINGS`・`QUIT`を補助操作として扱う
- 背景用ルームは表示専用で、ゲーム本編のManagerやNetworkObjectは追加しない
- キャラクター色は`BobBody`だけに反映する

### 影響範囲
- メインメニュー
- ホスト・参加導線
- 設定画面
- MainMenuSteamの3D背景・カメラ・ライティング

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity Skills診断：正常
- Unity Play表示：確認済み
- `PLAY`から`HOST`・`JOIN`への展開：確認済み
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- Host・Client実機で新しいメニュー導線を確認
- 本番用の家具モデル追加時にメニュー背景の構図を最終調整

## 2026/07/30｜9回目

### 今回の変更
- 全体UIを明るくポップなオリジナルパーティーゲーム調へ再デザイン
- 白・クリームを基調にピンク、紫、水色のアクセントカラーへ統一
- ボタンのHover・Selected・Pressedに拡大、傾き、沈み込み演出を追加
- メインメニューとロビーへ柔らかい背景グラデーションと装飾を追加
- ESC設定、HUD、カラー選択、通知、家賃、日付・収支演出を新テーマへ統一
- ESCメニュー表示時のゲーム画面へ軽量なソフトブラーを追加

### 変更ファイル
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 新規：`Assets/Resources/Shaders/RoomiesSoftBlur.shader`
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`Assets/_Scripts/DayTransitionUI.cs`
- 変更：`Assets/_Scripts/DayResultUI.cs`
- 変更：`Assets/_Scripts/Night/NightEventUI.cs`
- 変更：`Assets/_Scripts/RentPaymentUI.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- UIはゲーム画面を主役にし、HUDは日数・時間・共同口座へ絞る
- お金は緑、必要額はオレンジ、警告は赤、選択中はピンクで示す
- 状態は色だけでなく拡大、傾き、枠線でも判別できる
- 常設画面は明るく柔らかく、全画面演出は短い文言を大きく表示する
- 既存機能とネットワーク処理は変更しない

### 影響範囲
- メインメニュー・ロビー
- HUD・共同口座表示
- ESC・設定・キーバインド
- キャラクターカラー選択
- 日付遷移・収支・夜イベント・家賃演出

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity Skills接続：未確認（ローカルAPI応答なし）
- Unity Play表示：未確認
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- Unity Playで全画面の見切れと文字コントラストを確認
- ゲームパッドで選択移動と押下演出を確認
- Unity Skillsサーバー再起動後にUnity側コンソールを確認

## 2026/07/30｜8回目

### 今回の変更
- 全体UIをPC・コンソール向けパーティーゲーム調へ刷新
- 切り欠きパネル、控えめな階調、意味色による共通テーマを追加
- メインメニュー、HUD、ESC、クローゼット、ロビー、夜イベント、ブラックジャックへ反映
- マウス・キーボード・コントローラーの選択状態を視覚化
- ESCメニューを左寄せ半透明表示にしてゲーム画面の視認性を改善

### 変更ファイル
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`Assets/_Scripts/LobbyUIManager.cs`
- 変更：`Assets/_Scripts/Night/NightEventUI.cs`
- 変更：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 変更：`Assets/_Scripts/PlayerEarningListUI.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 主操作は黄、危険操作は赤、所持金は黄緑、情報は青・紫で意味を統一する
- 常設HUDは画面端へ置き、ゲームプレイ中央を空ける
- 主ボタンと補助ボタンはサイズ・明度・アクセント線で優先度を分ける
- 選択状態は色だけでなく枠線とスケール変化でも示す
- 小さな状態表示以外では角丸カードを多用しない

### 影響範囲
- メインメニュー・ロビーUI
- ゲーム内HUD
- ESC・設定・キーバインドUI
- キャラクターカラー選択
- 夜イベント・ブラックジャック結果演出
- 夜終了時の収支ランキング

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity再コンパイル：確認済み（エラー0）
- Unity診断：正常
- Host動作：未確認
- Client動作：未確認
- 解像度別の見切れ：未確認

### 未完了・次の作業
- Unity Playでメニュー、HUD、ESC、クローゼットの実画面を確認
- 16:9以外の解像度とゲームパッド操作を確認

## 2026/07/30｜7回目

### 今回の変更
- Host・Clientでの実プレイ確認結果を反映
- DAY4以降の日数進行を確認済みに更新
- 共同口座の現在金額・必要金額表示を確認済みに更新
- キャラクターカラーのBobBody限定変更とマルチ同期を確認済みに更新

### 変更ファイル
- 変更：`PROGRESS.md`

### 重要な仕様
- DAY番号はDAY4以降も連番で増え続ける
- 家賃支払いはDAY3・6・9…の3日周期
- 共同口座は`¥現在額 / ¥必要額`で表示する
- キャラクター色は`BobBody`だけに反映し、Host・Client間で同期する

### 影響範囲
- 日数進行と家賃支払い
- 共同口座HUD
- キャラクターカスタマイズ
- マルチプレイ同期

### 確認状況
- コンパイル：確認済み
- DAY4以降の進行：確認済み
- 3日周期の家賃支払い：確認済み
- 共同口座HUD：確認済み
- BobBody限定の色変更：確認済み
- Host動作：確認済み
- Client動作：確認済み
- キャラクターカラー同期：確認済み
- 既存機能：確認済み

### 未完了・次の作業
- なし

## 2026/07/30｜6回目

### 今回の変更
- DAY3の次をDAY1へ戻さず、DAY4・DAY5以降も増え続けるように変更
- 家賃支払いはDAY3・6・9…の3日周期を維持
- 左上HUDからクール目と必要金額表示を削除し、通常のDAY表示へ復帰
- 共同口座を「現在金額 / 必要金額」形式へ変更
- キャラクター色の変更対象をFBX内の`BobBody`だけに限定

### 変更ファイル
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/PlayerNameDisplay.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- DAY番号はゲームオーバーになるまで連番で増え続ける
- 家賃は3日ごとに支払い、残り日数表示も3日周期で繰り返す
- 共同口座は`¥現在額 / ¥必要額`で表示する
- 色変更では目や別メッシュのMaterialPropertyBlockを変更しない

### 影響範囲
- 日数進行と家賃支払い
- 共同口座HUD
- キャラクターカスタマイズ
- マルチプレイ時の外見同期

### 確認状況
- FBX内の`BobBody`：確認済み
- C#コンパイル：確認済み（エラー0）
- Unityコンパイル：確認済み（エラー0）
- DAY4以降の実進行：未確認
- Host・Client間の色同期：未確認

### 未完了・次の作業
- Unity PlayでDAY3夜からDAY4朝への遷移を確認
- BobBodyだけが変色し、目の色が維持されることを確認

## 2026/07/30｜5回目

### 今回の変更
- キャラクターカラー画面で色ボタンをクリックできない問題を修正
- 必要金額とクール表示を左上の既存DAYカードへ統合
- 左上を「1クール目・朝 / ¥500」形式へ変更
- 共有口座カードから重複していた必要金額・クール表示を削除

### 変更ファイル
- 変更：`Assets/_Scripts/CharacterCloset.cs`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- カラー画面のCanvasには`GraphicRaycaster`を必ず設定する
- クール・朝夜・必要金額は左上のDAYカードでまとめて表示する
- ゲームオーバー中は統合HUDで`GAME OVER`表示を上書きしない

### 影響範囲
- キャラクターカスタマイズUI
- ゲーム内HUD
- 家賃表示

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unityコンパイル：確認済み（エラー0）
- Unity Playでのクリック操作：未確認

### 未完了・次の作業
- Unity Playで16色すべてのクリックとHUDの収まりを目視確認

## 2026/07/30｜4回目

### 今回の変更
- 家にクローゼットを追加し、各プレイヤーが16色からキャラクターカラーを選べるように変更
- ESC設定にキー割り当て変更画面を追加
- HUDへ必要金額と現在のクール・朝夜表示を追加
- 朝と夜を各3分へ変更
- 持ち運び箱を画面中央へ移動
- しゃがみを切り替え式から長押し式へ変更
- 初期マウス感度とカメラ揺れを弱く調整
- 頭上のプレイヤー名を非表示化
- ゲーム離脱後のカーソル、ホスト・参加ボタン、Steam接続状態の復帰処理を修正
- メインメニューとESCメニューの終了ボタンを実際のゲーム終了処理へ接続

### 変更ファイル
- 新規：`Assets/_Scripts/CharacterCloset.cs`
- 新規：`Assets/_Scripts/CharacterCloset.cs.meta`
- 変更：`Assets/_Scripts/PlayerNameDisplay.cs`
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/UI/GameSettings.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/MainMenuManager.cs`
- 変更：`Assets/_Scripts/Network/SteamLobby.cs`
- 変更：`Assets/_Scripts/Network/SteamManager.cs`
- 変更：`Assets/_Prefabs/Player.prefab`
- 変更：`PROGRESS.md`

### 重要な仕様
- キャラクター色はプレイヤーごとにServerで同期し、各自の端末へ保存する
- クローゼットは`GameRoom`読込時に家の中へ自動生成し、外見を差し替えても機能を再利用できる
- キー設定は重複時に元のキーと入れ替え、端末へ保存する
- 朝・夜のターン時間はどちらも180秒
- 家賃の追加額もNetworkVariableで全Clientへ同期し、HUDの必要金額へ反映する
- Steam Lobby離脱後はNetworkManagerの停止を待ってメニュー状態を復旧する

### 影響範囲
- プレイヤー外見とマルチプレイ同期
- キャラクター操作と入力設定
- HUD、日付・朝夜進行、家賃表示
- 持ち運び表示
- ESCメニュー、メインメニュー、Steam Lobby

### 確認状況
- C#コンパイル：確認済み（エラー0）
- Unity Skills診断：確認済み（Healthy、Console Error 0）
- Player Prefab設定：確認済み（横オフセット0、初期感度0.22）
- Host動作：未確認
- Client動作：未確認
- ビルド実機終了・再起動：未確認

### 未完了・次の作業
- Unity Playでクローゼットの設置位置とUIレイアウトを目視確認
- Host・Client間で色変更、家賃表示、離脱後の再参加を確認
- Windowsビルドで終了後のSteam再接続を確認

## 2026/07/30｜3回目

### 今回の変更
- Windowsビルド完了時に`steam_appid.txt`を実行ファイルの隣へ自動コピー
- App IDファイルがない・内容が不正な場合はビルドを失敗させる検証を追加
- 現在の`Build`フォルダにもテスト用App ID `480`を追加

### 変更ファイル
- 新規：`Assets/_Scripts/Editor/SteamAppIdBuildProcessor.cs`
- 変更：`PROGRESS.md`
- ビルド生成物：`Build/steam_appid.txt`

### 重要な仕様
- 友達へ直接渡すWindowsビルドには`steam_appid.txt`を含める
- `steam_appid.txt`は`Roomies.exe`と同じフォルダへ配置する
- Steamストアへ正式配信するDepotには`steam_appid.txt`を含めない

### 影響範囲
- Windowsビルド
- Steamworks初期化
- 友達へ渡すテスト版

### 確認状況
- App ID：`480`を確認済み
- 現在のBuildフォルダへの配置：確認済み
- 自動コピー処理：コード確認済み
- Unityコンパイル：確認済み（エラー0、既存警告19件）
- 友達のPC：未確認

### 未完了・次の作業
- 次回ビルド後に`Roomies.exe`の隣へ自動配置されることを確認
- 友達のPCでSteamを起動・ログインしてオンライン接続を確認

## 2026/07/30｜2回目

### 今回の変更
- ビルド起動時に空の地平線が表示される問題を修正
- 初期シーンを`SampleScene`から`MainMenuSteam`へ変更
- 未使用の`SampleScene`をBuild Settingsから除外

### 変更ファイル
- 変更：`ProjectSettings/EditorBuildSettings.asset`
- 変更：`PROGRESS.md`

### 重要な仕様
- Build Index 0は`MainMenuSteam`
- ゲーム開始後はシーン名指定で`GameRoom`へ遷移する

### 影響範囲
- Windowsビルド起動
- メインメニュー表示
- Steamロビーからゲームへのシーン遷移

### 確認状況
- Build Index 0：`MainMenuSteam`を確認済み
- Build Settingsのシーン存在・GUID：確認済み
- シーン遷移が名前指定であること：確認済み
- UnitySkills接続：未確認（ローカルサーバー停止中）
- 再ビルドした実行ファイル：未確認

### 未完了・次の作業
- 新しくビルドし直した実行ファイルでメインメニュー起動を確認

## 2026/07/30｜1回目

### 今回の変更
- Build Settingsから削除済みの`MainMenu.unity`参照を除去
- 起動用メニューを現行の`MainMenuSteam.unity`だけに統一

### 変更ファイル
- 変更：`ProjectSettings/EditorBuildSettings.asset`
- 変更：`PROGRESS.md`

### 重要な仕様
- メインメニューには`Assets/_Scenes/MainMenuSteam.unity`を使用する
- Build Settingsには実在するシーンだけを登録する

### 影響範囲
- Windowsビルド
- Build And Run
- メインメニューの起動順

### 確認状況
- Build Settingsの全シーン存在確認：確認済み
- 不正な`MainMenu.unity`参照：除去済み
- UnitySkills接続：未確認（ローカルサーバー停止中）
- Unity実ビルド：未確認

### 未完了・次の作業
- Unityで再度Build And Runを実行して最終確認

## 2026/07/29｜3回目

### 今回の変更
- UI全体を、黒・白・黄色を基調としたシンプルな製品ゲーム風デザインへ変更
- 太い枠、強い影、大きな角丸、多色ボタンを抑え、細い境界線と小さな角丸へ統一
- メインメニューの電飾・家具図・大量の機能カードを削除し、タイトル・ゲーム説明・開始操作だけに整理
- ゲーム内HUDを小型化し、日付・残り時間・共同口座・家賃期限の優先順位を明確化
- ESCメニューと夜イベント通知も、同じフラットな配色と控えめな演出へ変更
- ボタンのホバー拡大を抑え、短く自然な反応へ調整

### 変更ファイル
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/Night/NightEventUI.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- UIの基本色・角丸・枠・影・ボタン状態は`UITheme`が一元管理する
- 主操作だけ黄色にし、通常操作は暗いニュートラルカラーで表示する
- メインメニューとHUDは従来どおりランタイム生成し、SceneやPrefabの追加設定は不要
- ボタン参照、ロビー機能、共有口座、昼夜処理などのゲーム機能は変更しない

### 影響範囲
- メインメニュー
- Steamロビー
- ゲーム内HUD
- ESCメニュー
- 夜イベント通知
- 家賃・日付・収支など`UITheme`を利用する演出
- インタラクト表示

### 確認状況
- コンパイル：確認済み（エラー0、既存警告19件）
- メインメニュー表示：Unity Playで確認済み
- GameRoom HUD表示：Unity Playで確認済み
- Unity Console追加エラー：なし
- Play終了後のMainMenuSteam復帰：確認済み
- Steamロビー表示：未確認
- ESCメニュー表示：未確認
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- 実際のHost／Client接続中にロビーとESCメニューを確認
- 実プレイ中の背景に重ねた状態でHUDの最終サイズを調整
- 夜イベント・家賃支払い・収支ランキングの表示を実イベントで確認

## 2026/07/29｜2回目

### 今回の変更
- ブラックジャックの結果時に、テーブル全面へ大きな勝敗カードを表示する演出を追加
- 勝利・ブラックジャック・敗北・引き分け・エラーを、専用の文字と配色で判別できるように変更
- 勝利時は結果カードが弾み、敗北時は横に揺れる演出を追加
- 賭け金を含めた純増減額を「+○R」「-○R」「±0R」で大きく表示

### 変更ファイル
- 変更：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 勝敗判定と所持金処理は従来どおりServer側で行う
- 結果の純増減額は`resultNetAmount`で全Clientへ同期する
- 結果UIは`BlackjackTable`がランタイム生成するため、Prefabの追加設定は不要
- Prefabの結果表示時間は3.5秒のまま使用する

### 影響範囲
- ブラックジャックの結果表示
- ブラックジャックの収支表示
- Host／Client間のブラックジャック結果同期

### 確認状況
- コンパイル：確認済み（エラー0、既存警告19件）
- Unity一時プレビューで結果UI階層生成：確認済み
- Unity Console追加エラー：なし
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- 実際のHost／Clientプレイで勝利・敗北・引き分け演出を確認
- 実機距離から文字サイズと揺れ幅を最終調整

## 2026/07/29｜1回目

### 今回の変更
- イメージボードに合わせ、UI全体を暖色の共同生活・カオス生活ゲーム風へ変更
- メインメニューを「3日後の家賃、払える？」を中心にした情報ボード構成へ再設計
- 黒茶のパネル、生成りの枠、太い白文字、黄色・紫・ライムの差し色へ配色を統一
- メニュー背景に電飾、バイト種別カード、共同生活ルール、散らかった部屋の図を追加
- ゲーム内HUDを「日付・残り時間」「共同口座・家賃までの日数」の2カードへ整理
- 常時表示されていた操作説明を削除
- ESC、夜イベント、家賃支払い、朝演出、収支ランキングも同じトーンへ統一
- Network Debugを通常は非表示にし、F1で必要な時だけ表示する仕様へ変更

### 変更ファイル
- 変更：`Assets/_Scripts/UI/UITheme.cs`
- 変更：`Assets/_Scripts/UI/MenuThemer.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/Network/NetworkDebugOverlay.cs`
- 変更：`Assets/_Scripts/Night/NightEventUI.cs`
- 変更：`Assets/_Scripts/RentPaymentUI.cs`
- 変更：`Assets/_Scripts/DayTransitionUI.cs`
- 変更：`Assets/_Scripts/DayResultUI.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- シーンの既存機能やボタン参照は変えず、`MenuThemer`と`HudThemer`がランタイムで再配置する
- 共通色・枠・影・角丸は`UITheme`が一元管理する
- メインメニューとHUDの追加パーツは画像アセット不要で自動生成される
- 家賃までの日数は現在の日付からHUD側で表示する
- Network DebugはEditor／Development BuildでF1を押した時だけ表示する

### 影響範囲
- メインメニュー
- Steamロビー
- ゲーム内HUD
- ESCメニュー
- 夜イベント通知
- 家賃支払い演出
- 日付切り替え演出
- 収支ランキング
- 開発用Network Debug

### 確認状況
- コンパイル：確認済み（エラー0、既存警告19件）
- メインメニュー表示：Unity Playで確認済み
- ロビー表示：Unity Playで確認済み
- GameRoom HUD表示：Unity Playで確認済み
- メインメニュー／ロビーのUnity Console追加エラー：なし
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- 実際のHost／Client接続中にロビーとHUDを確認
- 実プレイ中の背景にHUDを重ね、文字サイズと占有面積を最終調整
- 16:9以外の解像度でメインメニューと各カードの表示を確認

## 2026/07/28｜10回目

### 今回の変更
- ESCメニューを本番向けの「再開・設定・メインメニュー・終了」構成へ拡張
- マウス感度・音量・画面モード・解像度・画質・カメラ揺れの設定を追加
- 設定をPlayerPrefsへ保存し、次回起動時とプレイヤー生成時に自動反映
- メインメニュー退出とゲーム終了に確認ダイアログを追加
- 設定画面や確認画面では、ESCで一つ前へ戻る操作を追加

### 変更ファイル
- 新規：`Assets/_Scripts/UI/GameSettings.cs`
- 新規：`Assets/_Scripts/UI/GameSettings.cs.meta`
- 変更：`Assets/_Scripts/UI/EscMenuUI.cs`
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ESCメニューはGameRoom読み込み時に自動生成され、Prefab配置は不要
- マルチプレイを止めないため、ESCメニュー中も`Time.timeScale`は変更しない
- 音量は`AudioListener.volume`、画質は`QualitySettings`、画面設定は`Screen`へ即時反映
- マウス感度とカメラ揺れはOwnerプレイヤーだけに反映
- 保存済み設定は`GameSettings`が一元管理する

### 影響範囲
- ESCメニューUI
- プレイヤーの視点感度
- カメラ演出
- ゲーム全体の音量
- 解像度・画面モード・画質
- Steamロビー退出

### 確認状況
- コンパイル：確認済み（エラー0、既存警告19件）
- GameRoomでのESCメニュー自動生成：確認済み
- メイン画面・設定画面の生成と切り替え：確認済み
- Unity Consoleの追加エラー：なし
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- 実際のHostとClientで各自の設定がローカルだけに反映されることを確認
- 16:9以外の解像度でUIが切れないことを確認

## 2026/07/28｜9回目

### 今回の変更
- GameRoomでESCを押すと開くポップなメニューを追加
- 「ゲームに戻る」「メインメニューへ」「ゲームを終了」の3ボタンを追加
- ESCメニュー表示中はローカルプレイヤーの移動・視点・インタラクト・家具ショップ操作を停止
- メニュー表示中だけカーソルを表示し、閉じた時にFPS操作へ自動復帰
- メインメニューへ戻る時にSteamロビーとNetcode接続を安全に終了する処理を追加

### 変更ファイル
- 新規：`Assets/_Scripts/UI/EscMenuUI.cs`
- 新規：`Assets/_Scripts/UI/EscMenuUI.cs.meta`
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scripts/Furniture/FurnitureEditController.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ESCメニューはGameRoom読込時に自動生成され、SceneやPrefabへの手配置は不要
- ESCをもう一度押すか「ゲームに戻る」で閉じる
- マルチプレイ同期を止めないため`Time.timeScale`は変更しない
- メニューを開いている本人の入力だけを停止し、他プレイヤーとゲーム内時間は動き続ける
- 「メインメニューへ」はロビー退出・NetworkManager停止後に`MainMenuSteam`を読み込む

### 影響範囲
- プレイヤー移動・視点
- インタラクト
- 家具ショップ
- カーソル制御
- Steamロビー退出
- Netcode切断
- GameRoom UI

### 確認状況
- C#コンパイル：確認済み（エラー0、既存警告19件）
- 新規スクリプトを含む外部ビルド：確認済み
- GameRoomのInputSystem EventSystem：確認済み
- GameRoom読込時のESCメニュー自動生成：Unity Playで確認済み
- メニュー開閉・カーソル表示切替・3ボタン生成：Unity Playで確認済み
- UI Graphic描画登録とCanvasGroup表示切替：Unity Playで確認済み
- HostでのESC表示・ボタン操作：未確認
- ClientでのESC表示・退出処理：未確認
- 画面解像度別レイアウト：未確認

### 未完了・次の作業
- HostとClientでESCを開き、本人の操作だけ止まることを確認する
- 「メインメニューへ」でロビー・接続が正常に終了することを確認する
- 16:9以外の解像度でUIが切れないことを確認する

## 2026/07/28｜8回目

### 今回の変更
- Steam未起動時にゲームが終了していた処理を修正
- Steam初期化失敗・DLL読込失敗を例外終了せず、オフライン状態として継続するよう変更
- 起動後にSteamとの接続が切れた場合も、ゲーム本体を継続してオンライン機能だけ停止する処理を追加
- Steam未利用時はHost／Joinボタンを無効化し、メインメニューに理由を表示
- ロビー表示中にSteam接続が切れた場合のSteam API呼び出しを防止

### 変更ファイル
- 変更：`Assets/_Scripts/Network/SteamManager.cs`
- 変更：`Assets/_Scripts/Network/SteamLobby.cs`
- 変更：`Assets/_Scripts/MainMenuManager.cs`
- 変更：`Assets/_Scripts/LobbyUIManager.cs`
- 変更：`Assets/_Scripts/LOBBY_SETUP_GUIDE.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- Steam未起動でもゲームは終了せず、メインメニューまで起動する
- Steamが利用できない間はSteamロビーのHost／Joinのみ使用不可
- `SteamManager`の`Restart Through Steam`は初期値OFF
- `Restart Through Steam`をONにした場合だけSteam経由の強制再起動を行う
- Steam APIが初期化済みの場合は従来どおりSteamロビー・アバター・P2P通信を使用する

### 影響範囲
- ゲーム起動
- Steam初期化
- メインメニュー
- Steamロビー
- Steam切断時の処理

### 確認状況
- C#コンパイル：確認済み（エラー0、既存警告19件）
- Steam未初期化時のAPIガード：確認済み
- Steam強制再起動の初期値OFF：確認済み
- Steam未起動での実ビルド起動：未確認
- Steam起動中のHost／Client動作：未確認

### 未完了・次の作業
- Steamを完全終了した状態でビルドを起動し、メインメニューが残ることを確認する
- Steam起動中にHost／Joinが従来どおり動くことを確認する
- ロビー中にSteamを終了した場合、ゲームが落ちずエラー表示になることを確認する

## 2026/07/28｜7回目

### 今回の変更
- キャラ移動をミメシス／Gamble With Your Friends風の、重さと弾みがある操作感へ調整
- 地上の加速・減速と空中での操作量を追加し、急発進・急停止する移動を改善
- コヨーテタイムとジャンプ先行入力を追加し、ジャンプの反応を改善
- 歩行・ダッシュ時のカメラ揺れ、左右移動時の傾き、着地時の沈み込みを追加
- ダッシュ時のFOV変化と、しゃがみ時の視点・当たり判定の滑らかな遷移を追加

### 変更ファイル
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`Assets/_Prefabs/Player.prefab`
- 変更：`PROGRESS.md`

### 重要な仕様
- 基本操作はWASD、左Shiftダッシュ、Spaceジャンプ、Ctrlしゃがみのまま
- ダッシュは前進入力中のみ発動する
- 持ち物の重量低下と家具の移動速度バフは、新しい加速移動にも引き続き反映する
- カメラ演出の強さ・加速・減速・空中操作量はPlayer PrefabのInspectorから調整できる
- カメラ演出を止めたい場合はPlayerMovementの`Enable Camera Motion`をOFFにする
- プレイヤーの位置同期は既存のOwner Authority NetworkTransformを使用する

### 影響範囲
- プレイヤー移動
- ジャンプ・しゃがみ
- 一人称カメラ
- 持ち物による速度低下
- 家具による移動速度バフ
- マルチプレイ位置同期

### 確認状況
- C#コンパイル：確認済み（エラー0、既存警告19件）
- Player Prefab設定：確認済み
- Unity Console：未確認（Unity MCP接続解除中）
- Host動作：未確認
- Client動作：未確認
- カメラ酔い・操作感：実プレイ未確認

### 未完了・次の作業
- Hostで歩行、ダッシュ、ジャンプ、しゃがみ、着地演出を確認する
- Clientで移動位置と回転がHostへ同期されることを確認する
- カメラ揺れの強さが酔わない範囲か実プレイで調整する

## 2026/07/28｜6回目

### 今回の変更
- 購入した家具へ照準を合わせて`F`で持ち上げられる機能を追加
- 家具を持っている間に`R`で45度回転、`F`で床へ再設置できるよう追加
- 家具の位置・回転・持ち主をServer側で管理し、全Clientへ`NetworkTransform`で同期
- 家具移動中はColliderと家具効果を一時停止し、設置後に再開する処理を追加
- 遠距離からの持ち上げ・移動、他Client名義、不正座標をServer側で拒否
- 家具を持ったClientが退出した場合、家具を自動解放する処理を追加
- 設置時に家具の底面を検出し、床へ自動スナップする処理を追加
- `NetworkFurniture.prefab`を新規作成し、NetworkPrefab登録とGameRoomのDayManager参照を設定
- 家具ネットワーク設定が外れた場合に修復できるEditorメニューを追加
- 家具購入をClient側で即成功表示せず、Serverの購入・生成成功後に結果表示するよう変更
- Clientだけで家具を削除して同期が崩れていた右クリック撤去を廃止
- 家具効果一覧キーをランキングと競合する`Tab`から`O`へ変更
- 本番家具Prefabがカタログへ登録された場合、仮Cubeではなく子モデルとして生成できるよう対応

### 変更ファイル
- 新規：`Assets/_Prefabs/NetworkFurniture.prefab`
- 新規：`Assets/_Prefabs/NetworkFurniture.prefab.meta`
- 新規：`Assets/_Scripts/Editor/FurnitureNetworkInstaller.cs`
- 新規：`Assets/_Scripts/Editor/FurnitureNetworkInstaller.cs.meta`
- 変更：`Assets/DefaultNetworkPrefabs.asset`
- 変更：`Assets/_Scenes/GameRoom.unity`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scripts/Furniture/NetworkFurniture.cs`
- 変更：`Assets/_Scripts/Furniture/PlacedFurniture.cs`
- 変更：`Assets/_Scripts/Furniture/FurnitureEditController.cs`
- 変更：`Assets/_Scripts/Furniture/FURNITURE_NETWORK_SETUP.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- 夜に`P`で家具ショップ、数字／ホイールで選択、左クリックで購入する
- 家具へ照準を合わせて`F`で持つ、`R`で45度回転、`F`で設置する
- 家具効果一覧は`O`で表示する
- 家具の持ち上げ・移動・設置はServer側で持ち主と距離を検証する
- 家具移動の送信は20回／秒に制限し、`NetworkTransform`で補間表示する
- 家具の移動中は効果を無効化し、床へ設置後にRoom判定を再開する
- 購入成功表示はServerで残高減算とNetworkObject生成が完了した後だけ出す
- `NetworkFurniture.prefab`は`DefaultNetworkPrefabs`へ登録済み
- GameRoomのDayManagerへ`NetworkFurniture.prefab`を割り当て済み
- 設定修復は`Tools/Roomies/家具ネットワーク設定を修復`から実行できる

### 影響範囲
- 家具ショップ
- 家具購入と共有口座
- 家具の持ち運び・再配置
- 家具ColliderとRoom判定
- 移動速度・毎朝収入の家具効果
- PlayerInteractの`F`・`R`操作
- NetworkPrefab登録
- Host・Clientの家具同期

### 確認状況
- Runtime C#ビルド：確認済み（エラー0、既存警告19件）
- Editor C#ビルド：確認済み（エラー0）
- NetworkFurniture必須コンポーネント：確認済み
- DefaultNetworkPrefabs登録：確認済み
- GameRoomのDayManager参照：確認済み
- Clientだけの旧家具削除処理：0件
- 家具持ち運びRPCと所有者検証：確認済み
- Unity Console：未確認（Unity MCP接続が解除中）
- Host実プレイ：未確認
- Client実プレイ：未確認
- 本番家具モデル：未登録

### 未完了・次の作業
- Hostで家具を購入し、Fで持つ・Rで回す・Fで床へ置くことを確認する
- Clientで家具を移動し、Hostにも同じ位置・回転で表示されることを確認する
- 移動中にColliderと家具効果が止まり、設置後に復帰することを確認する
- 家具を持った状態でClient退出し、家具が再び持てることを確認する
- 本番家具モデルをカタログへ登録して見た目を差し替える

## 2026/07/28｜5回目

### 今回の変更
- 共有口座の全入出金を`SharedMoneyManager`のServer権限APIへ集約
- 全額支払い用の`TrySpend`、残高まで徴収する`SpendUpTo`、入金用の`TryAdd`を追加
- 不正な金額、Client側からの直接変更、残高不足、int上限超過を拒否する処理を追加
- 全取引へ連番、用途、増減額、変更前後の残高を出すServer監査ログを追加
- 家賃、家具、スロット、ブラックジャック、配達、闇バイト、光熱費、家具収入を新APIへ移行
- 罰金・レア箱破損・光熱費で共有口座が0未満にならないよう変更
- スロットと配達で、RPC送信者と指定プレイヤーの所有者が一致しない要求を拒否
- Clientが任意の金額を送ってローカル家具を生成できたフォールバック購入を廃止
- 共有口座への入金が成功した場合だけ、対応する個人収支へ報酬を反映するよう統一

### 変更ファイル
- 変更：`Assets/_Scripts/SharedMoneyManager.cs`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 変更：`Assets/_Scripts/Slot/SlotMachine.cs`
- 変更：`Assets/_Scripts/Delivery/DeliveryButton.cs`
- 変更：`Assets/_Scripts/Delivery/DeliveryItem.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingPlayer.cs`
- 変更：`Assets/_Scripts/Night/NightEventManager.cs`
- 変更：`Assets/_Scripts/Furniture/FurnitureEditController.cs`
- 変更：`Assets/_Scripts/Furniture/FurnitureEffectManager.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 共有口座の変更はServer側の`TryAdd`、`TrySpend`、`SpendUpTo`だけが行う
- 家賃・家具・BETは全額を払えない場合、残高を一切変更しない
- 逮捕罰金・レア箱破損・光熱費は残高まで徴収し、共有口座の下限を0とする
- 実際に共有口座へ反映できた金額だけを個人収支へ反映する
- Clientから金額を直接指定する共有口座RPCは使用しない
- 配達とスロットは、要求したClient本人の`PlayerEarning`だけを変更できる
- 取引ログは`[Money #連番] 用途 ±金額 変更前 -> 変更後`形式でServer Consoleへ出す

### 影響範囲
- 共有口座と増減UI
- 家賃支払い
- 家具購入・家具収入
- 配達報酬・レア箱破損
- 闇バイト報酬・逮捕罰金
- スロット・ブラックジャック
- 夜イベントの光熱費
- プレイヤー個人収支
- マルチプレイRPC検証

### 確認状況
- C#ビルド：確認済み（エラー0、既存警告19件）
- 旧入出金APIの呼び出し残り：0件
- `SharedMoneyManager`外からの残高直接変更：0件
- 全入出金元の新API移行：確認済み
- 変更ファイルの差分チェック：確認済み
- Unity Console：未確認（Unity MCP接続が解除中）
- Host動作：未確認
- Client動作：未確認
- 同時購入・同時BET：未確認

### 未完了・次の作業
- Hostで各報酬・BET・家賃・罰金を発生させ、取引ログとHUD増減を確認する
- 残高不足の状態で家具・BET・家賃が減算されないことを確認する
- 残高より高い罰金・破損・光熱費で残高が0になり、マイナスにならないことを確認する
- HostとClientから同時購入・同時BETを行い、残高と生成物が一致することを確認する
- Clientから配達とスロットを操作し、本人の個人収支だけが変化することを確認する

## 2026/07/28｜4回目

### 今回の変更
- ブラックジャック画面をDealer側とPlayer側へ上下に大きく分け、カードと合計値を見分けやすく変更
- 各カードを白いカード風の背景、赤・黒のスート色、`[A♠]`形式で表示するよう変更
- 勝ちは緑、負けは赤、引き分けは黄色、Blackjackは金色へ画面全体を切り替える演出を追加
- 勝敗を日本語の大きな結果バナーで表示し、結果が出た瞬間にポンと拡大する演出を追加
- 勝敗結果の表示時間を2.5秒から3.5秒へ延長
- 勝敗の種類をNetworkVariableで同期し、HostとClientで同じ色と結果演出を出すよう変更

### 変更ファイル
- 変更：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 変更：`Assets/_Prefabs/BlackjackTable.prefab`
- 変更：`PROGRESS.md`

### 重要な仕様
- 勝敗種別はServer側で決定し、`ResultKind`を全Clientへ同期する
- 通常勝利、Blackjack、敗北、引き分け、残高不足で異なる表示色を使用する
- カード表示はランタイム生成UIのため、PrefabへUIを手作業で追加する必要はない
- 既に配置済みの`BlackjackTable.prefab`にもスクリプト更新が自動反映される

### 影響範囲
- ブラックジャックのカード表示
- ブラックジャックの勝敗表示
- ブラックジャックのマルチプレイ表示同期

### 確認状況
- C#ビルド：確認済み（エラー0）
- 変更ファイルの差分チェック：確認済み
- Unity Console：未確認（Unity MCP接続が解除中）
- 実際のカードレイアウト：未確認
- 勝敗色・バナー拡大演出：未確認
- Host動作：未確認
- Client動作：未確認

### 未完了・次の作業
- Unity Play上でカードの大きさと画面内への収まりを確認する
- 勝ち・負け・引き分け・Blackjackの各色と拡大演出を確認する
- HostとClientで同じ勝敗表示になることを確認する

## 2026/07/28｜3回目

### 今回の変更
- シーンへPrefabを配置するだけで遊べるブラックジャックを追加
- `E`でゲーム開始／HIT、`R`でSTAND、マウスホイールでBET変更できるよう追加
- プレイヤーとDealerのカード、合計値、勝敗、賭け金を表示するワールドUIを自動生成
- 仮のブラックジャック台モデルと差し替え可能なマテリアルを作成
- 賭け金、カード抽選、Dealer処理、勝敗、配当をServer側で処理し、全Clientへ同期
- 勝敗による増減を共有口座とプレイヤー個人収支へ反映

### 変更ファイル
- 新規：`Assets/_Scripts/Blackjack/BlackjackTable.cs`
- 新規：`Assets/_Scripts/Blackjack/SETUP.md`
- 新規：`Assets/_Prefabs/BlackjackTable.prefab`
- 新規：`Assets/_Materials/Blackjack/BlackjackFelt.mat`
- 新規：`Assets/_Materials/Blackjack/BlackjackGold.mat`
- 新規：`Assets/_Materials/Blackjack/BlackjackWood.mat`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- `BlackjackTable.prefab`をGameRoomへ配置すれば追加設定なしで動作する
- 1台につき同時に1人がプレイする
- BETは10R、50R、100Rから選択する
- Dealerは17以上になるまでカードを引く
- 通常勝利は賭け金込み2倍、Blackjackは2.5倍、PUSHは賭け金を返す
- Server側でプレイヤー所有者、共有口座残高、ターン所有者を検証する
- Scene配置のNetworkObjectとして同期するため、NetworkPrefabリストへの追加は不要
- 仮モデルを差し替える場合もPrefabルートの`NetworkObject`、`BlackjackTable`、`BoxCollider`は残す

### 影響範囲
- プレイヤーのインタラクト入力
- 共有口座
- プレイヤー個人収支
- ミニゲーム
- マルチプレイ同期

### 確認状況
- C#コンパイル：確認済み
- Unity Console：エラー0件
- Prefabの必須コンポーネント：確認済み
- BET切替（10R→50R）：確認済み
- ワールドUI自動生成：確認済み
- Inspector追加設定不要：確認済み
- Hostでの一連のゲーム進行：未確認
- ClientからのHIT／STANDと表示同期：未確認
- 共有口座・個人収支の実プレイ反映：未確認

### 未完了・次の作業
- `BlackjackTable.prefab`をGameRoomへ配置する
- HostでDEAL、HIT、STAND、勝敗、配当を確認する
- Clientから操作し、HostとClientでカード表示と収支が同期することを確認する
- 必要に応じて仮テーブルモデルを本番モデルへ差し替える

## 2026/07/28｜2回目

### 今回の変更
- デリバリーのレア箱Prefabで、見た目に対して極端に小さかった当たり判定を修正
- レア箱の接地を安定させ、スポーン直後に回転し続ける原因を解消
- 見た目の箱全体へ照準を合わせて持てるように調整

### 変更ファイル
- 変更：`Assets/_Prefabs/rare_Box.prefab`
- 変更：`PROGRESS.md`

### 重要な仕様
- レア箱のBox Colliderは、ルートの0.01スケールを考慮してローカルサイズを設定する
- `CarryableObject`、`Rigidbody`、`DeliveryItem`、ネットワーク設定は既存仕様を維持する

### 影響範囲
- デリバリーのレア箱
- レア箱の持ち上げ判定
- レア箱のスポーン後の物理挙動

### 確認状況
- PrefabのCollider実寸：確認済み（約0.60 × 0.34 × 0.48m）
- `CarryableObject`のCollider・Rigidbody参照：確認済み
- コンパイル：確認済み
- Unity Console：エラー0件
- Host動作：未確認
- Client動作：未確認
- 既存機能：未確認

### 未完了・次の作業
- HostとClientでレア箱を持てることを実プレイ確認
- レア箱がスポーン後に回転し続けないことを実プレイ確認

## 2026/07/28｜1回目

### 今回の変更
- 3日目の夜終了後、全プレイヤーへ家賃支払いフェーズを全画面表示する機能を追加
- 今日の収支表示後に、請求額・共有口座残高・引き落とし進捗を表示する流れへ変更
- 支払い可能時は「支払い完了！」と支払い後残高を表示してから1日目の朝へ進むよう変更
- 残高不足時は、赤い点滅・カードの横揺れ・「残高が足りない…！」の拒否エフェクトを再生するよう追加
- 拒否エフェクト後に全画面で大きく「失敗！！」を表示し、その後に既存のゲームオーバーへ進むよう変更
- 家賃値上げ分を含む実際の請求額を演出へ反映

### 変更ファイル
- 新規：`Assets/_Scripts/RentPaymentUI.cs`
- 新規：`Assets/_Scripts/RentPaymentUI.cs.meta`
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 家賃支払いフェーズは3日目の夜終了時だけ実行する
- 表示順は「今日の収支」→「家賃支払いフェーズ」→成功なら「1日目の朝」
- 支払い成功時の残高減算と支払い可否判定はServer側で行う
- 家賃画面の表示はClientRpcでHostと全Clientへ同期する
- 残高不足時は減算せず、拒否エフェクトと「失敗！！」を最後まで表示してからゲームオーバーにする
- UIはランタイム生成のためScene、Prefab、Inspectorへの追加設定は不要

### 影響範囲
- 3日サイクルの終了処理
- 家賃支払い・共有口座残高
- 今日の収支表示から翌朝への遷移
- ゲームオーバー処理
- Host・Clientの全画面UI同期

### 確認状況
- 家賃支払い全画面UIのランタイム生成：Unity Play上で確認済み
- 支払い成功表示（請求500・残高900→400）：Unity Play上で確認済み
- 残高不足表示（請求500・残高120）：Unity Play上で確認済み
- 赤い拒否表示から「失敗！！」への切り替え：Unity Play上で確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 3日目終了からの実際のServer進行：未確認
- Clientでの家賃画面同期：未確認

### 未完了・次の作業
- Hostで3日目の夜を終了し、収支→家賃→翌朝の順に進むことを確認する
- 残高不足時に「失敗！！」後でゲームオーバーになることを確認する
- Clientにも同じタイミング・内容で家賃画面が表示されることを確認する

---

## 2026/07/27｜2回目

### 今回の変更
- 左上の共有口座が増減したとき、変化額を視覚表示する機能を追加
- 増加時は緑色の角丸表示で「+¥金額」、減少時は赤色の角丸表示で「-¥金額」を表示
- 増減表示が共有口座カードの右側へポンと現れ、上へ浮きながら約1.65秒で消えるアニメーションを追加
- NetworkVariableの残高変更イベントから表示するため、配達報酬・闇バイト・スロット・家具・家賃など全ての共有口座増減へ対応
- 連続して残高が変化した場合も、変化ごとに個別表示するよう対応

### 変更ファイル
- 変更：`Assets/_Scripts/SharedMoneyManager.cs`
- 変更：`Assets/_Scripts/UI/HudThemer.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 増減額は共有口座のNetworkVariableの旧値と新値の差から算出する
- Hostだけでなく、残高同期を受けた各Clientでも同じ増減表示を生成する
- 初期残高の表示時や差額0の場合は増減表示を出さない
- 表示は入力を遮らず、ゲーム時間停止中でもアニメーションする
- Scene、Prefab、Inspectorへの追加設定は不要

### 影響範囲
- 共有口座HUD
- 共有口座のNetworkVariable同期
- 配達・闇バイト・スロット・家具・家賃など共有口座を変更する全機能

### 確認状況
- 緑色の`+¥250`表示生成：Unity Play上で確認済み
- 赤色の`-¥50`表示生成：Unity Play上で確認済み
- 増加・減少の連続表示：Unity Play上で確認済み
- 上昇・フェードアウトアニメーション：Unity Play上で確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- Hostの実際の報酬・支払いからの表示：未確認
- ClientのNetworkVariable同期からの表示：未確認

### 未完了・次の作業
- Hostで配達報酬などを受け取り、実際の共有口座増加表示を確認する
- 支払い・罰金で共有口座減少表示を確認する
- Clientでも同じ増減額が表示されることを確認する

---

## 2026/07/27｜1回目

### 今回の変更
- ゲーム開始前のルール説明画面をいったん削除
- 10秒カウントダウン、開始中表示、説明画面内の開始・戻るボタンを削除
- `LobbyUIManager`を説明画面追加前の状態へ戻し、ロビーの「ゲーム開始」から直接`SteamLobby.StartGame()`を呼ぶよう復元
- 説明画面対応で追加したHost開始中・失敗再試行処理を`SteamLobby`から削除し、変更前のHost開始フローへ復元

### 変更ファイル
- 削除：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 削除：`Assets/_Scripts/UI/GameRulesPopup.cs.meta`
- 復元：`Assets/_Scripts/LobbyUIManager.cs`
- 復元：`Assets/_Scripts/Network/SteamLobby.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ロビーの「ゲーム開始」を押すと説明画面を挟まず、直接既存のHost開始処理を実行する
- Host開始、GameRoom読み込み、Clientへの開始通知は説明機能追加前の仕様へ戻す
- Scene、Prefab、Inspectorの追加設定は不要

### 影響範囲
- MainMenuSteamのゲーム開始操作
- Steam Host開始処理
- Host・ClientのGameRoom遷移

### 確認状況
- `LobbyUIManager.cs`の説明追加前コードへの復元：確認済み
- `SteamLobby.cs`の説明追加前コードへの復元：確認済み
- C#スクリプト検証：確認済み（エラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 実際のSteam HostからGameRoomへの遷移：未確認
- Clientの同期遷移：未確認

### 未完了・次の作業
- Steam Hostロビーで「ゲーム開始」を押し、GameRoomへ遷移することを確認する
- ClientもGameRoomへ同期遷移することを確認する

---

## 2026/07/26｜6回目

### 今回の変更
- ルール画面のスタートを押すとホスト開始画面へ戻って見える問題を修正
- スタート後はルール画面を閉じず、「ゲームを開始中…」表示のままSteamリレー準備とGameRoom遷移を待つよう変更
- Host開始処理の多重実行を防ぐガードを追加
- Host開始要求を受けた時点でBusy状態にし、ロビーのStartButtonが再び有効化されないよう変更
- NetworkManager・SteamTransport・Host起動に失敗した場合、理由を表示して「もう一度スタート」から再試行できる処理を追加
- Host開始要求、ルール画面からの開始、開始失敗理由の診断ログを追加

### 変更ファイル
- 変更：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 変更：`Assets/_Scripts/LobbyUIManager.cs`
- 変更：`Assets/_Scripts/Network/SteamLobby.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 「よーし、スタート！」後はルール画面を開始中表示として維持し、GameRoomのScene読み込みで自動的に破棄する
- 開始処理中は開始・戻るボタンとEscを無効化する
- Host開始に失敗した場合だけ、開始ボタンと戻るボタンを再有効化する
- Steamリレー待機中もStartButtonを再表示・再有効化しない
- 既存のHost開始後のGameRoom読み込みとClient通知順序は変更しない

### 影響範囲
- ゲーム開始前ルール画面
- Steam Host開始処理
- ロビー待機画面の開始中表示
- Host・ClientのGameRoom遷移

### 確認状況
- 開始クリック直後のゲーム開始コールバック実行：Unity Play上で確認済み
- 開始後にルール画面が閉じず残ること：Unity Play上で確認済み
- 「ゲームを開始中…」表示とボタン無効化：Unity Play上で確認済み
- 開始失敗後の「もう一度スタート」再有効化：Unity Play上で確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 実際のSteam HostからGameRoomへの遷移：未確認
- ClientのGameRoom同期遷移：未確認

### 未完了・次の作業
- Steam Hostロビーから開始し、「ゲームを開始中…」後にGameRoomへ遷移することを確認する
- ClientもGameRoomへ同期遷移することを確認する
- 遷移しない場合は追加した`[SteamLobby] Host開始...`ログで停止箇所を確認する

---

## 2026/07/26｜5回目

### 今回の変更
- 10秒カウントダウン中でも「よーし、スタート！」を押せるよう変更
- カウントダウンを開始制限ではなく、説明時間の目安表示へ変更
- カウント表示を「説明はあと○秒」、完了後を「説明おわり！」へ変更
- スタートを押してもルール画面が閉じるだけでロビーへ戻る問題を修正
- ゲーム開始コールバックを閉じるアニメーション完了後ではなく、クリックした瞬間に実行するよう変更

### 変更ファイル
- 変更：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 開始ボタンはルール画面の表示直後から使用できる
- 10秒カウントダウンは画面上で継続するが、ゲーム開始を妨げない
- 開始クリック時は、ルール画面の終了アニメーションより先に既存の`StartGameAfterRules`を実行する
- 「まだ待って！」とEscはゲームを開始せずルール画面だけ閉じる
- Scene、Prefab、Inspectorへの追加設定は不要

### 影響範囲
- ゲーム開始前ルール画面
- ホストのゲーム開始処理
- SteamロビーからGameRoomへの開始フロー

### 確認状況
- 表示直後の開始ボタン有効化：Unity Play上で確認済み
- 表示直後の「よーし、スタート！」表示：Unity Play上で確認済み
- 開始クリックと同フレームでのゲーム開始コールバック実行：Unity Play上で確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 実際のSteam HostロビーからGameRoomへの遷移：未確認
- Clientの同期遷移：未確認

### 未完了・次の作業
- Steam Hostロビーで開始ボタンを押し、GameRoomへ遷移することを確認する
- ClientもGameRoomへ同期遷移することを確認する

---

## 2026/07/26｜4回目

### 今回の変更
- ゲーム開始ルール画面へ10秒のカウントダウンを追加
- カウントダウン中は開始ボタンを無効化し、「あと○秒」と残り時間を表示するよう変更
- 上部のピンク見出しにも「スタートまで○秒」を表示するよう変更
- 10秒経過後、見出しを「スタートできます！」、開始ボタンを「よーし、スタート！」へ切り替えてクリック可能にする処理を追加
- 無効中の開始処理がコード経由で誤実行されないよう確認処理を追加

### 変更ファイル
- 変更：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- カウントダウンはゲーム時間停止の影響を受けない実時間で10秒進む
- 「まだ待って！」とEscはカウントダウン中も使用できる
- ゲーム開始は10秒経過後の有効な開始ボタンからのみ実行する
- Scene、Prefab、Inspectorへの追加設定は不要

### 影響範囲
- ゲーム開始前ルール画面
- ホストのゲーム開始タイミング

### 確認状況
- 表示直後の開始ボタン無効化：Unity Play上で確認済み
- 表示直後の「あと10秒」表示：Unity Play上で確認済み
- 10秒経過後の有効化処理：コード確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 実際のSteam Hostロビーで10秒待機後のクリック：未確認
- ClientのGameRoom同期遷移：未確認

### 未完了・次の作業
- Steam Hostロビーで10秒カウントダウン完了後に開始ボタンが押せることを確認する
- Host・ClientがGameRoomへ同期遷移することを確認する

---

## 2026/07/26｜3回目

### 今回の変更
- ゲーム開始ルール画面からWASD・Shift・Space・E・Fの操作説明を削除
- 操作説明を削除して空いた領域へ3つのルールカードを少し下げ、画面全体の余白バランスを調整

### 変更ファイル
- 変更：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- 開始前画面には昼のバイト、夜のハプニング、3日ごとの家賃だけを表示する
- 「まだ待って！」と「よーし、スタート！」の操作は変更しない
- 解像度に応じた自動縮小と文字切れ防止処理は維持する
- Scene、Prefab、Inspectorへの追加設定は不要

### 影響範囲
- ゲーム開始前ルール画面の表示内容とレイアウト

### 確認状況
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 操作説明UIのコード削除：確認済み
- 実際のSteam Host・Clientロビー：未確認

### 未完了・次の作業
- Steam Hostロビーでルール画面の最終レイアウトを確認する

---

## 2026/07/26｜2回目

### 今回の変更
- ゲーム開始ルール画面で一部UI・文字が切れる問題を修正
- 上部のピンク見出し内テキストを背景全体へ正しく広げ、文字サイズを自動調整するよう変更
- 3つのルール本文が白いカードの下へ25pxはみ出していた配置を修正
- ルール本文の文字サイズを枠内で自動調整し、改行内容が増えても切れにくく変更
- GameViewの解像度・縦横比が変わったとき、画面端に70px相当の安全枠を残してルールカード全体を自動縮小する処理を追加
- 表示・終了アニメーションにも自動縮小率を反映するよう変更

### 変更ファイル
- 変更：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ルール画面の基準サイズとデザインは維持し、画面へ収まらない場合だけ全体を縮小する
- 文字の自動調整は上部見出しと3つのルール本文に適用する
- 解像度変更は表示中にも検出し、その場で収まる大きさへ更新する
- Scene、Prefab、Inspectorへの追加設定は不要

### 影響範囲
- ゲーム開始前ルール画面
- 小さいGameView・異なるアスペクト比でのUI表示
- ルール画面の開始・終了アニメーション

### 確認状況
- 修正前の上部見出し文字オーバーフロー：Unity上で再現済み
- 修正前のルール本文のカード外はみ出し：座標確認済み
- 640×480 GameViewで全TMPテキストのオーバーフローなし：確認済み
- ルール画面の自動縮小率適用：確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- 実際のSteam Host・Clientロビー：未確認

### 未完了・次の作業
- Steam Hostロビーでルール画面を開き、使用中のGameView解像度で見た目を最終確認する
- Host・Clientが「よーし、スタート！」後にGameRoomへ同期遷移することを確認する

---

## 2026/07/26｜1回目

### 今回の変更
- Steamロビーの「ゲーム開始」を押したとき、すぐ開始せずルール確認画面を表示するよう変更
- 明るいクリーム色のカード、カラフルな3つのルールカード、紙吹雪風の飾りを使ったポップでコミカルなデザインを追加
- 昼のバイト、夜のハプニング、3日ごとの家賃500R、基本操作を開始前に確認できるよう追加
- 「よーし、スタート！」で既存のゲーム開始処理を実行し、「まだ待って！」またはEscでロビーへ戻れるよう追加
- ルール画面の表示・アニメーション・UI一式をランタイム生成し、SceneやInspectorへの追加設定を不要にした

### 変更ファイル
- 新規：`Assets/_Scripts/UI/GameRulesPopup.cs`
- 新規：`Assets/_Scripts/UI/GameRulesPopup.cs.meta`
- 変更：`Assets/_Scripts/LobbyUIManager.cs`
- 変更：`PROGRESS.md`

### 重要な仕様
- ルール画面はホスト専用の既存StartButtonから表示する
- ルール画面で開始を確定するまでは`SteamLobby.StartGame()`を呼ばない
- 開始確定後は既存のHost開始・Client通知・GameRoom遷移処理をそのまま使用する
- ルール画面は最前面Canvasとして生成し、表示中は背面のロビーUI操作を遮断する
- ロビー退出時は開いているルール画面も閉じる
- Scene、Prefab、Inspectorへの新規登録は不要

### 影響範囲
- MainMenuSteamのSteamロビー待機画面
- ホストのゲーム開始操作
- ゲーム開始前のルール説明UI
- HostからClientへの既存ゲーム開始フロー

### 確認状況
- Unity MCP接続：確認済み
- C#スクリプト検証：確認済み（今回のエラー0）
- Unity Console：確認済み（停止状態でエラー0）
- MainMenuSteamでのルールUI生成：確認済み
- 最前面Canvas・ルールカード・開始ボタンのランタイム生成：確認済み
- 確定ボタンから既存開始処理への接続：コード確認済み
- 実際のSteam Hostロビーからの表示・開始：未確認
- ClientのGameRoom同期遷移：未確認
- 解像度ごとのGameView見た目：未確認

### 未完了・次の作業
- Steam Hostロビーで「ゲーム開始」を押し、ルール画面が表示されることを確認する
- 「まだ待って！」とEscでロビーに戻れることを確認する
- 「よーし、スタート！」でHostとClientがGameRoomへ移動することを確認する
- 実際のGameViewで文字切れやレイアウト崩れがないことを確認する

---

## 2026/07/21｜11回目

### 今回の変更
- Codexから現在開いているRoomies_saveのUnity Editorへ接続するプロジェクト専用MCP設定を追加
- 誤ったHTTP接続先`127.0.0.1:8080/mcp`のグローバルUnityMCPを、このプロジェクト内では無効化
- Unity AI Assistant Relayをstdio MCPとして起動し、`--project-path`でRoomies_saveを指定
- Unity Editor起動時にMCP Bridgeを自動起動するEditorスクリプトを追加
- Bridgeを手動再起動できる`Tools > Codex > Start Unity MCP`メニューとF12ショートカットを追加
- 終了済みUnity PID 8416の古いMCP接続記録を一時フォルダへ退避

### 変更ファイル
- 新規：`.codex/config.toml`
- 新規：`Assets/_Scripts/Editor.meta`
- 新規：`Assets/_Scripts/Editor/UnityMcpAutoStart.cs`
- 新規：`Assets/_Scripts/Editor/UnityMcpAutoStart.cs.meta`
- 変更：`PROGRESS.md`

### 重要な仕様
- Unity MCPは`C:\Users\yuse2\.unity\relay\relay_win.exe`を使用する
- 接続対象は`D:\Bonchan\Roomies_save`を開いているUnity Editorに限定する
- Unityが開いていない場合もCodexを起動できるよう、MCPサーバーは必須扱いにしない
- Unity Bridgeはプロジェクトを開いたときに自動起動し、必要ならF12で再起動できる
- 初回の直接接続はUnity側で接続許可が必要
- 新しいMCP設定をCodexのツール一覧へ反映するにはCodexアプリの再起動が必要

### 影響範囲
- Codexのプロジェクト別MCP設定
- Unity Editorの起動時処理
- Unity AI Assistant MCP Bridge
- UnityのScene・Hierarchy・Inspector・Console操作

### 確認状況
- Unity AI RelayのMCP初期化：確認済み
- Roomies_saveのUnity PID 28040への名前付きパイプ接続：確認済み
- Unity MCPツール検出：確認済み
- Unity側Editorスクリプトのコンパイル：確認済み（エラーなし）
- Unity Bridgeの待受パイプ生成：確認済み
- Codex再起動後のUnity MCPツール表示：未確認
- CodexからのUnityツール実行：未確認

### 未完了・次の作業
- Codexアプリを再起動し、このプロジェクトのタスクを開き直す
- Unityに接続許可が表示された場合は`Allow`を選択する
- 再開後、Unity Console読取などの安全な操作でMCP接続を最終確認する

---

## 2026/07/21｜10回目

### 今回の変更
- 警察がプレイヤーへ気づいていない巡回中のランダム警戒行動を追加
- Point 1〜2往復中に、左右確認・後方確認・短い引き返し・巡回継続をランダム抽選する処理を追加
- Point到着時に必ず左右を確認する処理を追加
- 警戒行動中も懐中電灯の向きで紙袋所持者を発見できるよう対応
- 警察ごとに巡回速度と警戒行動のタイミングが変化するよう対応
- 警戒行動の時間・角度・巡回速度幅をInspectorから調整できるよう設定を追加

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingPolicePatrol.cs`
- 変更：`Assets/_Prefabs/Smuggling/Police_Placeholder.prefab`
- 変更：`Assets/_Scripts/Smuggling/SETUP.md`
- 変更：`PROGRESS.md`

### 重要な仕様
- Point 1〜2を往復する既存の巡回経路は維持する
- 3〜6秒ごとの抽選は、巡回継続65％、左右確認20％、後方確認10％、短い引き返し5％
- Point到着時は抽選とは別に左右確認を行う
- 警戒行動と発見判定はServer側で実行し、警察本体の回転をNetworkTransformで同期する
- 発見距離、視野角、追跡速度、逮捕判定の既存値は変更しない

### 影響範囲
- 警察AIの未発見時巡回
- 懐中電灯の向きと発見判定
- Host／Clientの警察表示同期
- 運び屋バイトの難易度
- 警察PrefabのInspector設定

### 確認状況
- C#コンパイル：確認済み（エラー0、既存警告19件）
- 警察Prefabへの初期値登録：確認済み
- Hostでのランダム警戒行動：未確認
- Clientでの警察回転同期：未確認
- 警戒行動中の発見・追跡・逮捕：未確認
- 既存のPoint 1〜2往復：変更後は未確認

### 未完了・次の作業
- Hostで左右確認・後方確認・短い引き返しが発生することを確認する
- 警戒行動中に懐中電灯へ入ると追跡が始まることを確認する
- Clientから警察の警戒回転が同じように見えることを確認する
- Point 1〜2往復と接触逮捕が維持されていることを確認する

---

## 2026/07/21｜9回目

### 今回の変更
- ユーザーの実機確認結果を進捗へ反映
- Hostで夜に渡し人・売人が継続表示されることを確認済みに更新
- Hostで紙袋所持中に警察へ接触すると逮捕されることを確認済みに更新
- Clientで夜間表示、紙袋所持、警察接触逮捕が動作することを確認済みに更新
- 警察ドアップ、暗転、500R減算、牢屋移送が動作することを確認済みに更新
- 翌朝の牢屋労働と釈放が動作することを確認済みに更新
- 闇バイト「運び屋」一式を`main`へコミットし、`origin/main`へpush

### 変更ファイル
- 変更：`PROGRESS.md`

### 重要な仕様
- 渡し人と売人は夜だけ表示され、夜の間は消えずに継続表示される
- 紙袋所持中に活動中の警察へ接触すると、Server側判定で逮捕される
- 実装コミットは`5884725 feat: 闇バイト運び屋を追加`

### 影響範囲
- 進捗記録
- 運び屋バイトの確認状況
- Gitの`main`ブランチ

### 確認状況
- Hostでの渡し人・売人の夜間継続表示：確認済み
- Hostでの警察接触逮捕：確認済み
- Clientでの夜間表示・紙袋所持・警察接触逮捕：確認済み
- 警察ドアップ・暗転：確認済み
- 500R罰金・牢屋移送：確認済み
- 翌朝の牢屋労働・釈放：確認済み
- C#コンパイル：確認済み（エラー0、既存警告19件）
- `origin/main`へのpush：確認済み（`5884725`）

### 未完了・次の作業
- 闇バイト「運び屋」の現仕様に未完了項目なし
- 本番Prefab／FBXへの差し替えは、素材確定後に行う

---

## 2026/07/21｜8回目

### 今回の変更
- 警察へ接触しても逮捕されない問題を修正
- Rigidbodyを持たない警察とCharacterControllerの組み合わせでも判定できる、Server側Collider接触チェックを追加
- 追跡中だけでなく、紙袋所持中に活動中の警察へ直接触れた場合も逮捕されるよう変更
- 接触逮捕時の診断ログを追加

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingPolicePatrol.cs`

### 重要な仕様
- 逮捕判定はServer側で行う
- 紙袋を所持しているプレイヤーと活動中の警察のCollider範囲が接触すると逮捕する
- 警察が非活動、昼、またはプレイヤーが紙袋を持っていない場合は接触しても逮捕しない

### 影響範囲
- 警察AI
- 運び屋バイトの逮捕判定
- Host／Clientのプレイヤー状態
- 罰金・牢屋移送・逮捕演出

### 確認状況
- C#コンパイル：確認済み（エラー0、既存警告19件）
- Collider構成：確認済み（警察CapsuleCollider、プレイヤーCharacterController）
- Hostでの接触逮捕：未確認
- Clientでの接触逮捕：未確認
- 罰金・牢屋移送・翌朝の労働：未確認

### 未完了・次の作業
- Playを再起動してHostで紙袋を受け取り、活動中の警察へ接触して逮捕されることを確認する
- 警察ドアップ、暗転、500R減算、牢屋移送を確認する
- Client側でも同じ接触逮捕と演出を確認する

---

## 2026/07/21｜7回目

### 今回の変更
- 夜に渡し人・売人が一瞬だけ表示されて消える原因を特定
- `GameRoom`の`DayText`へ誤って付いていた2個目の`DayManager`を削除
- `DayManager`が重複している場合や`NetworkObject`なしで誤配置された場合に、そのコンポーネントを無効化する防止処理を追加

### 変更ファイル
- 変更：`Assets/_Scenes/GameRoom.unity`
- 変更：`Assets/_Scripts/DayManager.cs`

### 重要な仕様
- `DayManager`は`NetworkObject`と同じGameObjectに1個だけ配置する
- 昼夜判定には正規の`DayManager`だけを使用する
- UIの`DayText`には`DayManager`を配置しない

### 影響範囲
- 昼夜切り替え
- 渡し人・売人の夜間表示
- 運び屋バイト
- 昼夜状態を参照する既存機能

### 確認状況
- `GameRoom`内の`DayManager`が1個だけであること：確認済み
- C#コンパイル：確認済み（エラー0、既存警告19件）
- Hostでの継続表示：未確認
- Client表示：未確認
- 既存機能：未確認

### 未完了・次の作業
- Playを停止してUnityの再コンパイル完了後、Hostを起動する
- 夜に渡し人・売人が消えずに継続表示されることを確認する
- 渡し人へEで話しかけ、紙袋を受け取れることを確認する
- Client側でも夜間表示とインタラクトを確認する

---

## 2026/07/21｜6回目

### 今回の変更
- Hostログから、夜のままなのに仮モデルが一瞬で消えることを確認
- 原因候補となった実行時Unlitシェーダー差し替えを運び屋NPCの表示経路から除去
- `CreatePrimitive`のRender Pipeline対応済み標準材質をそのまま利用し、色だけ変更する方式へ修正
- 仮NPCごとに専用Point Lightを生成して夜間の視認性を確保

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingContact.cs`

### 重要な仕様
- 仮NPCの描画シェーダーは実行時に別シェーダーへ置き換えない
- 渡し人・売人の仮モデルは専用ライトで照らす
- 昼夜処理は夜のまま維持されており、今回の問題は描画材質側として扱う

### 影響範囲
- 渡し人・売人の仮モデル描画
- 夜間照明

### 確認状況
- Hostの夜状態維持：ログで確認済み
- `active=false`が呼ばれていないこと：確認済み
- C#コンパイル：確認済み（エラー0）
- 標準材質＋専用ライトでの実画面表示：未確認
- Client表示：未確認

### 未完了・次の作業
- Playを再起動し、夜に仮NPCが継続表示されることを確認する
- 継続表示後、Eインタラクトと紙袋受け取りを確認する
- Client側でも継続表示を確認する

---

## 2026/07/21｜5回目

### 今回の変更
- 配置座標はユーザー指定を正として、高さの自動変更を行わない方針へ修正
- 既存Prefab内の描画階層に依存せず、夜にNPCルート直下へ仮人型を生成する最終フォールバックを追加
- 渡し人は水色、売人はオレンジ色のUnlit仮モデルとして夜に表示する処理を追加
- Inspectorへ本番モデルを設定した場合は、実行時フォールバックを生成しないよう対応

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingAppearance.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingContact.cs`

### 重要な仕様
- ユーザーが配置したTransform座標は実行時に変更しない
- 本番モデル未設定時は、Prefab内のRenderer状態に関係なく仮人型を実行時生成する
- 本番Prefab／FBX設定後は`SmugglingAppearance`側のモデルだけを使用する

### 影響範囲
- 渡し人・売人の仮表示
- 本番モデル差し替え
- 夜間表示

### 確認状況
- 添付Hostログの夜判定：確認済み
- 渡し人・売人の検出とRenderer有効化：確認済み
- C#コンパイル：確認済み（エラー0）
- 実行時フォールバックの画面表示：未確認
- Client表示：未確認
- 既存機能：未確認

### 未完了・次の作業
- Hostで夜にして、水色の渡し人とオレンジ色の売人が表示されることを確認する
- 表示後、渡し人へEでインタラクトできることを確認する
- Clientでも同じ位置・時間帯で表示されることを確認する

---

## 2026/07/21｜4回目

### 今回の変更
- 新しいHostログから、夜判定と渡し人・売人2体の有効化が正常に実行されていることを確認
- 仮モデルのLit材質が夜間照明で暗く見える可能性へ対処
- 本番モデル未設定時のみ、仮モデルを夜でも見えるUnlit材質へ実行時変換する処理を追加
- 夜表示時に子Rendererを明示的に有効化する処理を追加
- NPCごとのRenderer数とワールド座標を診断ログへ追加

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingAppearance.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingContact.cs`

### 重要な仕様
- Unlit材質への変換は仮モデルだけに適用し、Inspectorで設定した本番Prefab／FBXの材質には適用しない
- 夜表示ログには`renderers`と`position`を出力する

### 影響範囲
- 渡し人・売人の仮モデル表示
- 夜間の視認性
- 運び屋バイトの診断ログ

### 確認状況
- Hostの夜判定：確認済み
- 渡し人・売人の検出数：確認済み（2体）
- `VisualRoot`有効化命令：確認済み
- C#コンパイル：確認済み（エラー0）
- Unlit化後の実画面表示：未確認
- Client表示：未確認
- 本番モデル差し替え：未確認

### 未完了・次の作業
- Hostで夜へ切り替え、仮モデルが明るく表示されることを確認する
- 表示されない場合は新ログの`renderers`数と`position`を確認し、配置座標・遮蔽物を調査する
- 表示確認後、渡し人へのE操作と紙袋受け取りを確認する

---

## 2026/07/21｜3回目

### 今回の変更
- 添付されたHost実行ログを確認し、運び屋NPCの例外が出ていないことを確認
- `DayManager`から渡し人・売人へ昼夜状態を直接適用する処理を追加
- シーン内の非表示オブジェクトも含めて`SmugglingContact`を検索し、夜に確実に表示する処理へ変更
- 時間帯と検出したNPC数を確認する診断ログを追加

### 変更ファイル
- 変更：`Assets/_Scripts/DayManager.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingContact.cs`

### 重要な仕様
- 運び屋NPCの表示切り替えは`DayManager.OnTimeChanged`から直接実行する
- `DayManager`起動時にも現在の時間帯を運び屋NPCへ適用する
- Consoleへ`isNight`と検出した`contacts`数を出力する

### 影響範囲
- 昼夜切り替え
- 渡し人・売人の表示と当たり判定
- 運び屋バイト

### 確認状況
- 添付されたHostログ：確認済み（運び屋関連の例外なし）
- GameRoomの渡し人・売人Prefab配置：確認済み
- C#コンパイル：確認済み（エラー0）
- 修正後のHost表示：未確認
- Client表示：未確認
- 既存機能：未確認

### 未完了・次の作業
- Hostで夜へ切り替え、Consoleの`[Smuggling] DayManagerが時間帯を適用: isNight=True, contacts=2`を確認する
- `contacts=0`の場合はロードされているGameRoomと配置シーンの一致を調査する
- `contacts=2`でも見えない場合は各Prefabの`VisualRoot`とRenderer状態を調査する

---

## 2026/07/21｜2回目

### 今回の変更
- 夜になっても渡し人・売人が再表示されない問題へ対処
- 昼夜イベントを取り逃した場合でも、現在の`DayManager.IsNight`からNPC表示を毎フレーム復元する処理を追加
- 警察も現在の昼夜状態をServer側で再確認し、夜の活動メンバー選出を復元する処理を追加
- 夜用NPCが表示されたことを確認できる診断ログを追加

### 変更ファイル
- 変更：`Assets/_Scripts/Smuggling/SmugglingContact.cs`
- 変更：`Assets/_Scripts/Smuggling/SmugglingPolicePatrol.cs`

### 重要な仕様
- 渡し人・売人の表示は昼夜イベント通知だけに依存せず、`DayManager.IsNight`の現在値を正として復元する
- 警察の夜間有効化と1〜2体の選出はServer側で行う
- 夜に渡し人・売人が有効化されるとConsoleへ`[Smuggling] ... を夜用NPCとして表示しました`と出力する

### 影響範囲
- 渡し人・売人の表示
- 警察の夜間有効化
- 昼夜切り替え
- マルチプレイ同期

### 確認状況
- Prefab参照設定：確認済み
- GameRoomへの渡し人・売人配置：確認済み
- C#コンパイル：確認済み（エラー0）
- Hostでの再表示：修正後は未確認
- Client動作：未確認
- 既存機能：未確認

### 未完了・次の作業
- Unityのコンパイル完了後、Hostで朝から夜へ切り替えて渡し人・売人の表示を再確認する
- 表示されない場合はConsoleの`[Smuggling]`ログとHierarchy上の`VisualRoot`有効状態を確認する
- 警察を配置後、夜に1〜2体が表示・巡回することを確認する

---

## 2026/07/21｜1回目

### 今回の変更
- 夜限定の闇バイト「運び屋」の進行処理を追加
- 渡し人から紙袋を受け取り、売人へ渡して500Rを得る処理を追加
- 紙袋の中身をServer側で抽選し、成功後に本人へ表示する処理を追加
- 警察のPoint 1／Point 2巡回、懐中電灯の視界判定、追跡・逃走・逮捕処理を追加
- 逮捕時の警察ドアップ・暗転、500R罰金、牢屋移送を追加
- 翌朝に作業台をEで10回操作して釈放される仮の牢屋労働を追加
- 渡し人、売人、警察、紙袋、牢屋作業台、牢屋地点の仮Prefab生成ツールを追加
- `PlayerInteract`、`PlayerMovement`、`PlayerSpawnSync`を運び屋状態へ対応
- `Player.prefab`へ`SmugglingPlayer`を追加

### 変更ファイル
- 新規：`Assets/_Scripts/Smuggling/SmugglingInteractable.cs`
- 新規：`Assets/_Scripts/Smuggling/SmugglingContact.cs`
- 新規：`Assets/_Scripts/Smuggling/SmugglingPlayer.cs`
- 新規：`Assets/_Scripts/Smuggling/SmugglingPolicePatrol.cs`
- 新規：`Assets/_Scripts/Smuggling/SmugglingArrestOverlay.cs`
- 新規：`Assets/_Scripts/Smuggling/SmugglingJailPoint.cs`
- 新規：`Assets/_Scripts/Smuggling/SmugglingJailLabor.cs`
- 新規：`Assets/_Scripts/Smuggling/Editor/SmugglingPrefabGenerator.cs`
- 新規：`Assets/_Scripts/Smuggling/SETUP.md`
- 変更：`Assets/_Prefabs/Player.prefab`
- 変更：`Assets/_Scripts/PlayerInteract.cs`
- 変更：`Assets/_Scripts/PlayerMovement.cs`
- 変更：`Assets/_Scripts/PlayerSpawnSync.cs`
- 既存・今回の土台：`Assets/_Scripts/Smuggling/SmugglingConfig.cs`
- 既存・今回の土台：`Assets/_Scripts/Smuggling/SmugglingAppearance.cs`

### 重要な仕様
- 昼夜判定は`DayManager`が管理する
- 共同所持金は`SharedMoneyManager`、個人収支記録は`PlayerEarning`が管理する
- 運び屋の進行、紙袋所持、逮捕、牢屋労働状態は`SmugglingPlayer`が管理する
- 紙袋の中身抽選、報酬、罰金、警察AI、逮捕判定はServer側で処理する
- 渡し人と売人は夜だけ表示・操作可能になる
- 夜終了時に未配達の紙袋は失効する
- シーンに配置した警察のうち、夜ごとに1〜2体が活動する
- 警察の巡回範囲は各Prefabの子`Point 1`と`Point 2`で指定する
- 本番のPrefab／FBXは`SmugglingAppearance`の「本番モデル」で差し替える
- 牢屋労働は未定仕様のため、現在は作業台をEで10回操作する仮実装

### 影響範囲
- 運び屋バイト
- 警察AI
- プレイヤー操作
- プレイヤーの朝リスポーン
- 共同所持金と個人収支
- 昼夜切り替え
- マルチプレイ同期
- `Player.prefab`
- GameRoomへ配置するNPC・警察・牢屋関連Prefab

### 確認状況
- C#ランタイムコードの個別コンパイル：確認済み（エラー0）
- C# Editorコードの個別コンパイル：確認済み（エラー0）
- Unity Editor全体コンパイル：未確認（既存Shader Graphパッケージの`GUID`エラーで停止）
- 仮Prefab自動生成：未確認（Unity Editor全体コンパイル待ち）
- Host動作：未確認
- Client動作：未確認
- 逮捕から翌朝の牢屋労働・釈放：未確認
- 既存機能：未確認

### 必要なInspector・Scene設定
- Unityのコンパイル後、`Tools > Roomies > 闇バイト > 仮プレハブを生成・修復`を実行する
- GameRoomへ渡し人、売人、警察2体、牢屋地点、牢屋作業台を配置する
- 各警察の`Point 1`と`Point 2`を巡回させたい道の両端へ移動する
- 牢屋地点と作業台は、プレイヤーが外へ出られない牢屋内へ配置する
- 詳細は`Assets/_Scripts/Smuggling/SETUP.md`を参照する

### 未完了・次の作業
- 既存Shader Graphパッケージのコンパイル問題が解消した状態でUnity全体を再コンパイルする
- 仮Prefabが`Assets/_Prefabs/Smuggling`へ生成されることを確認する
- GameRoomへ各Prefabを配置してHostで一連の進行を確認する
- Clientで紙袋所持、警察追跡、逮捕演出、牢屋移送、報酬・罰金同期を確認する
- 警察が壁や地形へ引っ掛からないよう、実際のマップ上で巡回地点と速度を調整する
- 本番モデルへ差し替え後、位置・回転・スケールを調整する

---

最終更新: 2026-07-11
現在フェーズ: Steamマルチプレイ不具合調査

## 現在の状態

- 完了: `me_from_soso` の内容を `main` に反映、シーン遷移・スポーン構成の修正、ロビーコード検索の診断ログ追加、Steam transport設定の修正
- 作業中: なし
- 未確認: Unityプロジェクトが別インスタンスで起動中のためバッチ検証不可。2つのSteamアカウントで検索結果ログと実機接続を確認
- 問題: Steam経由の実機接続結果は未確認。Unityプロジェクトが別インスタンスで起動中のためバッチ検証不可
- TODO: 2つのSteamアカウントでMainMenuSteamから接続し、P2Pソケット生成・クライアント接続・GameRoom同期を確認
- 次にやること: ホスト側の `StartHost: True` とクライアント側の `クライアント接続成功` を確認

## 変更履歴

| ID | 内容 | 結果 | 変更ファイル | 次 |
|---|---|---|---|---|
| 2026-07-11-01 | `me_from_soso` を基準に `main` を整理 | 実装済み・未確認 | `PROGRESS.md` | Unityで動作確認 |
| 2026-07-11-02 | Steamマルチプレイのシーン遷移・スポーン構成を調査 | 調査完了 | `Assets/_Scenes/MainMenuSteam.unity`, `Assets/_Scenes/GameRoom.unity`, `Assets/_Prefabs/Player.prefab`, `Assets/_Scripts/Network/SteamLobby.cs` | Unityログで停止地点を確認し構成修正 |
| 2026-07-11-03 | GameRoomの重複NetworkManagerを削除し、スポーン地点をSpawnPoint専用構成へ修正 | 実装済み・未確認 | `Assets/_Scenes/GameRoom.unity` | 2アカウントでSteam接続とシーン同期を確認 |
| 2026-07-11-04 | JoinLobbyWithCode〜LobbyMatchList_tの結果数・ID・Code・IOFailureログを追加し、検索結果数制限を解除 | 実装済み・未確認 | `Assets/_Scripts/Network/SteamLobby.cs`, `PROGRESS.md` | 2アカウントで検索ログを確認 |
| 2026-07-11-05 | SteamNetworkingSocketsTransportへ渡す空の設定要素を除去し、起動時にも設定配列を正規化 | 実装済み・未確認 | `Assets/_Scripts/Network/SteamLobby.cs`, `Assets/_Scenes/MainMenuSteam.unity`, `PROGRESS.md` | 2アカウントでP2P接続とGameRoom同期を確認 |

---

## 2026/07/21（12回目）

### 今回の変更
- スロットの一文字切替表示を、上下の隣接絵柄が見える3段リール表示へ変更
- 記号ごとに色を分け、各リールの背面グローと中央の当選ラインを追加
- 3本同時停止をやめ、左・中央・右の順に減速して停止する演出へ変更
- 2本一致時、最後のリール停止前に`ONE MORE...`を表示する期待演出を追加
- 数字キー`1`/`2`/`3`による賭け金選択を廃止
- スロットを見ている間だけ、マウスホイールまたは左右キーで賭け金を循環変更できるように変更
- インタラクト表示へ現在の賭け金と操作方法を表示
- TMPで`</alpha>`が露出する問題を修正し、フォントに依存しないTwemoji画像（🍒・⭐・🍀・💎・7️⃣）へ変更

### 変更ファイル
- `Assets/_Scripts/Slot/SlotMachine.cs`
- `Assets/_Scripts/PlayerInteract.cs`
- `PROGRESS.md`

### 確認状況
- Unity C#コンパイル：確認済み（今回の変更によるエラー0件）
- 既存の非推奨API警告：19件（今回の変更対象外）
- Host/Clientでの実プレイ表示と入力：未確認

### 次の確認
- Hostでスロットへ照準を合わせ、ホイールで`10 R / 50 R / 100 R`が切り替わること
- `E`で3本のリールが順番に停止し、結果確定後に報酬が反映されること
- Client側でも同じ停止結果とフィーバー表示が同期すること

---

## 2026/08/03｜Codex Unity MCP設定修正

### 今回の変更
- transport情報を持たない無効化用`mcp_servers.UnityMCP`定義を削除
- `RoomiesUnity`のRelay実行ファイルとUnityプロジェクトパスを、現在のPC上に実在するパスへ更新

### 変更ファイル
- `.codex/config.toml`
- `PROGRESS.md`

### 確認状況
- TOML構文解析：確認済み（有効なMCP定義は`RoomiesUnity`のみ）
- Relay実行ファイルとUnityプロジェクトパスの存在：確認済み
- Codex CLIによるMCP起動確認：未確認（WindowsApps内の`codex.exe`起動がAccess denied）
- Codexデスクトップでのチャット再作成とUnity接続：未確認

### 変更履歴

| ID | 内容 | 結果 | 変更ファイル | 次 |
|---|---|---|---|---|
| 2026-08-03-01 | CodexのUnity MCP設定エラーを修正し、現PCのRelay・プロジェクトパスへ更新 | 実装済み・未確認 | `.codex/config.toml`, `PROGRESS.md` | CodexでRoomiesを開き直し、チャット作成とUnity接続を確認 |
