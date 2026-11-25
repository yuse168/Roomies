using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 家具のカタログ（全員で同じ並び）を持つ係。GameRoom に 1 つ置く。
///
/// 2026/11/25：家具の注文は部屋の PC の「家具ショップ」アプリ（<see cref="RoomComputerUI"/>）にまとめた。
/// 前の P キーの家具ショップ（実行時に作る帯の UI）と、O キーの「家具の効果」の一覧（実行時に作る板）は消した。
/// 家具の効果の一覧は、PC の「家具の効果」アプリにある。
/// 注文の仕組み（代金・配達・夜だけ）は DayManager.BuyFurnitureServerRpc のまま。届いた家具は F で持つ・R で回す・F で置く。
/// </summary>
public class FurnitureEditController : MonoBehaviour
{
    [Header("共有家具カタログ（全クライアントで同じ並びにする）")]
    [Tooltip("Prefab・価格・効果をInspectorから変更できます。indexが同期キーなので並び順と内容は全員で共通にしてください。")]
    [SerializeField] private List<FurnitureItem> catalog = new List<FurnitureItem>();

    /// <summary>Inspectorでカタログを編集したいとき用：デフォルト12種を流し込む。</summary>
    [ContextMenu("デフォルトカタログを生成")]
    private void EditorFillDefaultCatalog()
    {
        BuildDefaultCatalog();
    }

    private void Awake()
    {
        if (catalog == null || catalog.Count == 0)
            BuildDefaultCatalog();

        FurnitureCatalog.Configure(catalog);
    }

    private void BuildDefaultCatalog()
    {
        // マルチ同期と整合させるため、共有カタログ（FurnitureCatalog）をそのまま使う。
        // ※ index がそのまま同期キーになるので、並び順は FurnitureCatalog 側で管理する。
        catalog = new List<FurnitureItem>(FurnitureCatalog.Items);
    }
}
