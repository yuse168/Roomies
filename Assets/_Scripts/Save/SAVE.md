# セーブ（続きから）

ホストの端末に部屋の進行を1枠だけ保存し、「続きから」で再開できるようにする仕組み。

## 形

- セーブを持つのは**ホストだけ**。参加者は何も保存しない。
- 保存するのは**部屋の進行**（誰が参加していたかは条件にしない）。
  そのため、**1人で続きを始めても、友達を呼んで始めても**同じ続きから遊べる。
- 枠は1つ。進むたびに上書きする（オートセーブ）。手動セーブは無い。

## ファイル

```
%USERPROFILE%/AppData/LocalLow/<会社名>/Roomies/roomies_save.json
```

`SaveSystem.FilePath` が場所を持つ。Unityからは `Roomies/Save/Open Save Folder` で開ける。
書き込みは一時ファイル→置き換えなので、保存中に落ちても前のセーブは壊れない。

## 保存される内容

| 項目 | 備考 |
|---|---|
| Day・朝/夜・残り秒 | 残り秒はそのターンの途中から再開するため |
| 共同口座の残高 | |
| 次回家賃への上乗せ額 | 夜イベントで積み上がった分 |
| 置いてある家具 | カタログ番号と位置・向き |
| 参加者の所持金 | **名前で**覚える。同じ名前の人が入ってきたら戻す |

保存**しない**もの：誰が参加していたか、持っていた物、配達や密輸の途中経過、
ゲームオーバー後の状態（負けた瞬間は保存しないので、続きは負ける直前の状態になる）。

## 流れ

1. メインメニューでセーブがあると「続きから」ボタンが出る（`MainMenuManager`）
2. 押すと `SaveSystem.PendingLoad` に読み込んだデータが入り、そのままホストを作る
3. GameRoomの `RunSaveManager` がそれを部屋へ反映し、`PendingLoad` を空にする
4. 以後、進行が変わるたびに `RunSaveManager` が上書き保存する
5. 「ホスト」（はじめから）を押したときは `PendingLoad` を空にするので、新しい部屋になる
   （セーブファイルは消さない。次のオートセーブで上書きされる）

## 置いてある物

| どこ | 何 | 置き直す |
|---|---|---|
| GameRoom | `RunSaveManager` | `Roomies/Save/Build Save Manager` |
| MainMenuSteam | `Canvas/MainMenuPanel/ContinueButton` | `Roomies/Save/Build Continue Button` |

ボタンは `HostButton` を複製して作るので見た目はそろう。位置・大きさ・文字は
シーンビューとインスペクターで自由に直してよい（作り直しても既にあれば動かさない）。

保存の間隔や、所持金を戻すかどうかは `RunSaveManager` のインスペクターで変えられる。

## テスト用

- `Roomies/Save/Delete Save File` … セーブを消す（確認ダイアログあり）
- `Roomies/Save/Open Save Folder` … 置き場所をエクスプローラーで開く

## 増やすとき

`RoomiesSaveData` に項目を足したら `RoomiesSaveData.CurrentVersion` を上げる。
版が違うセーブは読み込まずに捨てる（古いセーブで壊れた状態にならないようにするため）。
