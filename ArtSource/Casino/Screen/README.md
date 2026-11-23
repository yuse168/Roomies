# スロットの画面とトランプの絵

`make_casino_screen_art.py` が、スロットの画面の部品（`Assets/_Art/Casino/Slot/*.png`）と、トランプ 52 枚＋裏面を 1 枚にした絵
（`Assets/_Art/Casino/casino_cards.png`）を作る。プロジェクトの直下で：

```
python ArtSource/Casino/Screen/make_casino_screen_art.py
```

そのあと Unity で `Roomies/Casino/2 Build Gimmick Prefabs` → `3 Place In Open Scene` を実行する。

## 描いた絵に差し替える

このフォルダに `codex/` を作り、下の名前で絵を置くと、コードで描いた絵の代わりにそれを使う（無い物はコードで描く）。
数字札（A〜10）・スートの形・文字のロゴ（BIG WIN・JACKPOT・FEVER・REACH!・WIN・LUCKY ROOM）は、いつもコードで描く。

| ファイル名 | サイズ | 用途 |
|---|---|---|
| `slot_sym_cherry.png` `slot_sym_bell.png` `slot_sym_bar.png` `slot_sym_crown.png` `slot_sym_seven.png` | 512×512・透明 | シンボル。この順が `SlotMachine` の `slotSymbols`（配当の低い順）と同じ |
| `slot_bg_normal.png` `slot_bg_fever.png` | 768×1120 | 画面の背景（ふだん／フィーバー中） |
| `slot_fx_sunburst.png` | 1024×1024・透明・白 | 大当たりの後ろで回る光 |
| `slot_fx_coin.png` `slot_fx_sparkle.png` | 256×256・透明 | コイン、きらめき |
| `card_back.png` | 500×700 | トランプの裏面 |
| `card_court_j.png` `card_court_q.png` `card_court_k.png` | 500×700・透明 | 絵札の人物（1 色。赤と黒はコードで付ける） |

フォントは、プロジェクトに入っている Bangers・Anton・Roboto（`Assets/TextMesh Pro/Examples & Extras/Fonts/`）を使っている。

## トランプの絵の並び

2048×1024 に、幅 146・高さ 256 のマスを 14 列 × 4 段。1〜13 列が A〜K、段は上から ♠♥♦♣。14 列目のいちばん上が裏面。
`BlackjackTableView` が、表の板に出すマスを切り替える（マテリアルは `casino_cards` 1 つ）。
