# Roomies AI開発ルール

## マルチプレイ、不正対策、パフォーマンス

- Roomiesのマルチプレイは、フレンド同士で遊ぶ招待中心の協力プレイを前提とする。
- 野良プレイヤーとの公開マッチングは現時点で想定しない。
- 悪意あるプレイヤーを前提にした本格的なアンチチートや、まれな不正だけを防ぐための複雑な仕組みは標準では追加しない。
- Null確認、配列や金額の範囲確認、所有者確認、Serverだけが共有状態を変更する処理など、負荷がほぼない基本検証は維持してよい。
- 共同口座、家賃、報酬、日付進行、所有状態、二重決済など、通常プレイでも壊れやすい重要な整合性はServer側で守る。
- 不正対策だけを目的とした高頻度RPC、毎フレームの距離検証、重複したPhysics判定、全体検索、過剰なログ出力は避ける。
- 通常のフレンドプレイで再現するグリッチや進行不能は修正する。悪意ある操作でのみ成立する軽微なグリッチは、性能や保守性を犠牲にしてまで対策しない。
- 不正対策の追加によって処理負荷、通信量、入力遅延、実装の複雑さが増える場合は、実装前に必要性と影響を説明する。
- 判断に迷う場合は、厳密な不正耐性よりもゲームの軽さ、反応の良さ、分かりやすさ、フレンド同士の遊びやすさを優先する。

## プレハブと仮アセット

見た目のアセットはAIで仮置きし、あとで本物に差し替える前提で作る。差し替えのときにスクリプト・通信・当たり判定を壊さないための決まり。

### 2種類に分ける
- **背景・環境**（床、壁、建物、街並み、空、植木、看板など、触っても何も起きない見た目だけの物）
  - シーンや `Assets/_Prefabs/Environment/` のプレハブに直接置いてよい。
  - 本アセットが来たら丸ごと置き換えてよい。
  - スクリプトを付けない。付けてよいのは歩くための床・壁のコライダーだけ。
- **ギミック付きオブジェクト**（スロット、ブラックジャック台、配達箱、クローゼット、鉱石、NPC、ドアが開く冷蔵庫・棚・ロッカー・引き出しなど、触る・動く・開く・お金や通信が絡む物）
  - 必ずプレハブにし、シーンにはそのプレハブを置く（シーン上で直接作り込まない）。
  - 置き場所は `Assets/_Prefabs/`。用途別のフォルダ（`Facilities/`、`Mining/`、`Smuggling/` など）に入れる。

### ギミック付きプレハブの組み立て方
- **ルート**には動きに関わるものを全部置く。スクリプト、NetworkObject、操作・判定用のコライダー、Rigidbody などが対象。
- **見た目は子オブジェクト `Visual` の下にだけ置く**。モデル、マテリアル、見た目だけのパーティクルなどが対象。
  - `Visual` の中にスクリプトを置かない。操作・トリガーなど判定に使うコライダーも置かない。見た目の形に合わせた「ぶつかるだけ」のコライダーは、見た目と一緒に替わるので `Visual` の中に置いてよい。
  - 見た目の差し替えは「`Visual` の中身を入れ替えるだけ」で終わる状態を保つ。ルートや他の子は触らずに済むようにする。
  - 仮の見た目（プリミティブ、AIの仮アセット）は `Visual/Placeholder` にまとめる。本物を入れるときは `Placeholder` を消し、本物を `Visual` の下に置く。
  - 見た目違い（通常・レアなど）は `Visual/Normal`、`Visual/Rare` のように並べる。スクリプトは表示の切り替えだけ行う。
- **ギミックが使う位置は `Visual` の外に置く**。画面、座る位置、持つ位置、ボタン、エフェクトの出る位置などのこと。
  - `Anchor_Screen`、`Anchor_Seat` のように、`Anchor_` で始まる名前付きの空オブジェクトにする。
  - 見た目を替えたときは、アンカーの位置を合わせ直すだけで済むようにする。
  - スクリプトは見た目の部品（メッシュ名など）を直接探さず、アンカーやインスペクターの参照を使う。
- **動く部品（ドア、ふた、引き出しなど）**は、回転・移動の軸をギミック側に置く。
  - 軸は `Pivot_Door`、`Pivot_Drawer` のような名前の空オブジェクトにし、ルート直下（`Visual` の外）に置く。蝶番の位置に合わせる。
  - その部品の見た目は、軸の子の `Visual` に入れる（例: `Pivot_Door/Visual`）。
  - スクリプトやアニメーションは軸（`Pivot_〇〇`）だけを動かす。本アセットに替えても、軸の中の見た目を入れ替えて蝶番に合わせるだけで開き方はそのまま動く。
