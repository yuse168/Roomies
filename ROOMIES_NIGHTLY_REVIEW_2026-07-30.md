# Roomies 夜間 自動レビュー・改善レポート

実施日：2026/07/30
対象：Unity 6000.4.7f1 / URP / Netcode for GameObjects 2.11.2

## 自動修正したもの

### マルチプレイと不正操作対策

- `CarryableObject`
  - RPC送信者とプレイヤー所有者が一致するかServer側で検証
  - 拾う・持つ・落とす位置をServer側で距離検証
  - NaN・Infinityを含む座標や回転を拒否
  - 所持中のClientが切断した場合に箱を自動解放
  - 同時取得に負けたClientが「持っている状態」のまま固まる競合を修正
- `DoorInteract`、`DeliveryButton`、`SlotMachine`、`BlackjackTable`
  - 遠距離からRPCだけを呼んで操作できないようServer側の距離判定を追加
- `DayManager`
  - Clientから任意に日付を進められるRPCを廃止し、Server内部処理へ変更
  - 朝・夜の長さをInspectorで変更可能なまま、GameRoomの初期値を各180秒へ統一
  - 製品版で日付スキップ用デバッグキーが動かないよう制限
- `SharedMoneyManager`
  - 製品版で共同口座のデバッグ加算キーが動かないよう制限

### ゲームプレイの安定化

- `DeliveryZone`
  - 箱のColliderが子Objectにある場合も納品判定できるよう修正
  - Despawn済み・破棄済みの箱参照を自動除去
  - 箱のスポーン高さと散らばり幅をInspectorへ公開
- `FurnitureCatalog` / `FurnitureEditController`
  - ショップ画面だけInspector設定、Serverと設置家具はハードコード値、という不整合を修正
  - Scene上の家具カタログをServer・`NetworkFurniture`と共有
  - 家具Prefab、価格、効果値をInspectorから調整可能
- `CharacterCloset` / `EscMenuUI`
  - Escapeでクローゼットを閉じた同じフレームにESCメニューまで開く競合を修正
- Unity 6.4で非推奨になった検索APIと`GetInstanceID`利用を整理

## 自動追加したゲーム体験改善

### 納品結果のゲーム内フィードバック

- 変更ファイル：`Assets/_Scripts/Delivery/DeliveryButton.cs`
- 発生条件：納品ボタンを押して成功または失敗した時
- 成功時：`納品成功！ +¥○○ 共同口座へ`を既存のゲーム内バナーで表示
- 失敗時：箱不足、参照不足、報酬反映失敗などの理由を本人へ表示
- Inspector設定：
  - `Show Result Banner`：演出全体のON/OFF
  - `Announce Success To Everyone`：成功を全員へ出すか、本人だけに出すか
  - `Server Interact Distance`：納品可能距離

### 持ち運び競合の分かりやすさ

- 変更ファイル：`Assets/_Scripts/PlayerInteract.cs`
- ほかのプレイヤーが持っている箱には「ほかの人が運搬中」と表示
- 同時に拾おうとしてServerに拒否された場合、約0.75秒以内にローカル状態を復旧

### 配送箱スポーンの調整項目

- 変更ファイル：`Assets/_Scripts/Delivery/DeliveryZone.cs`
- `Spawn Jitter`と`Spawn Height`をInspectorへ追加
- 既存のスポーン方式を維持しながら、Sceneに合わせて見た目を調整可能

## 発見したバグ

### Critical

- 今回の静的確認・BootからメニューまでのPlay確認では、進行不能になるCriticalバグは新たに確認されませんでした。

### High

- 修正済み：Clientが日付進行RPCを直接呼べる
- 修正済み：持ち運びRPCで他人名義・遠距離座標・不正座標をServerが受け入れる
- 修正済み：持ち運び中のClient切断で箱の所持状態が残る
- 修正済み：家具ショップのInspector設定とServer側の実際の価格・効果が一致しない

### Medium

- 修正済み：ドア、納品、スロット、ブラックジャックを遠距離からRPC操作できる
- 修正済み：DeliveryBoxの子Colliderが納品エリアに入っても検知できない
- 修正済み：Despawn済みDeliveryBoxが納品エリアのリストに残る
- 修正済み：製品版で共同口座加算・日付スキップのデバッグキーが残る
- 未修正：`DayManager.remainingTime`を高頻度で`NetworkVariable`へ書いており、参加人数や通信状況によっては不要な同期量が増える
- 未確認：Steam切断・再接続、Host終了、Client離脱中に今回の所持解除が全ケースで期待通り動くか

### Low

- 修正済み：クローゼットをEscapeで閉じた直後にESCメニューが開くことがある
- 修正済み：Unity 6.4の非推奨API警告
- 未修正：Build中のPackage内部Hidden Shader検証で3件のエラー表示候補がある
  - `Hidden/ProbeVolume/VoxelizeScene`
  - `Hidden/Core/DebugOccluder`
  - `Hidden/URP/FallbackLoading`
  - Assets内の独自Shaderではなく、Unity/URP Package側のため今回は変更していません。

