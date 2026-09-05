# Roomies 背景・アセット制作（2026-09-05）

ブランチ: `codex/astra-debug-20260905`

## 反映した内容

- GameRoomの既存FBX、配達ゾーン、施設、インタラクトを維持して背景を追加。
- 家: 屋根、窓、カーテン、床、リビング、寝具、生活小物、看板、玄関階段。出現位置を天井の下に変更し5地点を用意。
- 街: 歩道、横断歩道、街灯、ベンチ、ごみ箱、植栽、遠景の住宅。
- 施設: 店舗・仕事場の窓、庇、看板、配達場所の縁取り、ゲームコーナー。
- 購入家具12種を専用メッシュPrefabに置換。カタログの順番・価格・効果は変更なし。明示設定されたPrefabを優先し、未設定時はIDから共通のResourcesアセットを参照。
- 小物・設備17種を制作（うち1種は既存スロットの外装）。メッシュはEditorで焼き出し、共有URPマテリアルを使用。実行時にメッシュを組み立てない。
- 取引NPC2種と警察の外観は既存Bobモデルを再利用。元のCollider、NetworkObject、取引・巡回スクリプトと参照を維持。
- 太陽光の元の強度0を修正。DayManagerの既存の昼夜同期に連動し朝1.2・夜0.12、室内灯・街路灯を追加。停電イベントが既存のLight無効化処理で消灯できる構成。

## ファイル

- `Assets/_Scenes/GameRoom.unity`: 反映済みシーン。
- `Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab`: 背景Prefab。子の小物はPrefab参照を維持。
- `Assets/Resources/RoomiesArt/Furniture`: 購入家具12種。
- `Assets/Resources/RoomiesArt/Props`: 生活小物・設備17種。
- `Assets/Resources/RoomiesArt/Meshes`, `Materials`: 保存済みのメッシュ・共通マテリアル。
- `ArtReview/2026-09-05`: Unityから直接レンダリングした確認画像と検査結果。

## 検証範囲

Unity 6000.4.7f1でコンパイル確認。家具12種のメッシュ・マテリアル・Collider・サイズ整合性・入れ子のNetworkObject不在を検査。Player.prefabのCharacterController寸法で5地点の出現位置の重なりを検査。NPC3種のモデル認識・仮モデル非表示・Collider保持、および背景のMissing Scriptを検査。

画像はEditorのCamera.Renderによるもの。Play Modeの移動、階段昇降、家具購入、NPC取引、昼夜遷移、複数PCの通信と描画性能は今回未検証。静的に配置した室内家具・小物には購入効果や物理持ち運びを追加していない。購入して届く家具は既存のNetworkFurnitureの持ち運び・効果を使用。

タイトル画面の背景、NPCの専用アニメーション、紙袋・作業台など今回対象外の小物は別途仕上げ対象。

## 再制作

通常は保存済みPrefabを編集するだけでよい。再生成時はUnityのメニューを使用する。

1. `Roomies/Art/Build Prop Library` — 保存済みの家具・小物を生成定義から更新する。手編集した同名アセットは上書きされる。
2. `Roomies/Art/Install Environment` — 未配置のシーンにだけ配置する。既存の背景ルートがある場合は変更しない。
3. `Roomies/Art/Finish Existing NPCs and Slots` — 外観を接続。既存の接続を再利用する。
4. `Roomies/Art/Finalize and Validate Environment` — 背景Prefabと照明を保存し、検査・画像を書き出す。GameRoomの未保存編集がある場合は先に保存する。
5. `Roomies/Polish/Repair Slot and Build Jail` — スロットの当たり判定参照と盤面を更新。背景に牢屋が未配置の場合、家の隣に建設し納品区画をその隣へ移す。既存の牢屋配置は維持する。

HUDの見た目は `Roomies/Polish/Render HUD Review` で1920×1080の確認画像を生成できる。画像の所持金・時間は確認用の固定値であり、実プレイの記録ではない。

画像書き出しに使う一時カメラは必ず破棄される。画像は `Temp/RoomiesArt` に生成される。