- **基準の向き:** ルートの位置は足元（床面）、正面は +Z。本アセットはこれに合わせて大きさ・向きを調整し、ルートは動かさない。
- 同じギミックで見た目だけ違う物は Prefab Variant で作る。ロジックは元プレハブ1つにまとめる。

### 既存プレハブの扱い
- 2026/10/21 に既存のギミック付きプレハブを `Visual` の形へそろえた。手順はメニュー「Roomies/Prefabs/Apply Visual Convention」（`RoomiesPrefabConventionFixer`、何度実行しても同じ結果）。
- NPC（Smuggling）の `Visual/ModelRoot` は本物のモデルを入れる場所として残している（`SmugglingAppearance` が参照）。
- `Player.prefab` と見た目だけのアセット（`Resources/RoomiesArt/` のライブラリ、`Nomal_Box` などの箱モデル）は対象外。
- 家具（`NetworkFurniture`）は実行時に見た目を `Visual` という名前の子として作るので、もともとこの形になっている。

## 実行時に作らない（エディタで全部触れる形にする）

最終的には人がUnityを触って作り込む。**ゲームを実行したときだけ現れる物を作らない。**

### 決まり
- **見た目・当たり判定・配置を、スクリプトから新規生成しない。**シーンかプレハブに実体を置く。
  位置・大きさ・向き・マテリアル・文言は、すべてインスペクターとシーンビューで直せる状態にする。
- 実行時のスクリプトがやってよいのは、**置いてある物に対する操作だけ**。
  表示の切り替え（`SetActive`）、値の反映、アニメ、マテリアルの差し替えなど。
  `new GameObject(...)`、`GameObject.CreatePrimitive(...)`、`Instantiate` で見た目を新しく作らない。
  （ネットワークで動的に増える物＝プレイヤー、鉱石、家具、配達物のように、
  **数が実行時に決まる物だけ**例外。その場合も必ずプレハブを `Instantiate` する）
- 並べるのが大変な物は**エディタ拡張（`Roomies/〜` メニュー）で生成**し、シーンに保存して実体として残す。
  生成コードは「置くための道具」であって、実行時には動かさない。
- スクリプトが参照する物は `[SerializeField]` でインスペクターから割り当てる。名前で探し回らない。
- 数値（速度・金額・秒数・サイズなど）はコードに直書きせず、`[SerializeField]` にして
  インスペクターから調整できるようにする。

### 守れているかの確認
- Play Modeに入らずに、シーンビューだけで最終的な見た目が確認できるか。
- 本アセットに差し替えるとき、スクリプトを読まずにオブジェクトを入れ替えるだけで済むか。
- 位置や数値を変えたいとき、コードを書き換えずにインスペクターで直せるか。

### 実体化の進み具合

**済み**（シーンに置いてある。配置ツールで置き直せる）
- 朝の演出・今日の収支・家賃の支払い … `Roomies/UI/Build Day Screens`
- 夜イベントのバナー … `Roomies/UI/Build Night Banner`（`NightEventManager` と
  `FurnitureEditController` も一緒に置く）
- みんなの稼ぎ … `Roomies/UI/Build Earning List`
- HUD … `Roomies/UI/Build HUD`
- メニューとロビーの見た目 … `Roomies/UI/Build Menu`（MainMenuSteamシーン）
- ポーズ・設定・キー設定 … `Roomies/UI/Build Pause Menu`
- ゲームオーバー画面 / 逮捕演出 / セーブの「続きから」ボタン
- ベッド・睡眠の管理・今日のおねがい … `Roomies/Night/Place Beds In Open Scene`
- 部屋の小物・明かり・画面の仕上げ（2026/11/13） … `Roomies/Art/Restyle/Build Room`
  （タイトルの背景は `Build Title Room`、ベッドと冷蔵庫の見た目は `Restyle Night Prefabs`）。
  シーンの「Roomies_RoomRestyle」を作り直す。手で直した位置は作り直すと消えるので、
  直したら配置ツール（`RoomiesRoomRestyleBuilder`）の値もそろえること
