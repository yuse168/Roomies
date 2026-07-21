# PROGRESS.md

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
