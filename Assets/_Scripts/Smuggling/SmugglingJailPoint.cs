using UnityEngine;

/// <summary>逮捕されたプレイヤーの移送先。シーンに1つ配置する。</summary>
public class SmugglingJailPoint : MonoBehaviour
{
    public static SmugglingJailPoint Find()
    {
        return FindAnyObjectByType<SmugglingJailPoint>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.25f, 0.55f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward);
    }
}