- HUD・タイトルの色とフォント … `Roomies/UI/Restyle Game UI` / `Restyle Title UI`（置いてある物を作り直さずに整える）
- 素材のつや・質感・照り返し・影・明暗（2026/11/13） … `Roomies/Art/Restyle/Material Finish/1 Matte Surfaces`・`2 Surface Detail`・`3 Light Bounce`・
  `4 Contact Shadows`・`5 Light Balance`（4・5 も作り直しのあとに自動でかけ直す）
  （`RoomiesMaterialFinishBuilder`）。値は表で持つ。1と2の素材の部分は Build Room などの作り直しのあとに自動でかけ直す。
  デカール「Roomies_Decals」と照り返し・反射プローブ「Roomies_LightBounce」は、GameRoom とタイトルのそれぞれで 2・3 を実行して置く
- 質感のテンプレマテリアル（2026/11/13） … `Roomies/Art/Restyle/Material Templates/1 Build Templates`・`2 Apply To Room And Title`
  （`RoomiesMaterialTemplates.cs`）。テンプレ（親）は `Assets/_Materials/Templates/T_*.mat`、色は子マテリアル（マテリアルバリアント）で上書きする。
  質感を直すときは親を直す。子の名前は元のマテリアルと同じにする（足音 `FootstepAudio` がマテリアル名の言葉で床を判定するため）
  ギミック付きプレハブは `3 Apply To Gimmick Prefabs`（Visual の中だけ）。町は `4 Apply To Town`（部品の名前で型を決める。鉱山 `Roomies_Mining` は触らない）。
  町のベベルは `Bevel/Apply To Town`
- 角の丸み（ベベル、2026/11/13） … `Roomies/Art/Restyle/Bevel/Apply To Room And Title`・`Apply To Gimmick Prefabs`（`RoomiesBevelBuilder`）。
  部品ごとの強・中・弱は表で決める（同じ家具でも天板は中・脚は弱）。メッシュは `Assets/_Art/Restyle/BevelMeshes.asset`。
  `Preview Tiers` で強さを色で見られる（保存されない）。部屋の分は Build Room のあとに自動でかけ直す
- 朝と夜の明かり（2026/11/13） … `Roomies/Art/Restyle/Material Finish/6 Time Of Day Lighting`。GameRoom の「Roomies_TimeOfDay」の
  `RoomTimeOfDayLighting` が DayManager の IsNight を読んで、ランプの強さと画面の仕上げ（夜／朝の Volume の重み）を切り替える。
  強さはインスペクターで直せる（`5 Light Balance` を実行すると夜の値をそろえ直す）
- 町の作り直し（2026/11/13） … `Roomies/Art/Town/1 Windows And Walls`・`2 Houses And Roofs`・`3 Street And Night Lights`・`9 Combine For Speed`
  （`RoomiesTownRebuild*.cs`、素材は `RoomiesTownMaterials.cs`）。寸法と色はコードの表。町のプレハブ `RoomiesNeighborhood` の
  「Town facades / houses / rooftops / street props / night lights」と、GameRoom の遠景 `Roomies_Backdrop` を作り直す。鉱山は触らない。
  1〜3 と遠景の `Build Distant Backdrop` は最後に `9` を呼び、見た目だけの部品を素材ごとに1つのメッシュへまとめる
  （`Assets/_Art/Restyle/Town/Combined/`。各グループの「Combined」の下）。**部品を直すときは表を直して 1〜3 を実行し直す**
  （まとめたメッシュは手で直さない）。夜の明かりは名前の頭（`Street accent`・`Shop light`・`Porch light`）で朝に消える。
  まとめたあとに `Material Templates/4 Apply To Town` や `Bevel/Apply To Town` をかけ直すと、まとめた物の素材が変わることがあるので、かけ直したら 1〜3 も実行し直す
- 町の自然物（2026/11/13） … `Roomies/Art/Nature/1 Trees`（木のプレハブ `Assets/_Prefabs/Environment/Nature/Tree_*` と町の庭の木）・
  `3 Ground And Grass`（町の地面 pPlane1 の素材と、GameRoom の草の束「Roomies_Grass」）。遠景の木・山・境界の外の起伏のある地面は
  `Roomies/Environment/Build Distant Backdrop` が作る（`RoomiesNature*.cs` を呼ぶ。乱数の順は前のまま）。木と草は LODGroup で遠いと消える。
  町の地面の当たり判定（pPlane1 の平らな箱）は変えていない。草の束・木・境界の外の地面に当たり判定はない。素材は URP Lit（自作の手続きテクスチャ）
  葉と草の揺れは `4 Wind On Leaves And Grass`（シェーダー `Roomies/Foliage Lit`。強さ・速さ・止まる距離は素材のインスペクター）
