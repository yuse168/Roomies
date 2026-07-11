# Roomies Lobby UI セットアップガイド
## iiwakeking方式のLobby待機画面 移植手順

---

## 1. MainMenuSteam シーンのUI構成

以下の GameObject 階層を `MainMenuSteam.unity` に作成すること。

```
[Canvas]
├── MainMenuPanel             ← 既存のメインメニュー
│   ├── HostButton
│   ├── JoinButton
│   ├── QuitButton
│   └── StatusText (TMP_Text, 初期はSetActive=false)
│
├── JoinPanel                 ← 新規: コード入力パネル (初期はSetActive=false)
│   ├── CodeInputField (TMP_InputField)
│   ├── ConfirmJoinButton
│   ├── CancelButton
│   └── ErrorText (TMP_Text, 初期はSetActive=false)
│
└── LobbyPanel                ← 新規: ロビー待機画面 (初期はSetActive=false)
    ├── PartyCodeText (TMP_Text) ← "ABCDE" 表示
    ├── CopyButton
    ├── CopyFeedbackText (TMP_Text, 初期はSetActive=false) ← "コピーしました!"
    ├── PlayerListParent (空のTransform/VerticalLayoutGroup)
    ├── StartButton             ← ホストのみ表示
    ├── WaitingText (TMP_Text)  ← "Waiting for host..." クライアントのみ
    ├── InviteButton            ← ホストのみ表示 (任意)
    └── LeaveButton
```

---

## 2. PlayerRowPrefab の作成

1. `Assets/_Prefabs/` に `PlayerRow.prefab` を作成
2. 中身: `GameObject` + `TMP_Text` (1つだけ)
3. VerticalLayoutGroup の子として使われるため、高さを固定 (例: LayoutElement.preferredHeight = 40)

---

## 3. MainMenuSteam シーンの GameObject 構成

### SceneRootに追加するコンポーネント

**GameObject "LobbyController"** を作成し、以下を付ける:
- `MainMenuManager` コンポーネント
- `LobbyUIManager` コンポーネント

### MainMenuManager の Inspector 設定

| フィールド | 割り当て |
|---|---|
| Main Menu Panel | `MainMenuPanel` GameObject |
| Join Panel | `JoinPanel` GameObject |
| Join Code Input | `JoinPanel/CodeInputField` |
| Confirm Join Button | `JoinPanel/ConfirmJoinButton` |
| Cancel Join Button | `JoinPanel/CancelButton` |
| Join Status Text | `MainMenuPanel/StatusText` |
| Host Button | `MainMenuPanel/HostButton` |
| Join Button | `MainMenuPanel/JoinButton` |

### LobbyUIManager の Inspector 設定

| フィールド | 割り当て |
|---|---|
| Lobby Panel | `LobbyPanel` GameObject |
| Main Menu Panel | `MainMenuPanel` GameObject |
| Party Code Text | `LobbyPanel/PartyCodeText` |
| Copy Code Button | `LobbyPanel/CopyButton` |
| Copy Feedback Text | `LobbyPanel/CopyFeedbackText` |
| Player List Parent | `LobbyPanel/PlayerListParent` |
| Player Row Prefab | `Assets/_Prefabs/PlayerRow.prefab` |
| Start Button | `LobbyPanel/StartButton` |
| Waiting Text | `LobbyPanel/WaitingText` |
| Leave Button | `LobbyPanel/LeaveButton` |
| Invite Button | `LobbyPanel/InviteButton` |
| Join Error Text | `JoinPanel/ErrorText` (または `MainMenuPanel/StatusText`) |
| Min Players To Start | 2 |

---

## 4. DontDestroyOnLoad オブジェクト確認

以下のGameObjectが `DontDestroyOnLoad` で永続化されているか確認:

| GameObject | コンポーネント | 備考 |
|---|---|---|
| `SteamManager` | SteamManager | Steam初期化 |
| `SteamLobby` | SteamLobby | ロビー管理 |
| `NetworkManager` | NetworkManager + SteamNetworkingSocketsTransport | NGO管理 |

- SteamLobby は `DontDestroyOnLoad` を自身のAwakeで設定済み
- SteamManager は既存のまま (DontDestroyOnLoad済み)
- NetworkManager は既存設定を確認すること

---

## 5. NetworkManager の設定確認

1. `NetworkManager` GameObject を選択
2. Inspector で以下を確認:
   - **Network Transport**: `SteamNetworkingSocketsTransport` が設定されているか
   - **Player Prefab**: `Player.prefab` が設定されているか (PlayerSpawnSyncが付いていること)
   - **Network Prefabs**: CarryableObject, SharedMoneyManager等のNetworkBehaviourを持つPrefabが全て登録されているか
   - **Scene Management**: Enable Scene Management = ON
   - **Connection Approval**: チェックしなくてよい (SteamLobby.StartGame()で動的に設定される)

