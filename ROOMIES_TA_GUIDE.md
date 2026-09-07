# Roomies: TA向け編集ガイド

## シーンで触る場所

- `Assets/_Scenes/GameRoom.unity` → `Roomies_TA_UI`。ESC、昼夜切り替え、収支、家賃、夜イベント、ゲーム終了、家具ショップ、HUDを編集できます。もともと別の親にあったコントローラは元の配置を維持しています。
- 各UIのCanvas内でRectTransform、Image、TMP Textを変更してください。起動時は保存済みの表示を再利用し、イベント接続と表示状態だけ初期化します。
- ESCのMain / Settings / KeyBindings / ConfirmationはCanvasGroupで切り替えます。編集時はOverlayを有効にし、見たいグループとOverlayのAlphaを1にしてください。再生時に閉じた状態へ戻ります。
- 収支・TABランキングには5人分の行を配置済みです。名前と金額はプレイ時に更新されます。
- `MainMenuSteam.unity` → `Roomies_TA_Menu` と既存のCanvas。メニュー・設定のレイアウトを保存できます。
- クローゼットの `CharacterClosetCanvas`、ブラックジャックの `RuntimeDisplay`、スロットの既存Canvasも配置済みです。

## Inspectorで調整するもの

- **DayManager**：昼夜の時間、家賃、昼夜の光量、各UIの参照。
- **FurnitureEditController / Catalog**：価格・表示名・見た目Prefab・効果・効果量。起動時に既定の効果へ戻す処理は修正済みです。ネットワークでは項目の順番を共有するため、同じプロジェクト内容で遊んでください。
- **NightEventManager**：電気代・食費・家賃値上げの金額範囲。
- **CashRegister / MiningSite / SlotMachine / BlackjackTable**：仕事・ゲームの設定、表示と音の参照。
- **プレイヤーPrefab / MiningPlayer**：つるはしPrefab、一人称・他プレイヤー視点の位置とサイズ、ヘルメットライト。ライトはカメラの子に配置済みです。
- **Assets/Resources/RoomiesAudio/Audio rig.prefab**：Music Volume、Pause Music Volume、3つのAudioSource、Sounds内のBGM・効果音クリップ。idはゲームから呼ぶ名前なので、音だけ差し替える場合はidを維持してください。BootSceneにも配置しています。
- **Assets/Resources/RoomiesArt**：家具・鉱石・道具のPrefab、マテリアル、テクスチャ。
- **Assets/RoomiesAuthoring**：UIの角丸・グラデーション・マテリアルとフォント補完。シーン保存後やUnity再起動後も表示資源が残ります。

## 動的なもの

接続したプレイヤー、購入した家具、鉱石、つるはしはネットワークや装備状態に合わせて生成します。編集元はPrefabです。人数に応じたロビー名札、採掘の粒子・ガス、一時的な音・収支ポップアップなどの短命な表示は動的な処理が残っています。

## 確認メニュー

- `Roomies > TA > Prepare editable scene UI`：未配置のUIを補完する編集用メニュー。既存のUIは作り直しません。
- `Roomies > TA > Validate authored UI`：シーンを読み直し、Missing Script・UI参照・ESCボタンの接続数を検査します。レポートは `Temp/RoomiesArt/ta-validation.txt`。

Unity AIのAccount API・ライセンス通信など、Unityアカウント／外部サービス由来の警告はゲームコードとは別です。Play Mode・5人通信の実動作はこの静的検査だけでは保証していません。

2026-09-07の検査：コンパイル警告・エラー0件。Console履歴を退避後、3シーンを再読み込みした検査で新規警告・エラー0件。Missing Script 0件、ESC 26ボタンのコールバックが各1件であることを確認。記録は `ArtReview/2026-09-07`。