- 作り直し2（2026/11/13） … `Roomies/Art/Rebuild/1 Jail`・`2 Main House`・`3 Casino`・`4 Road`（`RoomiesTownRebuild.*.cs`）。
  元の部品の当たり判定・スクリプト・移送先などは残し、見た目だけを外して新しく組む（外した部品は当たり判定だけの空の物として残っている）。
  （スロットとブラックジャック台の見た目は 2026/11/23 に作り直した。下の「カジノ」を見ること。以下は 11/13 時点の説明）
  スロットの筐体は Slot.prefab の `Visual/Cabinet`（`Rebuild/3b Slot Cabinet`。Visual は伸ばして回した当たり判定の箱で、
  `SlotMachine.interactionCollider` が参照しているので残し、Cabinet で伸ばしを打ち消している）、ブラックジャック台はプレハブの `Visual/Art`。
  メインの家の古い階段の見た目（Demo/pCube12）は表示だけ切っている（当たり判定は使っている）。
  確認は Play Mode で `Town/Walk - Enter GameRoom As Host` → `Walk Check`・`Gimmick Check`・`Perf - Measure In Game`
- 作り直し3（2026/11/13 夜） … 同じメニュー（`1 Jail`・`2 Main House`・`3 Casino`）で、建物として0から作る形に置き換えた。
  カジノは x -1.2〜16.8・z -19.6〜-7.8 の建物（入口は開け放した大きな扉と、シーンの DoorPivot の通用口）。
  留置所は x 0.8〜12.45・z 13.2〜22.65（待合室・面会の廊下・留置室。裏の z 22.65〜24 は取引の NPC への通り道）。
  メインの家は外階段を正面の壁に沿った形に変えた（古い段の当たり判定と Demo/pCube12 の坂の当たり判定は外した）。

- 町の改善（2026/11/14） … 遊べる範囲の外の囲い `Roomies/Art/Town/4 Enclosure`（`RoomiesTownEnclosure.cs`、町のプレハブの「Town enclosure」）、
  大通りの歩道橋 `5 Footbridge`（「Town footbridge」。確認は `Footbridge Check (Play Mode)`）、遠くの霧 `Roomies/Art/Restyle/Material Finish/7 Distance Fog`
  （朝夜の色は `RoomTimeOfDayLighting` の `morningFogColor`・`nightFogColor`、距離はシーンのライティング設定）、牢屋の作業台の見た目
  `Roomies/Art/Rebuild/5 Jail Workbench`（プレハブ `JailLabor_Placeholder` の `Visual/Workbench`）。高架・電車・観覧車・遠景のビルは
  `Roomies/Environment/Build Distant Backdrop`（`RoomiesBackdropBuilder.Railway/Ferris/OuterTown.cs`）。
  `9 Combine For Speed` がまとめるのは、上の町の作り直しのグループに加えて、留置所・メインの家・カジノ・道路・囲い・歩道橋（コードの `PrefabGroups`）
- 部屋の小物のローポリ（2026/11/23） … 缶・瓶・観葉植物・電飾と吊りランプの電球・ピザと箱は、`Build Room` / `Build Title Room` が
  1 つの形のメッシュで置く（`RoomiesRoomRestyleBuilder.Props.cs`。メッシュは `RestyleMeshes.asset` の `prop_*`）。色はアトラス
  `Assets/_Art/Restyle/Props/props_atlas.png` とマテリアル `Assets/_Materials/Props/props_atlas.mat` を使い回す。
  ラベル・雑誌の表紙・メモ・紙くず・ピザの具・葉の元の絵はプロジェクト直下の `ArtSource/Props/`（Unity に取り込まない）にあり、
  Build Room が切り出してアトラスに組む（`RoomiesRoomRestyleBuilder.PropsAtlas.cs` の表。説明は `ArtSource/Props/README.md`）。
  ネコのネオンは `Textures/Neon` の絵を貼った板（マテリアル `props_neon_cat`）。冷蔵庫の扉のメモは `Restyle Night Prefabs` が置く。
  床・壁・ソファ・段ボール・ラグの描いたテクスチャ（色とノーマル）は `Assets/_Art/Restyle/Textures/Tiles/`。床と壁は家のプレハブの部品なので
  `Material Finish/2 Surface Detail` が元の素材（Restyle の floor_planks・wall_plaster）に貼り、ソファ・段ボール・ラグはテンプレの子
  （`RoomiesMaterialTemplates.cs` の `Tiles`）に貼る。色は「元の色 ÷ 絵の平均の色」をかけて部屋の配色にそろえる。
  同じ角度の写真と数の確認は `RoomiesPropsCheck`（バッチ用）
