# 効果音の置き場

`GameAudio.Play("<このフォルダからの相対パス>")` で鳴る。
**Inspector配線は不要。ここへ正しい名前で置けば鳴る。**

拡張子は `.wav` 推奨（`.mp3` でも動く）。

## 第1段階（基礎）のファイル名

| 置き場所 | 用途 | 鳴るタイミング |
|---|---|---|
| `player/footstep_1` `_2` | 歩きの足音 | 接地して一定距離進むたび（既定1.55m）。連番でランダムに鳴り分ける。増やすなら `_3` 以降を足す |
| `drop/light` `drop/medium` `drop/heavy` | 物が落ちてぶつかる | 運んでいた物が着地・衝突した瞬間。`CarryableObject.weightLevel` で鳴り分ける（1=light / 2〜3=medium / 4以上=heavy）。勢いで音量、重さでピッチが変わる |
| `player/jump` | ジャンプ | 踏み切った瞬間 |
| `player/land` | 着地 | 落下速度が一定以上で接地した瞬間 |
| `ui/confirm` | 決定 | ボタン押下 |
| `ui/cancel` | キャンセル・戻る | 戻る／閉じる系のボタン |
| `money/gain` | 入金 | 共同口座が増えたとき |
| `money/spend` | 出金 | 共同口座が減ったとき |

## 第2段階（夜イベント・日付・スロット・配達）

コード側は配線済み。ファイルを置けばそのまま鳴る。

| 置き場所 | 用途 | 鳴るタイミング |
|---|---|---|
| `night/trouble` | 夜イベント（悪いこと） | 電気代・食費・停電・地震・家賃値上げ・空き巣・冷蔵庫故障・騒音・小包ハズレ |
| `night/lucky` | 夜イベント（良いこと） | 宝くじ・大家の気まぐれ・落とし物・小包アタリ |
| `night/peace` | しずかな夜 | 何も起きない夜の告知 |
| `day/morning` | 朝になる | 夜→朝の切り替え |
| `day/night` | 夜になる | 朝→夜の切り替え |
| `slot/spin` | スロット回転 | 筐体の位置から（距離減衰あり） |
| `slot/stop` | リール停止 | リール3つが止まるたび |
| `slot/win` | スロット当たり | WIN のとき |
| `slot/lose` | スロットはずれ | MISS のとき |
| `slot/fever` | FEVER当たり | FEVER WIN のとき |
| `slot/reel` | リール回転中 | 回っている間（筐体の位置から） |
| `delivery/complete` | 納品成功 | 配達ボタンの成功時 |
| `blackjack/win` `blackjack/lose` | BJの勝ち負け | 結果が出た瞬間 |
| `blackjack/push` `blackjack/blackjack` | 引き分け・BJ | 同上 |
| `blackjack/deal` | カードを配る | 配牌のたび |
| `door/open` `door/close` | ドアの開け閉め | ドアを操作したとき |
| `furniture/bounce` | トランポリン | 上に乗って跳ねたとき |
| `furniture/pickup` `furniture/place` | 家具を持つ・置く | 模様替えで持ち上げ／設置したとき |
| `ui/logo` | タイトルロゴ | メインメニューを開いたとき |
| `ui/lobby` | ロビーの決定 | ロビー画面のボタン（メニューと別の音） |
| `ui/emote` | エモート | エモートを出したとき |
| `rent/paid` `rent/failed` | 家賃の成否 | 家賃日の精算結果 |

**無音のキーは残っていない**（2026/10/23時点）。`GameAudio` が引くキーはすべて
このフォルダにファイルがある。新しいキーを足すときは
`Roomies/Audio/Rebuild Sfx Levels` で音量表（`SfxLevels.asset`）にも行を足す。

## カジノ（`casino/`）

計算で作った音（録音や外部の素材は使っていない）。作り直すときは、プロジェクト直下で
`python ArtSource/Casino/Audio/make_casino_sfx.py`（数字を直して実行し直す。何度実行しても同じ音）。

| 置き場所 | 用途 | 鳴るタイミング |
|---|---|---|
| `casino/elevator_close` `casino/elevator_open` | エレベーターの扉 | 扉が閉まりはじめる／開きはじめるとき（箱の位置から） |
| `casino/elevator_move` | 動いている間のうなり | 扉が閉まりきってから着くまで（約 2.5 秒） |
| `casino/elevator_ding` | 到着の「チン」 | 着いて扉が開きはじめるとき |
| `casino/roulette_spin` | 玉が転がる | 盤が回っている間（約 6 秒。`RouletteTable.spinSeconds` に合わせた長さ） |
| `casino/roulette_stop` | 玉が止まる | 結果が出た瞬間 |
| `casino/elevator_button` | 階のボタンを押す | エレベーターのボタン・鍵穴を押したとき（前は `ui/confirm` を仮に使っていた） |
| `casino/key_sold` | 鍵を受け取る | 店の人から鍵を買えたとき（前は `ui/confirm`） |
| `casino/talk` | NPC が話しはじめる | 店の人・警察の吹き出しが出るとき（前は `ui/emote`） |
| `casino/chip` | チップを置く | ルーレットに賭けたとき（賭けた場所から） |
| `casino/card` | カードをめくる | ハイ＆ローで次のカードが出たとき |
| `casino/dice` | サイコロがどんぶりで転がる | チンチロでふるたび |
| `casino/win` `casino/lose` | 当たり・外れ | ルーレット（自分の結果だけ）・ハイ＆ロー・チンチロ |
| `casino/cash` | 手持ちを受け取る | ハイ＆ローで F を押したとき |
| `casino/chips_slide` | チップの山が台の上を滑ってくる | ブラックジャック・ハイ＆ローで、賭け金を置く・払い戻し・負けて下がるとき |
| `casino/slot_start` | レバーを引く | スロットを回しはじめたとき（筐体の位置から） |
| `casino/slot_reel` | リールが回っている間 | 回っている間、0.9 秒ごとに頭から鳴らし直す（長さ 0.9 秒。最後が頭へつながる音） |
| `casino/slot_stopa` `casino/slot_stopb` `casino/slot_stopc` | リールが止まる | 左・中・右のリールが止まるとき（右ほど高い） |
| `casino/slot_reach` | リーチ | 2 つそろって、最後のリールがゆっくり止まりはじめたとき |
| `casino/slot_reach_miss` | リーチが外れる | リーチのあとで、そろわなかったとき |
| `casino/slot_lose` | 外れ | そろわなかったとき（毎回鳴るので控えめ） |
| `casino/slot_win_small` `casino/slot_win_mid` | 小当たり・中当たり | 当たりの大きさに合わせて |
| `casino/slot_win_big` | 大当たり | BAR・王冠など大きい当たり（約 2.1 秒のファンファーレ） |
| `casino/slot_jackpot` | 7 ぞろい | ジャックポット（約 3.4 秒） |
| `casino/slot_coin` | コイン 1 枚 | 当たりでコインが噴き出すたび |
| `casino/slot_tick` | 数字が回る | 払い出しの数字が 1 目盛り進むたび |

