# 家具システム

`NetworkFurniture.prefab`の生成、NetworkPrefab登録、GameRoomのDayManager参照は
設定済み。追加のInspector作業は不要。

設定が外れた場合はUnityメニューの
`Tools/Roomies/家具ネットワーク設定を修復`から再設定できる。

## 操作

- 夜に`P`：家具ショップを開く
- 数字キー／マウスホイール：商品を選択
- 左クリック：家具を購入
- 家具へ照準を合わせて`F`：家具を持つ
- 家具を持って`R`：45度回転
- 家具を持って`F`：床へ設置
- `O`：家具効果の一覧を表示

家具の移動と設置位置はServer側で検証し、`NetworkTransform`で全Clientへ同期する。
移動中は家具のColliderと効果を一時停止し、設置後に再開する。

---

## 動作の仕組み（参考）
- 購入時：Client → `DayManager.BuyFurnitureServerRpc(index)` を呼ぶ
- Server：夜の自由行動中か、注文者・商品・Prefab登録・残高を検証する
- Server：`FurnitureDeliveryPoint` と全注文共通の配置順から配達先を決定する（Clientは座標や金額を送らない）
- Server：`NetworkFurniture` を生成し、`catalogIndex` を設定できた注文だけ決済する。生成失敗では残高を変えない
- 各Client：indexから`FurnitureCatalog`を引いて同じ見た目を生成
- 効果：`PlacedFurniture` が Room 判定、`effectActive`（NetworkVariable）で翌朝から同期有効化
- 移動：持ち主と距離をServer側で検証し、床へスナップして設置

## 確認方法（2クライアント）
1. ホスト＋クライアントで GameRoom に入る
2. 片方が夜に家具を購入 → **両方の画面に同じ家具が出る**
3. 翌朝まで進める → 効果が有効化（速度バフは各自の足元の家具がRoom上なら反映、収入はサーバーで加算）。Nキー送りは開発用設定を有効にしたHostのみ

2026-09-05追加確認：同時購入、残高不足、配達地点／Prefab未設定、朝・夜終了演出中・Game Over中の購入拒否。

---

## まだの課題

- [ ] カスタムモデルPrefabの本番素材登録
- [ ] 退室/再参加での家具の永続化（サーバーが保持しているので基本は同期されるが要確認）
- [ ] 家具の売却・廃棄