- 配達トラックと箱の値札（2026/11/13） … `Roomies/Delivery/1 Build Price Tags`・`2 Build Truck Prefab`・`3 Place Truck In Open Scene`
  （`RoomiesDeliveryTruckBuilder`）。トラックは `Assets/_Prefabs/Delivery/DeliveryTruck.prefab`。値段・締め切り・乗る距離は `GameBalance`。
  通し確認は Play Mode で `Town/Walk - Enter GameRoom As Host` → `Roomies/Delivery/Play Test`

- カジノ（2026/11/23。3 階建て・木とレンガと暖色の明かりの見た目） … `Roomies/Casino/1 Build Building`（＝`Roomies/Art/Rebuild/3 Casino`）・
  `2 Build Gimmick Prefabs`・`3 Place In Open Scene`・`Stats`（数を数えるだけ）。コードは `RoomiesTownRebuild.Casino*.cs`（建物）・`RoomiesCasinoBuilder*.cs`（プレハブと配置）・
  `RoomiesCasinoArt*.cs`（形と色）。
  建物は町のプレハブの「Town casino」（外壁・床の板・屋根・外観と 1 階の中）、「Town casino 2F」「Town casino 3F」（上の階の中。その階にいる人の画面でだけ出す）、
  「Town casino 2F windows」「3F windows」（外から見た窓の明かり。その階にいないときだけ出す。プレハブでは隠してある）。
  出し入れは GameRoom の「Roomies_Casino/FloorVisibility」の `CasinoFloorVisibility`。床の上面は 1 階 0.118・2 階 4.9・3 階 8.8、笠木 13.4。
  `Rebuild/3 Casino` は建物だけを作り直し、まとめ直すのもカジノの 3 グループだけ。位置や寸法を直すときはコードの数字を直して実行し直す（シーンで直した位置は消える）。
  壁・床・梁は町の素材（`town_casino_*`。レンガの床と敷物は `Textures/Tiles` の `floor_brick`・`carpet_suits`）の箱。
  小物とギミックの見た目は「物 1 つ = メッシュ 1 つ」（`Assets/_Art/Casino/CasinoMeshes.asset`）で、色と絵はアトラス `Assets/_Art/Casino/casino_atlas.png`
  （マテリアルは `Assets/_Materials/Casino/` の `casino_atlas`・`casino_atlas_glow`・`casino_neon`・`casino_window_glass`）。
  元の絵（台の枠線・ルーレットの盤・スロットの飾り・垂れ幕・ネオン）はプロジェクト直下の `ArtSource/Casino/`（取り込まない。`RoomiesCasinoArt.cs` の表で切り出す）。
  ギミックは `Assets/_Prefabs/Casino/`：`CasinoElevator`（箱は動かさず、扉を閉めている間に中の人を行き先の階へ移す）、`CasinoClerk`（鍵を売る店の人。Bob とは別のキャラ）、
  `RouletteTable`（3 階は Variant の `_3F`）、上の階の台の Variant（`Slot_2F`・`BlackjackTable_2F`・`BlackjackTable_3F`。数値は `RoomiesCasinoBuilder.Rates.cs` の表）。
  前からある `Slot.prefab`・`BlackjackTable.prefab` も、ルートに仕組み・`Visual` に見た目（スロットの画面は `Anchor_Screen`）の形に組み直した（`RoomiesCasinoBuilder.Tables.cs`）。
  新しいゲームは `CasinoGame` を継ぐ（`PlayerInteract` は、見ている物の親にこの型があれば、案内・E・R・ホイールを渡すだけ）。
  鍵は `CasinoAccess`（GameRoom に 1 つ。値段は `GameBalance`、買った数と挿した数はセーブに残る）。
  建物の箱のうち、模様のいらない物（腰の板・足元の石・壁の布・光る板）は `Swatch("色")` でアトラスの色見本を使う（町の素材を増やさない。建物のマテリアルは 10 個）。
  ブラックジャック：カード・チップ・点数の札は台の上の実物（`BlackjackTableView` が動かす。`Pivot_Card_〇`・`Pivot_Chips_〇`・`Anchor_Score_〇`。置くのは `RoomiesCasinoBuilder.BlackjackCards.cs`）。
  カードの並びは `BlackjackTable` が NetworkVariable で全員に配る。上のモニター `Anchor_Display` は賭け金と結果だけの補助（`BlackjackTable.EditorBuildUI()` が置く。実行時には作らない）。
  ハイ＆ロー：カードとチップも台の上の実物（`HiLoTableView` が動かす。`Pivot_Card_〇`・`Pivot_Chips_〇`・`Anchor_Deck`・`Anchor_Row`・`Anchor_Tray`。置くのは `RoomiesCasinoBuilder.HiLoCards.cs`。
  トランプの絵とマテリアルはブラックジャックと同じ物）。カードの並びは `HiLoTable` が全員に配る。立て札 `Anchor_Odds` は上と下の倍率だけの補助。コマ送りの確認は `Roomies/Casino/Demo/Capture HiLo Frames`。
  スロット：画面の部品は `Anchor_Screen` の Canvas の中、筐体のランプは `Anchor_Lamps`（置くのは `RoomiesCasinoBuilder.SlotScreen.cs`）。演出は `SlotScreen`（見た目と音だけ）、
  当たり外れと配当は `SlotMachine`。演出の長さ（リーチ・当たり）は `SlotMachine`・`SlotScreen` のインスペクターにあり、サーバーも同じ長さだけ次の回を待つ。
  スロットの画面の絵とトランプの絵は `ArtSource/Casino/Screen/make_casino_screen_art.py` が作る（`codex/` に絵を置くと差し替わる。説明は同じフォルダの README.md）。
  トランプの絵は 4096×2048 に 11 列 × 5 段（番号 = スート × 13 + 数字 − 1、52 が裏面、53 がチップの模様）。札の角は、絵ではなく板の形で丸く切る（`RoomiesCasinoArt.Cards.cs` の `CardCorner`）。
  飾りのチップは `lp.ChipStack`（1 枚ずつ積む）、動くチップの上の面は `Art.ChipTop`（トランプと同じマテリアル）。大きい円（台・受け皿・盤）は、辺の数を多めに指定する（16 以上は自動では増えない）。
  動きの確認は Play Mode で `Roomies/Casino/Demo/Capture Slot Frames`・`Capture Blackjack Frames`（コマ送りの写真を `Temp/RoomiesCasino/frames/` に書く）。
  音は `Assets/Resources/Audio/casino/`（計算で作る。`ArtSource/Casino/Audio/make_casino_sfx.py`）。
  形の決まり（2026/11/24）：カジノの小物・ギミックは、角ばった箱のままにしない。丸みは形を作る道具（`RoomiesCasinoArt.Lp`）がまとめて付ける
  （箱の角は短い辺の 22%・上限 2.5cm、丸い物は辺の数を倍、天板や座面は上の角を丸める）。椅子やソファのようなやわらかい物は `lp.Loft`・`lp.Cushion` でふくらみと絞りを付ける。
  伸ばして使う箱や、角のままにしたい物だけ `lp.Sharp = true`。
  外観の飾り（丸い塔・ひさし・アーチ・段々の冠・電球の列）は `RoomiesCasinoArt.Facade.cs`。レンガや塗り壁にしたい形は、置いたあとで町の素材に替える（`PropMat`）。
  NPC の会話：吹き出しは `NpcSpeechBubble`（プレハブの `Anchor_Bubble`。サーバーがせりふを決めて全員に出す）。店の人は `CasinoClerk`（せりふはインスペクターの `smallTalk`）、
  警察は `PoliceTalk` と `SmugglingPolicePatrol` の「声かけ」（配置ツール `Roomies/NPC/Build Police Talk`）。確認は Play Mode で `Roomies/Casino/Demo/NPC Talk Check`。
  `2 Build Gimmick Prefabs` を実行したら、続けて `3 Place In Open Scene` も実行する（階ごとの出し入れの参照をつなぎ直すため）。
  通し確認は Play Mode で `Town/Walk - Enter GameRoom As Host` → `Roomies/Casino/Check (Play Mode)`。
  2 人での確認は `RoomiesCasinoNetTest.cs`（Multiplayer Play Mode の Player 2 を `Roomies/Casino/Net Test/Activate Player 2` で起こし、
  `Temp/RoomiesCasino/net/run.flag` を置いて Play Mode に入る。その間だけ同じ PC の中でつなぐ。結果は同じフォルダの `host.txt`・`client.txt`。ふだんは何もしない）

