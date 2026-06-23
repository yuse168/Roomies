using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawnSync : NetworkBehaviour
{
    // シーン読み込み完了を待つ最大時間（秒）
    private const float SpawnPointWaitTimeout = 15f;
    private const string GameSceneName = "GameRoom";
    private const string SpawnPointTag = "SpawnPoint";

    public override void OnNetworkSpawn()
    {
        // 位置の権限は Owner 側（NetworkTransform が Owner Authority）にあるため、
        // サーバーが transform.position を変えても同期されず所有者に上書きされてしまう。
        // そこでサーバーは「何番目のスポーン地点を使うか」だけを決めて所有者に通知し、
        // 実際のテレポートは所有クライアント（ホストの場合はホスト自身）が行う。
        if (IsServer)
        {
            ApplySpawnPointRpc(OwnerClientId);
        }
    }

    // 所有クライアントでのみ実行される
    [Rpc(SendTo.Owner)]
    private void ApplySpawnPointRpc(ulong ownerClientId)
    {
        StartCoroutine(ApplySpawnPointRoutine(ownerClientId));
    }

    private IEnumerator ApplySpawnPointRoutine(ulong ownerClientId)
    {
        // シーン読み込み直後は SpawnPoint がまだ存在しないことがあるので、
        // 出現するまで待つ。
        // （ホストは StartHost 時点でメニューシーンにいるため特に重要）
        Transform[] spawnPoints = null;
        float elapsed = 0f;

        while (elapsed < SpawnPointWaitTimeout)
        {
            if (SceneManager.GetActiveScene().name == GameSceneName)
            {
                spawnPoints = GetSortedSpawnPoints();

                if (spawnPoints.Length > 0)
                {
                    break;
                }
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
        // PlayerSpawn1, PlayerSpawn2... の名前順で並べ、OwnerClientId 0 -> 0番、
        // OwnerClientId 1 -> 1番に割り当てる。
        int spawnIndex = (int)(ownerClientId % (ulong)spawnPoints.Length);
        Transform spawnPoint = spawnPoints[spawnIndex];

        Debug.Log(
            "[PlayerSpawnSync] ownerClientId=" + ownerClientId +
            " spawnIndex=" + spawnIndex +
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

    private static Transform[] GetSortedSpawnPoints()
    {
        List<Transform> points = new List<Transform>();

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(SpawnPointTag);
        foreach (GameObject obj in taggedObjects)
        {
            if (obj.GetComponent<SpawnPoint>() != null)
            {
                points.Add(obj.transform);
            }
        }

        if (points.Count == 0)
        {
            foreach (SpawnPoint point in FindObjectsByType<SpawnPoint>())
            {
                if (point.name.StartsWith("PlayerSpawn"))
                {
                    points.Add(point.transform);
                }
            }
        }

        points.Sort(CompareSpawnPoints);
        WarnIfOverlapping(points);
        return points.ToArray();
    }

    private static int CompareSpawnPoints(Transform a, Transform b)
    {
        int byName = string.CompareOrdinal(a.name, b.name);
        if (byName != 0) return byName;

        Vector3 pa = a.position;
        Vector3 pb = b.position;

        int byX = pa.x.CompareTo(pb.x);
        if (byX != 0) return byX;

        int byZ = pa.z.CompareTo(pb.z);
        if (byZ != 0) return byZ;

        return pa.y.CompareTo(pb.y);
    }

    private static void WarnIfOverlapping(List<Transform> points)
    {
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float distance = Vector3.Distance(
                    points[i].position,
                    points[j].position);
                if (distance < 0.1f)
                {
                    Debug.LogWarning(
                        "[PlayerSpawnSync] SpawnPointが重なっています: " +
                        points[i].name + " / " + points[j].name +
                        " pos=" + points[i].position);
                }
            }
        }
    }
}
