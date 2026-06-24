using UnityEngine;

/// <summary>
/// 家具の配達先（スポーン位置）。
/// 空のGameObjectに付けて、部屋の床の上に置いて位置を調整する。
/// シーンに無い場合はカメラ前方に仮配達される。
/// </summary>
public class FurnitureDeliveryPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.62f, 0.12f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.2f);
    }
}