- コンビニ（2026/11/24。CORNER STORE。平屋の、中に入れる店） … `Roomies/Store/1 Build Building`（建物。町のプレハブの「Town store」。`RoomiesTownRebuild.Store.cs`）・
  `2 Build Gimmick Prefabs`（`Assets/_Prefabs/Store/` の `StoreShop`・`StoreDoor`・`StoreClerk`。`RoomiesCasinoBuilder.Store.cs`）・`3 Place In Open Scene`（2 のあとに実行）。
  形と色は `RoomiesCasinoArt.Store.cs`（カジノと同じ道具・アトラス・マテリアル。店の色はさんご色・クリーム・墨色。実在の店の色・ロゴはまねしない）。
  絵は `ArtSource/Store/`（Codex の 24 枚。説明は同じフォルダの README.md）。ポスター 2 枚だけは専用のテクスチャとマテリアル（`Assets/_Art/Store/store_posters.png`・`Assets/_Materials/Store/store_posters.mat`）。
  前の店（2 階建ての箱）は、町の作り直しの表から外した（`RoomiesTownRebuild.StoreBuiltSeparately`）。シーンの Demo/pCube1 は表示だけ切っている。
  買える物は「食材 1 回分」だけ（`StoreShop`）。値段と決まりは冷蔵庫の前で補充するときと同じ（`NightFridge.StockCost`・`CurrentStockBlock`・`ServerStockBought`）。
  店の側には値段を持たない。棚を見て E でカゴへ（R で戻す）→ レジで E（お会計・支払う・袋を受け取る）。カゴと段階はサーバーが決めて全員に配り、動きは RPC。
  自動ドアは `StoreAutoDoor`（サーバーが人の位置だけを見て開け閉めを決める。とびらに当たり判定はない）。店の人は `CasinoClerk`（鍵を売らない設定）と、
  レジの `StoreShop.clerkBubble` が同じ吹き出しで話す。棚にたくさん並ぶ商品は、形を作る道具の軽い形（`lp.Lite`）で作る。店の中の物は影を落とさない。
  音は `Assets/Resources/Audio/store/`（`ArtSource/Store/Audio/make_store_sfx.py`）。通し確認は Play Mode で `Town/Walk - Enter GameRoom As Host` → `Roomies/Store/Check (Play Mode)`
  レジ打ちのバイト（2026/11/24）：朝だけ。`StoreShop.Job.cs`（StoreShop を partial にして、同じレジを店員の側から使う）。タイムカード `Pick_TimeCard` で始め、R で終える。
  お客さんは NPC だけ（`Pivot_Customer`）。バイト代の数字は `GameBalance` の storeJob〇〇、理由は `SharedMoneyReason.StoreWage`。お客さん用の値段は `StoreShop.kindPrices`（プレイヤーの買い物には使わない）。
  確認は `Roomies/Store/Job Check (Play Mode)`

