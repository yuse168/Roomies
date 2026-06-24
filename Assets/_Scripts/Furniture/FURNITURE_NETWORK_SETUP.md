# 家具マルチ同期（フェーズ3）セットアップ

家具をネットワーク同期で生成するには、**NetworkPrefab の作成と登録が必要**です
（NGOの仕様上、Prefabの内部ハッシュはエディタでしか作れないため自動化できません）。
下記3ステップを行うと、買った家具が**全プレイヤーに同期**され、効果も全員に反映されます。

> セットアップ前でも動きます：プレハブ未設定の間は**ローカル生成**にフォールバックします
>（自分だけ見える）。下記を行うと同期に切り替わります。

---

## ステップ1：NetworkFurniture プレハブを作る
1. Hierarchy で空の GameObject を作成 → 名前を `NetworkFurniture` に
2. コンポーネントを2つ追加：
   - **NetworkObject**
   - **NetworkFurniture**（このスクリプト）
3. `Assets/_Prefabs/` などにドラッグして**プレハブ化**
4. シーンに置いた元の GameObject は**削除**（プレハブだけ残す）

※ 見た目（仮ブロック）は実行時に自動生成されるので、メッシュは不要です。

---

## ステップ2：NetworkManager に登録
1. `NetworkManager`（GameRoom / または共通シーン）を選択
2. **Network Prefabs Lists** の `DefaultNetworkPrefabs` を開く
3. **＋** で行を追加し、作った `NetworkFurniture` プレハブを割り当て
4. 保存

---

## ステップ3：DayManager にプレハブを割り当て
1. GameRoom の `DayManager` を選択
2. インスペクタの **「家具のマルチ同期」→ Network Furniture Prefab** に
   `NetworkFurniture` プレハブをドロップ

これで完了。`DayManager.CanSpawnNetworkFurniture` が true になり、
購入時にサーバーが家具を Spawn → 全員に同期されます。

---

## 動作の仕組み（参考）
- 購入時：クライアント → `DayManager.BuyFurnitureServerRpc(index, 位置)` を呼ぶ
- サーバー：残高を引き、`NetworkFurniture` を Spawn、`catalogIndex` を同期
- 各クライアント：index から `FurnitureCatalog` を引いて同じ仮ブロックを生成
- 効果：`PlacedFurniture` が Room 判定、`effectActive`（NetworkVariable）で翌朝から同期有効化

## 確認方法（2クライアント）
1. ホスト＋クライアントで GameRoom に入る
2. 片方が夜に家具を購入 → **両方の画面に同じ家具が出る**
3. `N`で翌朝 → 効果が有効化（速度バフは各自の足元の家具がRoom上なら反映、収入はサーバーで加算）

---

## まだの課題（次フェーズ候補）
- [ ] 家具の**移動・撤去の同期**（今は撤去はローカルのみ）
- [ ] **カスタムモデルPrefab**の同期（今は仮ブロックのみ同期。モデルは各クライアントが同じものを持てば拡張可能）
- [ ] 退室/再参加での家具の永続化（サーバーが保持しているので基本は同期されるが要確認）
- [ ] Tabキーの競合整理（個人稼ぎリスト vs 家具効果パネル）
