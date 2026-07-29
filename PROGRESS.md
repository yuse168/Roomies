# PROGRESS.md

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
