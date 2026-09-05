# Roomies 現状確認と今回の修正

対象：`codex/astra-debug-20260905`、開始点 `523d7be`。
進捗・家具TODO・家具カタログ案・各SETUP・夜間レビューを読み、実装とScene/Prefabの保存内容を照合した。古い進捗は再実装の指示として扱わない。

## 現在の構成

- Unity 6000.4.7f1、URP 17.4.0、Input System 1.19.0、NGO 2.11.2、Steamworks.NET＋SteamNetworkingSocketsTransport。
- `BootScene` → `MainMenuSteam` → `GameRoom`。ゲーム開始はHostのNetworkSceneManagerが担当。BootSceneがEditorのPlay開始Sceneにも設定されている。
- PlayerはOwner AuthorityのNetworkTransform。移動、加減速、ジャンプ、ダッシュ、しゃがみ、控えめなカメラ演出、持つ／落とす／投げるを既存コンポーネントで実装済み。
- DayManagerは朝夜各180秒、3日ごとの家賃500＋ランダム上乗せ、連番の日付、夜の収支、支払い、翌朝リスポーンを管理する。
- SharedMoneyManagerが共有口座の入出金をServerで確定。PlayerEarningは個人の貢献・収支記録であり、独立した買い物用財布ではない。
- 配送は物理箱、通常／レア箱、納品報酬、破損罰金。運び屋は夜の受け渡し、警察、逮捕、牢屋労働。スロットとブラックジャックも既存。
- 家具は12品目のコードカタログを基本とし、SceneのFurnitureEditControllerから構成可能。購入、運搬、回転、設置、移動速度／毎朝収入の効果が存在する。
- 夜イベントは平和、電気代、食費、停電、地震、家賃値上げ。主にDayManagerのRPC経由で演出を配信する。
- UIはUGUI＋TMP。メニューのScene参照をMenuThemerが整え、HUD、収支、家賃、夜イベント、ESC、家具ショップなどは大部分が実行時生成。既存のUIThemeを維持する。
- NetworkFurniture、Player、配送箱のNetworkPrefab登録が存在する。GameRoomに家具配達地点が存在する。ScriptableObjectはNetworkPrefabsList、RenderPipeline、Volume、フォント等が中心で、家具商品はシリアライズ可能なFurnitureItemを使用する。

## 今回修正した問題

### ロビー・切断・再参加

- 開始直後にBusyを立て、リレー準備中の開始連打とUI更新による開始ボタン再有効化を防ぐ。
- 開始用コルーチンを保持し、退出時に停止する。作成・検索コールバックもキャンセルする。
- 退出後に届いた入室通知が、閉じたセッションを復活させないよう入室先を照合する。
- 入室時点でゲーム開始済みなら、追加のLobbyDataUpdateを待たず接続を開始する。
- Host開始失敗・Sceneロード開始失敗を検出してUIへ理由を通知する。
- Host退出、接続失敗、手動退出でNetwork callbackを解除し、NGOのShutdown完了後にメニューへ戻る。固定の2フレーム／0.2秒待ちは使わない。
- Clientの切断処理はLocalClientIdだけに反応する。Host離脱のSteamフラグはビットマスクとして判定する。
- 接続中・失敗メッセージを表示し、非表示のJoinPanelへ解散メッセージが埋もれる経路を修正する。

### 家具購入

- Clientは商品indexだけを送信し、価格・配達座標・全注文共通の配置順をServerが決定する。
- 昼、夜終了演出中、Game Over、未準備のプレイヤー、未登録／不正Prefab、無効な商品、残高不足を拒否する。
- 見た目PrefabへのNetworkObject混入を拒否する。生成できた注文だけ支払い、失敗した生成物は片付ける。
- 同期した遷移中フラグによってClient側のショップも閉じる。
- HostではRPC応答が同フレームに来るため、注文中表示をRPCより先に出して成功表示の上書きを防ぐ。
- 配達地点未設定時は購入を拒否する。GameRoomの既存地点を使い、Sceneへの追加作業は不要。

### 配送と日付同期

- 初期箱をStartではなくOnNetworkSpawnで生成する。Scene内でStartが先に実行された場合にも配送を始められる。
- 子ColliderがUntaggedでもDeliveryBoxのルートを認識する。
- 複数Colliderを箱単位で追跡し、最後のColliderがエリアを出るまで納品可能にする。無効／破棄済みColliderを除去する。
- DayManagerの購読をOnNetworkSpawn/OnNetworkDespawnへ合わせ、同期済み初期値をUI・空へ反映する。Despawn時に進行コルーチンを停止する。

## 検証

- UnityでC#コンパイル成功（修正・テスト追加を含めエラー0、警告0。最終チェックはPROGRESS参照）。
- `_Scenes` と `_Prefabs` の直接のMonoScript GUID参照をプロジェクト／PackageCacheのスクリプトと照合し、未解決0件。Prefabの全fileID・物理配置を検証したという意味ではない。
- 回帰テスト8件を `Assets/Tests/Editor/RoomiesRegressionTests.cs` に追加。既存Assembly-CSharpを動かさず、テストからReflectionで参照する。
- Unity Test Runnerによる検出で8件すべてRunnableを確認。テスト結果がPassedになったという意味ではない。
- テスト対象：3日周期、家賃成功とDAY 4、家賃失敗、購入可能時間、連続注文と残高不足、Prefab欠落、初期配送箱、複数の子Collider。
- テストはHostのみのメモリ内TransportでServerロジックを検証する設計。Steam接続や外部通信はしない。演出の待ち時間はスキップするため、見た目や実時間の同期の検証とは別。
- 自動テスト実行はUnitySkillsのAutoモードにより `MODE_FORBIDDEN`。`test_run_by_name` のAllowlist登録をユーザーへ依頼済み。**実行結果は未確認**。
- 二つのSteamアカウントでの接続、Client同期、実機再接続、1ゲームの通しプレイ、UI実表示、ビルドは今回未確認。

## 次に優先すること

1. 回帰テストを実行し、Host/Clientで開始連打・開始中退出・Host終了・再参加を確認する。
2. 現SceneはSteamロビー上限4人、開始下限1人。今回の1〜5人という仕様へ合わせるには上限設定と5人のスポーン／UI確認が必要。
3. DAY 1〜4、家賃成功／失敗、夜イベント中の購入と配送を複数端末で通し確認する。
4. Game Overは現在日数表示とタイマー停止が中心。全体の操作制限・リトライ導線は追加候補。
5. 家具の多くは仮Cubeで効果なし。カタログ案の説明と実際の効果を揃え、少数の家具から本番モデル・反応を完成させる。
6. 食費が個人の収支記録を減らすだけの現仕様、サイクル難易度上昇、残り時間の高頻度同期、途中参加の夜演出状態を検討する。
7. 家具PC、清掃等の新しい仕事、生活小物、UIのPrefab参照化は既存Managerを拡張して進める。

この作業では新しいネットワークライブラリ、代替Manager、未完成の大量Prefabを導入していない。
