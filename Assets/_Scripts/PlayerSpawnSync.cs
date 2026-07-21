using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnSync : NetworkBehaviour
{
    // サーバーが接続順にスポーン地点を割り当てるためのカウンター
    private static int s_spawnIndex = 0;

#if UNITY_2019_3_OR_NEWER
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetIndex() { s_spawnIndex = 0; }
#endif

    // シーン読み込み完了を待つ最大時間（秒）
    private const float SpawnPointWaitTimeout = 15f;

    // このプレイヤーに割り当てられたスポーン地点番号（サーバー側で保持）
    private int assignedIndex = -1;

    public override void OnNetworkSpawn()
    {
        // 位置の権限は Owner 側（NetworkTransform が Owner Authority）にあるため、
        // サーバーが transform.position を変えても同期されず所有者に上書きされてしまう。
        // そこでサーバーは「何番目のスポーン地点を使うか」だけを決めて所有者に通知し、
        // 実際のテレポートは所有クライアント（ホストの場合はホスト自身）が行う。
        if (IsServer)
        {
            assignedIndex = s_spawnIndex;
            s_spawnIndex++;
            ApplySpawnPointRpc(assignedIndex);
        }
    }

    /// <summary>
    /// サーバーから呼ぶ。割り当て済みのスポーン地点へプレイヤーを戻す（朝のリスポーン用）。
    /// </summary>
    public void ServerRespawn()
    {
        if (!IsServer) return;

        SmugglingPlayer smuggling = GetComponent<SmugglingPlayer>();
        if (smuggling != null && smuggling.ServerShouldStayInJail())
        {
            smuggling.ServerSendToJail();
            return;
        }

        if (assignedIndex < 0) assignedIndex = 0;
        ApplySpawnPointRpc(assignedIndex);
    }

    // 所有クライアントでのみ実行される
    [Rpc(SendTo.Owner)]
    private void ApplySpawnPointRpc(int index)
    {
        StartCoroutine(ApplySpawnPointRoutine(index));
    }

    private IEnumerator ApplySpawnPointRoutine(int index)
    {
        // シーン読み込み直後は SpawnPoint がまだ存在しないことがあるので、
        // 出現するまで待つ。
        // （ホストは StartHost 時点でメニューシーンにいるため特に重要）
        SpawnPoint[] spawnPoints = null;
        float elapsed = 0f;

        while (elapsed < SpawnPointWaitTimeout)
        {
            spawnPoints = FindObjectsByType<SpawnPoint>();

            if (spawnPoints.Length > 0)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning(
                "[PlayerSpawnSync] SpawnPointが見つかりませんでした (timeout)"
            );
            yield break;
        }

        // どのクライアントでも同じ割り当て順になるようにソートする。
        // 基本は名前順。名前が同じ場合は座標でタイブレークして順序を固定する。
        System.Array.Sort(
            spawnPoints,
            (a, b) =>
            {
                int byName = string.CompareOrdinal(a.name, b.name);
                if (byName != 0) return byName;

                Vector3 pa = a.transform.position;
                Vector3 pb = b.transform.position;

                int byX = pa.x.CompareTo(pb.x);
                if (byX != 0) return byX;

                int byZ = pa.z.CompareTo(pb.z);
                if (byZ != 0) return byZ;

                return pa.y.CompareTo(pb.y);
            }
        );

        Transform spawnPoint =
            spawnPoints[index % spawnPoints.Length].transform;

        Debug.Log(
            "[PlayerSpawnSync] index=" + index +
            " -> " + spawnPoint.name +
            " pos=" + spawnPoint.position
        );

        // CharacterController が有効なままだと transform を直接書き換えても
        // 反映されないため、一度無効化してから位置を設定する。
        CharacterController controller = GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        // SpawnPoint が傾いていてもプレイヤーが転ばないように、
        // 向き（ヨー = Y軸回転）だけを適用して body は直立させる。
        Quaternion uprightRotation =
            Quaternion.Euler(0f, spawnPoint.eulerAngles.y, 0f);

        transform.SetPositionAndRotation(
            spawnPoint.position,
            uprightRotation
        );

        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}
