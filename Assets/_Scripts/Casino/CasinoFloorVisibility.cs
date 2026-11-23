using Unity.Netcode;
using UnityEngine;

/// <summary>
/// カジノの 2 階・3 階の中身を、その階にいる人の画面でだけ出す（描く量を 3 階ぶんにしないため）。
/// 見た目だけ。ゲームの進行・通信には触らない。自分の画面の中だけで切り替える。
///
/// 規約（AGENTS.md）：置いてある物の表示を切り替えるだけ。実行時に物を作らない。
///  ・GameRoom の「Roomies_Casino/FloorVisibility」に置く（配置ツール Roomies/Casino/3 Place In Open Scene）。
///  ・階ごとの「その階にいるときだけ出す物」「その階にいないときだけ出す物（外から見た窓の明かりなど）」を
///    インスペクターで割り当てる。エディタでは全部出たままなので、Play Mode に入らずに見た目を確かめられる。
/// </summary>
public class CasinoFloorVisibility : MonoBehaviour
{
    [System.Serializable]
    public struct Floor
    {
        [Tooltip("インスペクター用の名前（2F など）")]
        public string label;
        [Tooltip("この高さ以上・未満にいるとき、この階にいると見なす（足元ではなくプレイヤーの中心の高さ）")]
        public float minY;
        public float maxY;
        [Tooltip("この階にいるときだけ出す物（中の家具・明かり）")]
        public GameObject[] onlyWhenHere;
        [Tooltip("この階にいないときだけ出す物（外から見た窓の明かりなど）")]
        public GameObject[] onlyWhenAway;
    }

    [Tooltip("建物の中かどうかを見る範囲（ワールド座標。高さは見ない）")]
    [SerializeField] private Vector2 minXZ = new Vector2(-1.2f, -19.6f);
    [SerializeField] private Vector2 maxXZ = new Vector2(16.8f, -7.8f);

    [SerializeField] private Floor[] floors = new Floor[0];

    [Tooltip("調べ直す間隔（秒）")]
    [SerializeField, Min(0.02f)] private float checkInterval = 0.1f;

    private Transform localPlayer;
    private float nextCheck;
    private int shownFloor = int.MinValue;   // いま出している階（floors の番号。-1 はどの階でもない）

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        if (localPlayer == null)
        {
            NetworkManager nm = NetworkManager.Singleton;
            NetworkObject player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (player == null) return;        // まだ自分がいない間は、置いてあるまま
            localPlayer = player.transform;
        }

        int here = FloorAt(localPlayer.position);
        if (here == shownFloor) return;
        shownFloor = here;

        for (int i = 0; i < floors.Length; i++)
        {
            SetAll(floors[i].onlyWhenHere, i == here);
            SetAll(floors[i].onlyWhenAway, i != here);
        }
    }

    /// <summary>その位置がどの階か（floors の番号）。建物の外・どの階でもないときは -1。</summary>
    public int FloorAt(Vector3 position)
    {
        if (position.x < minXZ.x || position.x > maxXZ.x || position.z < minXZ.y || position.z > maxXZ.y) return -1;
        for (int i = 0; i < floors.Length; i++)
            if (position.y >= floors[i].minY && position.y < floors[i].maxY) return i;
        return -1;
    }

    private static void SetAll(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (GameObject go in objects)
            if (go != null && go.activeSelf != active) go.SetActive(active);
    }
}