## コンビニ（`store/`）

計算で作った音（録音や外部の素材は使っていない）。作り直すときは、プロジェクト直下で
`python ArtSource/Store/Audio/make_store_sfx.py`。入店のチャイムはオリジナルの 2 音（実在の店のメロディーはまねしない）。

| 置き場所 | 用途 | 鳴るタイミング |
|---|---|---|
| `store/door_chime` | 入店のチャイム | 自動ドアが開いたとき |
| `store/door_slide` | 自動ドアが動く | 開くとき・閉まるとき |
| `store/pick` `store/putback` | 商品を手に取る・戻す | 棚を見て E・R |
| `store/basket_full` | カゴがいっぱい | 3 つ目を入れたとき |
| `store/putdown` | カウンターに置く | お会計で商品が並ぶとき |
| `store/scan` | 読み取り（ピッ） | 商品が読み取りの台を通るとき |
| `store/tick` | 金額が回る | 1 つ読み取って、金額が止まったとき |
| `store/register` `store/coins` `store/receipt` | 引き出し・おつり・レシート | 支払ったとき |
| `store/bag` | レジ袋 | 袋に入れる・袋が滑ってくる・受け取るとき |

## 足音の材質ごとの違い

足音の素材は1つのまま、`Resources/Audio/FootstepSurfaces.asset` で
**床のマテリアル名ごとにピッチと音量**を変えている（木・芝・金属など）。
足音が鳴るたびに足元へ短いレイを1本飛ばし、当たったマテリアル名で引く。
重ならないよう、足音は専用の再生口を使い回す（`GameAudio.PlayExclusive`）。

NPC（警察・売人・運び屋）も同じ仕組みで足音が鳴る（`NpcMotionFeedback`）。

## BGM（`Assets/_Audio/Music/`）

BGMはこのフォルダではなく `Assets/_Audio/Music/` に置き、`GameMusic` プレハブの
インスペクターから割り当てる（実行時に読み込まない＝AGENTS.mdの「実行時に作らない」に合わせた）。

| ファイル | 流れる場面 |
|---|---|
| `menu.mp3` | メインメニュー・ロビー |
| `day.mp3` | 朝〜日没 |
| `night.mp3` | 夜の自由行動 |

差し替えは `GameMusic` プレハブの `Menu Track` / `Day Track` / `Night Track` を入れ替えるだけ。
音量は同じプレハブの `Menu Volume` などで曲ごとに調整する（設定画面のBGM音量がさらに掛かる）。

## 逮捕・ゲームオーバー

| 置き場所 | 用途 | 鳴るタイミング |
|---|---|---|
| `police/siren` | パトカーのサイレン | 逮捕演出の始まり |
| `police/arrest` | 手錠 | 白フラッシュと同時 |
| `day/gameover` | ゲームオーバー | 家賃を払えずゲームオーバーになった瞬間 |
| `train/pass` | 遠景の電車 | 高架を電車が通りかかったとき（線路の中ほどから鳴る） |

## 第3段階（採掘）＝ アップデート待ちで保留

採掘場は `MiningSite.serviceClosed` で閉鎖中（リリース後のアップデートで開放予定）。
`Assets/_Audio/Mining/` にある4つは `RoomiesMiningBuilder.Tone()` が生成した
サイン波＋ノイズのプレースホルダで、同じものを `Resources/Audio/mining/` へ複製して
無音を無くしている。開放するときに本物へ差し替える。

| 置き場所 | 用途 |
|---|---|
| `mining/hit` | つるはしが当たる |
| `mining/break` | 鉱石が砕ける |
| `mining/sale` | 売却 |
| `mining/warning` | 落盤の警告 |

## 連番バリエーションの規則

`key_1` `key_2` … と1から連番で置くと、`Play("key")` が毎回ランダムに1つ選ぶ。
**連番は途中で飛ばさない**（`_3` が無いと `_4` 以降は読まれない）。
単発で良い音は連番なしで `key` という名前のまま置く。

## 出典の記録

Audiostock（定額制エンタープライズプラン）から取得したものは
`CREDITS.md` に作品番号を残す。ライセンス確認と差し替えの追跡用。