---

## 6. 動作フロー（確認用）

### Host フロー
```
[MainMenu] HostButton押す
    ↓ SteamLobby.CreateLobby()
    ↓ Steam Lobbyが作成される (OnLobbyReady発火)
    ↓ LobbyUIManager.ShowLobbyUI(isHost=true)
    ↓ MainMenuPanel非表示 / LobbyPanel表示
    ↓ パーティーコード表示、メンバー一覧更新ループ開始
[LobbyUI] プレイヤーが集まったらStartButtonを押す
    ↓ SteamLobby.StartGame()
    ↓ Steam lobby data "GameStarted"="1" を設定 (クライアントへ通知)
    ↓ SteamNetworkingSocketsTransport経由でNGO StartHost()
    ↓ NetworkManager.SceneManager.LoadScene("GameRoom")
[GameRoom] 全プレイヤーがGameRoomに入る
    ↓ PlayerSpawnSyncがSpawnPointを割り当て
```

### Join フロー
```
[MainMenu] JoinButton押す
    ↓ JoinPanel表示
    ↓ コードを入力してConfirmJoin押す
    ↓ SteamLobby.JoinLobbyWithCode(code)
    ↓ Steam Lobbyを検索して参加 (OnLobbyJoined発火)
    ↓ LobbyUIManager.ShowLobbyUI(isHost=false)
[LobbyUI] "Waiting for host..." 表示でホスト待機
    ↓ ホストがStartGameを押す
    ↓ LobbyDataUpdate_t コールバック受信 (GameStarted=1)
    ↓ SteamLobby.StartClientRoutine() → NGO StartClient()
    ↓ NGO がHostのSceneManager.LoadSceneに追従
[GameRoom] 全プレイヤーがGameRoomに入る
```

---

## 7. 2アカウントテスト方法

1. Unity Editor で Play → HostでLobbyを作成
2. ビルドを別途作成しておき、別のSteamアカウントでログインして起動
3. または `Multiplayer Play Mode` パッケージを使いEditor内で複数クライアントを起動

**Steam AppID (steam_appid.txt)**:
現在 `4953360` (正式なAppID・2026-07-09設定)。SteamManager.cs の `k_SteamAppId` と `steam_appid.txt` の両方に設定してある。変更する場合は両方をそろえること。

---

## 8. 変更ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `Assets/_Scripts/Network/SteamLobby.cs` | **全面改修**: Lobby UI対応。CreateLobbyがOnLobbyReadyイベント発火のみに変更。StartGame()メソッド追加。LobbyDataUpdate/LobbyChatUpdateコールバック追加。LeaveLobby()追加。GetLobbyMembers()追加。 |
| `Assets/_Scripts/MainMenuManager.cs` | **全面改修**: Host→CreateLobby(LobbyUI任せ)。Join→コード入力パネル表示。SteamLobbyイベント購読。 |
| `Assets/_Scripts/LobbyUIManager.cs` | **新規作成**: ロビー待機画面UI管理。パーティーコード表示・コピー・プレイヤー一覧・Start/Leaveボタン。 |
| `Assets/_Scripts/Network/LobbyIdDisplay.cs` | 軽微改修: OnLobbyReady/OnLobbyJoinedイベント購読に変更。 |
| `Assets/_Scripts/PlayerSpawnSync.cs` | 軽微修正: FindObjectsByType引数追加。staticインデックスのリセット追加。 |

---

## 9. 未確認事項・手動確認が必要なこと

1. **Unity シーン設定**: 上記UIの配置はエディタで手動作業が必要
2. **NetworkManager の Player Prefab**: Player.prefab に PlayerSpawnSync が付いているか確認
3. **Network Prefabs リスト**: GameRoom で使う NetworkBehaviour 持ちのPrefabが全登録されているか
4. **MainMenuSteam シーンのビルド設定**: File > Build Settings の Scene リストに含まれているか
5. **SteamNetworkingSocketsTransport**: NetworkManager に正しく設定されているか
6. **LobbyDataUpdate_t の遅延**: クライアントがGameStarted=1を受け取る前にタイムアウトしないか (30秒タイムアウトで対処済み)
7. **ホストが先にGameRoomに入る**: クライアントの接続承認前にシーンが遷移するため、ApprovalCheckのタイミングを注意
8. **旧 MainMenu.unity**: 旧シーン(SteamなしのMainMenu)は使わない場合、Build Settingsから除外すること