- 部屋の PC（2026/11/25） … `Roomies/PC/1 Build Prefab`・`2 Place In Open Scene`・`3 Build Desktop UI`（`RoomiesPcBuilder.cs`）。
  机の見た目は部屋の背景（Build Room の PC desk）。触る所はプレハブ `Assets/_Prefabs/Room/RoomComputer.prefab`（`RoomComputer`、HomeAppliance）。
  画面は GameRoomUI の `RoomComputerUI`（`GameUIRefs.ComputerUI`）。開いている間は `RoomComputerUI.IsOpen` で、その人の移動・操作・Esc メニュー・P キーが止まる。
  家具ショップのアプリは前からの注文（`DayManager.BuyFurnitureServerRpc`）を使い、結果は `DayManager.FurniturePurchaseResult` で受け取る。
  P キーのショップと O キーの効果の一覧は 11/25 に消した（`FurnitureEditController` はカタログを持つだけ）。効果の一覧は「家具の効果」アプリ。
  机のモニターの待ち受け・壁紙・アイコン・起動のしるしは Codex の絵（`ArtSource/Room/pc/make_pc_art.py` で取り込む）。
  家具ショップのカードと家具の効果の行の絵（家具 12 種）は `ArtSource/Room/pc/make_furniture_art.py` で取り込む（家具の id で引く。絵が無い家具は色の板と頭の字）。
  株のニュースは `GameBalance.stockNewsShowSteps` 回の値動きまで出す。
  株トレードは `StockMarket`（GameRoom に 1 つ。値段はホストが決めて配る。調整値と銘柄は `GameBalance` の stock〇〇。町の出来事との連動は `ServerNudge`）。
  確認は `Roomies/PC/Check (Play Mode)`