## ゆせがUnityでやること

- [ ] GameRoomの`FurnitureEditController`を選び、家具Prefab・価格・効果値・並び順を確認する
- [ ] 家具カタログの各Prefabが`NetworkFurniture`を持ち、NetworkPrefab登録済みか最終確認する
- [ ] `CarryableObject`の`Server Pickup Distance`と`Server Max Held Distance`を実際の腕の長さに合わせる
- [ ] ドア・納品・スロット・ブラックジャックの`Server Interact Distance`を各施設の大きさに合わせる
- [ ] `DeliveryZone`の`Spawn Point`、`Spawn Jitter`、`Spawn Height`をSceneビューで調整する
- [ ] HostとClientで同じ箱を同時に拾い、負けた側が正常復帰するか確認する
- [ ] 箱を持ったClientを切断し、箱が落ちて別のプレイヤーが拾えるか確認する
- [ ] 納品成功バナーを全員表示・本人のみ表示の両方で確認する
- [ ] 朝夜180秒、家賃支払い、DAY 4以降の進行をHost/Clientで通し確認する
- [ ] Steamなし、Steamあり、Host終了、Client離脱、再入室を実ビルドで確認する
- [ ] 製品ビルド前に起動ロゴのWindowsシステムフォントを配布可能なOFLフォントへ差し替える

## Unity Editorから触りにくい部分

以下は見た目をコードで生成しており、SceneビューやPrefab Modeだけでは調整しにくい状態です。

- `DayTransitionUI`
- `DayResultUI`
- `RentPaymentUI`
- `NightEventUI`
- `PlayerEarningListUI`
- `EscMenuUI`
- `BlackjackTable`のワールドUI
- `FurnitureEditController`のショップUI
- `CharacterCloset`のカスタマイズUI

推奨は、ロジックは現在のScriptへ残し、見た目だけをPrefab化してInspector参照にする構成です。未設定時だけ既存のコード生成へフォールバックさせれば、Scene破損を避けながら段階的に置き換えられます。

## 改善候補

1. `remainingTime`を毎フレーム同期せず、Server時刻とターン終了時刻だけ同期してClient側で補間する
2. 実行時生成UIをPrefab化し、文字・余白・色・アニメーションをUnity上で直接調整可能にする
3. Steam/NGOの終了処理を1か所へ集約し、Host終了・Client離脱・アプリ終了の順序を統一する
4. 共同口座、家賃、日付進行、持ち運び競合のEditMode/PlayModeテストを追加する
5. ボタン・施設へ共通のホバー音、決定音、失敗音のAudioEventを用意する
6. キーボード・マウス・Controllerで同じ選択状態が見える共通UIフォーカス処理を作る

## Roomies改善アイデア

- 共同口座が増減した時、金額だけでなく原因を短く表示する
  - 例：`+¥300 納品`、`-¥500 逮捕`
- 誰かが高額家具を買おうとした時、ほかのプレイヤーに短い相談スタンプを出せる
- 夜の残り30秒だけ部屋の時計音や照明を少し変え、自然に焦りを作る
- 家賃支払い前日の朝に、冷蔵庫やテレビへ「あと¥○○」の生活感あるメモを表示する
- 施設ごとに小さな成功リアクションを追加する
  - スロット：コイン跳ね
  - 納品：箱のスタンプ
  - 清掃：汚れが気持ちよく消える
  - 闇バイト：紙袋を受け取った瞬間だけ緊張音

## 次回自動実装候補

1. `DayManager`のターン残り時間同期を低頻度化
2. `EscMenuUI`または`DayTransitionUI`をPrefab参照方式へ移行
3. 持ち運び・共同口座・日付進行のPlayModeテスト追加
4. Steam終了・離脱処理の呼び出し経路を一本化
5. 施設共通の成功・失敗AudioEventとInspector設定を追加

## 確認結果

- C#コンパイル：確認済み（0 errors / 0 warnings）
- BootScene missing script / missing reference：確認済み（0件）
- Prefab missing script：確認済み（0件）
- 2048px超過Texture：確認済み（0件）
- Build Settings：確認済み（BootScene 0 / MainMenuSteam 1 / GameRoom 2）
- Unity Play：BootScene開始を確認済み
- ロゴ後の遷移：MainMenuSteam表示を確認済み
- Play停止：確認済み
- Host実プレイ：未確認
- Client実プレイ：未確認
- Steamロビー・再接続：未確認
- 実ビルド：未確認

## 備考

- 現在の作業ツリーには、この夜間レビュー以前からのUI・Scene・Font変更が多数残っています。今回の修正では、それらを削除・巻き戻ししていません。
- `git diff --check`は既存の`Assets/_Scenes/MainMenuSteam.unity` 6380行目に末尾空白を1件検出しました。今回触った箇所ではないため自動修正していません。
