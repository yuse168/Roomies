# PROGRESS.md

最終更新: 2026-07-11
現在フェーズ: Steamマルチプレイ不具合調査

## 現在の状態

- 完了: `me_from_soso` の内容を `main` に反映、シーン遷移・スポーン構成のコード調査
- 作業中: なし
- 未確認: Unityエディタおよび2つのSteamアカウントでの実行ログ確認
- 問題: `GameRoom` に遷移元とは別の `NetworkManager` が存在し、スポーン地点に `NetworkObject` / `PlayerSpawnSync` が付く一方で `Player.prefab` に `PlayerSpawnSync` がない
- TODO: `GameRoom` の重複NetworkManagerを除去し、`PlayerSpawnSync`をPlayer prefabへ移して2クライアント検証
- 次にやること: ホスト側ログで `StartHost: True` とGameRoom Load完了を確認後、シーン構成を修正して再検証

## 変更履歴

| ID | 内容 | 結果 | 変更ファイル | 次 |
|---|---|---|---|---|
| 2026-07-11-01 | `me_from_soso` を基準に `main` を整理 | 実装済み・未確認 | `PROGRESS.md` | Unityで動作確認 |
| 2026-07-11-02 | Steamマルチプレイのシーン遷移・スポーン構成を調査 | 調査完了 | `Assets/_Scenes/MainMenuSteam.unity`, `Assets/_Scenes/GameRoom.unity`, `Assets/_Prefabs/Player.prefab`, `Assets/_Scripts/Network/SteamLobby.cs` | Unityログで停止地点を確認し構成修正 |
