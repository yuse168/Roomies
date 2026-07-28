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
- 購入時：Client → `DayManager.BuyFurnitureServerRpc(index, 位置)` を呼ぶ
- サーバー：残高を引き、`NetworkFurniture` を Spawn、`catalogIndex` を同期
- 各Client：indexから`FurnitureCatalog`を引いて同じ見た目を生成
- 効果：`PlacedFurniture` が Room 判定、`effectActive`（NetworkVariable）で翌朝から同期有効化
- 移動：持ち主と距離をServer側で検証し、床へスナップして設置

## 確認方法（2クライアント）
1. ホスト＋クライアントで GameRoom に入る
2. 片方が夜に家具を購入 → **両方の画面に同じ家具が出る**
3. `N`で翌朝 → 効果が有効化（速度バフは各自の足元の家具がRoom上なら反映、収入はサーバーで加算）

---

## まだの課題

- [ ] カスタムモデルPrefabの本番素材登録
- [ ] 退室/再参加での家具の永続化（サーバーが保持しているので基本は同期されるが要確認）
- [ ] 家具の売却・廃棄
