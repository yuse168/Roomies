#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// メインの家（ROOMIES。プレイヤーが朝を迎える家）の外まわり。作り直し3（2026/11/13 ゆせさんの依頼 20:34）で0から設計し直した。何度実行しても同じ結果。
/// ・中のゲームの仕組みはそのまま：2階の部屋（ベッド・冷蔵庫・クローゼット・ブレーカー・スポーン地点・家具・配達地点）、2階の壁と柱
///   （部屋の内壁も兼ねる。当たり判定もそのまま）、床（Demo/pCube11）、屋根の当たり判定、2階の出入口（x 19.4〜21.6）。
/// ・作るもの（「Town main house」）：
///   1階：パステルのしっくいの外壁（当たり判定つき）、よろい戸と花の箱の付いた窓、踊り場の下の屋根のかかった玄関。
///   2階：縦の板張り（板と押し縁）、窓枠・窓台・しま模様の日よけ。
///   屋根：寄棟（4方向に下る屋根）、隅棟の笠木・軒の鼻隠し・雨どい・煙突。
///   外階段：正面の壁に沿って西から東へ上がる石の階段（木の踏み板・白い手すり）と、2階の出入口の前の木の踊り場（柱で支える）。
///   古い外階段（Entry stairs の段の当たり判定、Demo/pCube12 の坂の当たり判定）は外す。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string MainHouseGroup = "Town main house";
    // 1階の箱（Demo/pCube11）と2階の壁の面
    const float HX0 = 12.9f, HX1 = 22.9f, HZ0 = 13.7f, HZ1 = 23.7f, HFloor2 = 3.3f;
    const float WinY0 = 4.255f, WinY1 = 5.455f, UpperTop = 5.86f, RoofBase = 6.12f;
    const float HT = .25f;                                   // 1階の外壁の厚さ
    const int StairSteps = 13; const float StairRun = .32f;   // 段の数と踏み面
    const float StairZ0 = 12.15f, StairZ1 = 13.45f, LandX1 = 23.0f, LandZ0 = 12.0f;
    static float StairX0 => LandX0 - StairSteps * StairRun;
    const float LandX0 = 18.8f;

    [MenuItem("Roomies/Art/Rebuild/2 Main House")]
    internal static void BuildMainHouse()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var house = root.transform.Find(House);
            var stairs = root.transform.Find("Entry stairs");
            StripOldHouse(house, stairs);
            var g = Fresh(root.transform, MainHouseGroup);
            HouseGround(g);
            HouseUpper(g);
            HouseHipRoof(g);
            HouseStair(g);
            HousePorch(g);
            RoomiesBevelBuilder.ApplyTownUnder(g);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        DisableOldStairRamp();
        AssetDatabase.SaveAssets();
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();
        CombineForSpeed();
        Debug.Log("[Rebuild] main house built");
    }

    static void StripOldHouse(Transform house, Transform stairs)
    {
        if (house != null)
        {
            foreach (var n in new[] { "Entry trim", "Roof cap", "Home sign", "Address plaque" })
                foreach (Transform c in house.Cast<Transform>().Where(c => c.name == n).ToArray()) Object.DestroyImmediate(c.gameObject);
            var roof = house.Find("House roof");
            if (roof != null) { Object.DestroyImmediate(roof.GetComponent<MeshRenderer>()); Object.DestroyImmediate(roof.GetComponent<MeshFilter>()); }
            // 看板の文字は2階の正面の板張りの前へ
            foreach (var (n, p) in new[] { ("Sign - ROOMIES", new Vector3(16.2f, 5.05f, 13.5f)), ("Sign - 05", new Vector3(18.95f, 4.6f, 13.5f)) })
            {
                var t = house.Find(n); if (t == null) continue;
                var q = t.position; q.x = p.x; q.y = p.y; q.z = p.z; t.position = q;
            }
            // エアコンは東の外壁の外、ごみ箱と袋は階段の下から東の角へ
            foreach (Transform c in house)
            {
                if (c.name.StartsWith("air_conditioner")) { var p = c.position; p.x = HX1 + HT + .2f; c.position = p; }
                if (c.name == "street_bin") c.position = new Vector3(23.75f, c.position.y, 12.9f);
                if (c.name == "trash_bag") c.position = new Vector3(23.7f, c.position.y, 12.2f);
            }
        }
        // 古い外階段の段（当たり判定だけ残っていた）は外す
        if (stairs != null) foreach (Transform c in stairs.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
    }

    /// <summary>シーンの Demo/pCube12（古い階段の坂）の見た目と当たり判定を切る（新しい階段に置き換えたため）。</summary>
    static void DisableOldStairRamp()
    {
        var scene = SceneManager.GetActiveScene();
        var demo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Demo");
        var t = demo != null ? demo.transform.Find("pCube12") : null;
        if (t == null) return;
        bool changed = false;
        var r = t.GetComponent<MeshRenderer>(); if (r != null && r.enabled) { r.enabled = false; EditorUtility.SetDirty(r); changed = true; }
        foreach (var c in t.GetComponents<Collider>()) if (c.enabled) { c.enabled = false; EditorUtility.SetDirty(c); changed = true; }
        if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
    }

    /// <summary>外の面（法線 n、面の位置 plane）に沿った箱。a0〜a1 は面に沿った位置、d0〜d1 は面から外への厚さ。</summary>
    static void FaceBox(Transform g, string name, Vector3 n, float plane, float a0, float a1, float y0, float y1, float d0, float d1, Material m)
    {
        if (Mathf.Abs(n.z) > .5f)
        {
            float z0 = plane + n.z * d0, z1 = plane + n.z * d1;
            Box(g, name, a0, a1, y0, y1, Mathf.Min(z0, z1), Mathf.Max(z0, z1), m);
        }
        else
        {
            float x0 = plane + n.x * d0, x1 = plane + n.x * d1;
            Box(g, name, Mathf.Min(x0, x1), Mathf.Max(x0, x1), y0, y1, a0, a1, m);
        }
    }

}
#endif