組み立てのコードは各クラスの `EditorBuildUI()` に `#if UNITY_EDITOR` で残してある。
実行中は動かないので、置き直したいときだけ配置ツールから呼ぶ。
参照は `[SerializeField]` でシーンに保存される。

ポーズ画面だけは、ボタンの反応（onClick）がシーンに保存できないため、
組み立て（`EditorBuildUI`）と実行時の結び直し（`WireUI`）の2つがある。
**部品の名前がこの2つの約束**なので、名前を変えるときは両方そろえること。

**まだ**（新しく同じ作り方をしない。触るついでに実体化する）
- ロビーの参加者の行（`LobbyUIManager.CreatePlayerRow`）。人数が実行時に決まるので
  生成自体は避けられないが、**シーンに置いた見本をコピーする形**にしたい
- `NetworkFurniture` の `Visual`、`CharacterCloset` の見た目、`MiningFeedback` の演出
- ポーズ中の背景ぼかしは、いまの画面を写したものなので実行時のままでよい
- `UITheme` は部品を作る道具なので、**エディタの配置ツールから呼ぶぶんには問題ない**
  （実行時に呼ばないこと）

## ゲーム中のUIは別シーン（GameRoomUI）

- ゲーム中のUI（HUD、ポーズ、朝・収支・家賃・夜バナー、みんなの稼ぎ、ゲームオーバー、逮捕演出、EventSystem）は
  `Assets/_Scenes/GameRoomUI.unity` に置く。マップ（GameRoom）には置かない。タイトルのメニューは MainMenuSteam のまま。
- GameRoom の `GameUISceneLoader` が読み込む。ネットワーク中はホストが NGO の SceneManager で Additive に読み込み、
  全員に1回ずつ届く（クライアントは自分で読まない）。ほかのシーンへ Single で移ると一緒に外れる。
- UIシーンには NetworkObject・ネットワークの処理を置かない。
- マップ側がUIを使うときは `GameUIRefs`（UIシーンの「GameUI」）から受け取る：`GameUIRefs.WhenReady(...)`。
  UI側からマップへは今までどおりシングルトン（`DayManager.Instance` など）を使う。シーンをまたぐインスペクター参照は保存できない。
- 新しいゲーム中UIを足したら、GameRoomUI に置き、マップから使うものは `GameUIRefs` に参照を足す。
  GameRoom 側にUIのルートができてしまったら、メニュー「Roomies/UI/Split GameRoom UI」で移せる（何度実行しても同じ結果）。
- 配置ツール（Build HUD / Build Day Screens など）は GameRoom と GameRoomUI を両方開いて実行する。

## コミットメッセージ

### 形
```
<type>(<範囲>): <何をしたか・30字以内>

- 箇条書き（1行1つ・短く）
- 箇条書き
- 箇条書き

確認: <通ったこと> / <未確認のこと>
```

### 1行目
- `<type>` は次から選ぶ：
  `feat`（機能追加）／`fix`（不具合修正）／`art`（見た目・アセット）／`refactor`（動作は変えない整理）／
  `perf`（速度・負荷）／`docs`（ドキュメント・ルール）／`chore`（設定・雑務）
- `<範囲>` はどこを触ったか：`mining` `night` `lobby` `slot` `delivery` `furniture` `audio` `ui` `network` など。
  広い範囲なら省略してよい
- 「〜を追加」「〜を修正」まで書く。30字以内に収める

### 本文
- **短い箇条書きだけ**にする。「看板変更」「音量調整」くらいの粒度でよい
- 1行に1つ。長い説明や経緯は書かない（詳細は `PROGRESS.md` に書く）
- 最後に `確認:` を1行入れ、通ったことと**未確認のこと**を正直に書く

### 例
```
feat(mining): 採掘場を閉鎖して入口にシャッターを置く

- 実行時生成の禁止ルールを追加
- MiningSite の実行時生成を削除、closedGate 参照に変更
- 配置ツール MiningClosedGateBuilder を追加
- 入口の看板変更
- 効果音9本の .meta 追加

確認: コンパイルエラー0 / Play Mode未実施
```

## PROGRESS.md

- コード、仕様、設定を変更した作業の終了時は、`PROGRESS.md`の先頭へ今回の記録を追記する。
- 過去の記録を削除・上書きしない。
- 未確認の処理を確認済み・完成と記載しない。
