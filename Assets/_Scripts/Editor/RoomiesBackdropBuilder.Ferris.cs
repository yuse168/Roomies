#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 遠景の観覧車（Build Distant Backdrop → BuildLandmarks から呼ぶ。2026/11/14 作り直し）。
/// ・輪は YZ 面（軸は X）。外の輪2本（x ±RimX）と内の輪2本、そのあいだのはしごの桟・ジグザグの補強、軸から内の輪への腕
/// ・軸のまわりのハブ、両側の A 形の脚と横つなぎ、乗り場と切符売り場
/// ・ゴンドラは丸い形（回転体）に屋根と窓の帯。外の輪に電球を並べる
/// ・乱数はゴンドラの色を決める 16 回だけ（前と同じ順。ほかの遠景の配置が変わらないように）
/// ・回すスクリプトは付けない（止まった観覧車。見た目だけ。遠景のまとめ（Combine）でまとまる）
/// </summary>
internal static partial class RoomiesBackdropBuilder
{
    const float FerrisRadius = 26f, FerrisInner = 24.2f, FerrisHubY = FerrisRadius + 5.5f;
    const float RimX = 1.6f, LegX = 3.2f, LegFootZ = 11f, FerrisDeckY = 1.2f;
    const int FerrisCabins = 16, RimSteps = 32, RimPoints = 48, BulbsPerRim = 64;
    const string BulbMaterial = "Assets/_Materials/Templates/Variants/Emissive/glow_bulb.mat";

    private static void FerrisWheel(Transform parent)
    {
        RoomiesRestyleKit.Begin();
        Transform wheel = Group(parent, "Ferris wheel");
        wheel.position = new Vector3(188f, 0f, 188f);
        wheel.localRotation = Quaternion.Euler(0f, -28f, 0f);
        Vector3 hub = new Vector3(0f, FerrisHubY, 0f);

        // 乗り場と切符売り場
        Box(wheel, "Platform", new Vector3(0f, FerrisDeckY / 2f, 0f), new Vector3(14f, FerrisDeckY, 26f), "cream");
        Box(wheel, "Platform edge", new Vector3(0f, FerrisDeckY + .1f, 0f), new Vector3(14.4f, .2f, 26.4f), "silver");
        Box(wheel, "Ticket booth", new Vector3(5.2f, FerrisDeckY + 1.5f, 9f), new Vector3(3f, 3f, 3f), "coral");
        Box(wheel, "Booth roof", new Vector3(5.2f, FerrisDeckY + 3.15f, 9f), new Vector3(3.8f, .3f, 3.8f), "ink");
        Box(wheel, "Boarding stair", new Vector3(0f, FerrisDeckY + .4f, 0f), new Vector3(4f, .8f, 3f), "cream");

        // A 形の脚（両側）と横つなぎ
        float legDy = FerrisHubY - FerrisDeckY;
        foreach (float x in new[] { -LegX, LegX })
        {
            foreach (float z in new[] { -LegFootZ, LegFootZ })
                Strut(wheel, "Steel leg", new Vector3(x, FerrisDeckY, z), new Vector3(x, FerrisHubY, 0f), .9f, "silver");
            foreach (float y in new[] { 12f, 22f })
            {
                float half = LegFootZ * (1f - (y - FerrisDeckY) / legDy);
                Box(wheel, "Steel tie", new Vector3(x, y, 0f), new Vector3(.5f, .5f, half * 2f), "silver");
            }
            Box(wheel, "Leg footing", new Vector3(x, FerrisDeckY + .3f, -LegFootZ), new Vector3(1.8f, .6f, 1.8f), "ink");
            Box(wheel, "Leg footing", new Vector3(x, FerrisDeckY + .3f, LegFootZ), new Vector3(1.8f, .6f, 1.8f), "ink");
        }
        {
            float half = LegFootZ * (1f - (12f - FerrisDeckY) / legDy);
            foreach (float z in new[] { -half, half })
                Box(wheel, "Steel tie", new Vector3(0f, 12f, z), new Vector3(LegX * 2f, .4f, .4f), "silver");
        }

        // 軸とハブ
        CylX(wheel, "Steel axle", hub, 1.0f, LegX * 2f + 1f, "silver");
        CylX(wheel, "Hub drum", hub, 3.4f, RimX * 2f + .4f, "silver");
        CylX(wheel, "Hub cap", hub, 2.2f, RimX * 2f + 1.0f, "coral");

        // 輪：外と内の輪（両側）、はしごの桟、内と外をつなぐ補強、軸から内の輪への腕
        Material bulb = AssetDatabase.LoadAssetAtPath<Material>(BulbMaterial);
        foreach (float x in new[] { -RimX, RimX })
        {
            Ring(wheel, "Steel rim", "ferris_rim_outer", new Vector3(x, FerrisHubY, 0f), FerrisRadius, .3f, "silver");
            Ring(wheel, "Steel rim", "ferris_rim_inner", new Vector3(x, FerrisHubY, 0f), FerrisInner, .22f, "silver");
            for (int i = 0; i < RimSteps; i++)
            {
                float a0 = 360f / RimSteps * i, a1 = a0 + 180f / RimSteps;
                Strut(wheel, "Steel truss", OnWheel(x, FerrisInner, a0), OnWheel(x, FerrisRadius, a0), .2f, "silver");
                Strut(wheel, "Steel truss", OnWheel(x, FerrisInner, a0), OnWheel(x, FerrisRadius, a1), .16f, "silver");
            }
            for (int i = 0; i < FerrisCabins; i++)
            {
                float a = 360f / FerrisCabins * i;
                Strut(wheel, "Steel spoke", OnWheel(x * .9f, 1.7f, a), OnWheel(x, FerrisInner, a), .28f, "silver");
            }
            // 電球（外の輪の外側に並べる）
            for (int i = 0; i < BulbsPerRim; i++)
            {
                float a = 360f / BulbsPerRim * i;
                Vector3 p = OnWheel(x, FerrisRadius + .38f, a);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Bulb";
                go.transform.SetParent(wheel, false);
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
                go.transform.localScale = new Vector3(.32f, .32f, .32f);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                Paint(go.GetComponent<MeshRenderer>(), bulb);
            }
        }
        for (int i = 0; i < RimSteps; i++)
        {
            Vector3 p = OnWheel(0f, FerrisRadius, 360f / RimSteps * i);
            Box(wheel, "Steel rung", p, new Vector3(RimX * 2f, .25f, .25f), "silver");
        }

        // ゴンドラ（外の輪の桟から吊る。丸い胴・窓の帯・屋根・吊り棒）
        Vector2[] bodyProfile =
        {
            new Vector2(0f, -1.15f), new Vector2(.65f, -1.1f), new Vector2(1.05f, -.8f), new Vector2(1.2f, -.25f),
            new Vector2(1.2f, .45f), new Vector2(1.05f, .85f), new Vector2(.6f, 1.0f), new Vector2(0f, 1.02f),
        };
        Vector2[] bandProfile = { new Vector2(1.225f, -.12f), new Vector2(1.225f, .55f) };
        Vector2[] roofProfile = { new Vector2(0f, .95f), new Vector2(1.0f, .95f), new Vector2(.95f, 1.1f), new Vector2(.2f, 1.5f), new Vector2(0f, 1.52f) };
        for (int i = 0; i < FerrisCabins; i++)
        {
            float a = 360f / FerrisCabins * i;
            Vector3 attach = OnWheel(0f, FerrisRadius, a);
            Vector3 center = attach + new Vector3(0f, -2.1f, 0f);
            string paint = Pick("coral", "mustard", "teal", "pink");
            Transform cabin = Group(wheel, "Cabin");
            cabin.localPosition = center;
            Lathe(cabin, "Cabin body", "ferris_cabin_body", bodyProfile, paint);
            Lathe(cabin, "Cabin window", "ferris_cabin_band", bandProfile, "blue");
            Lathe(cabin, "Cabin cap", "ferris_cabin_roof", roofProfile, paint);
            Box(cabin, "Cabin hanger", new Vector3(0f, 1.8f, 0f), new Vector3(.14f, .6f, .14f), "silver");
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>輪の面（YZ）の上の点。角度 0 が真上、軸のまわりに回る。</summary>
    static Vector3 OnWheel(float x, float r, float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return new Vector3(x, FerrisHubY + Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    /// <summary>2点を結ぶ角材（YZ 面の中で傾ける。x は2点の平均）。</summary>
    static void Strut(Transform parent, string name, Vector3 a, Vector3 b, float thick, string material)
    {
        Vector3 d = b - a;
        float len = new Vector2(d.y, d.z).magnitude;
        float theta = Mathf.Atan2(d.z, d.y) * Mathf.Rad2Deg;
        BoxRot(parent, name, (a + b) / 2f, new Vector3(thick, len, thick), new Vector3(theta, 0f, 0f), material);
    }

    /// <summary>軸が X の円柱。</summary>
    static void CylX(Transform parent, string name, Vector3 pos, float diameter, float length, string material)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        c.transform.SetParent(parent, false);
        c.transform.localPosition = pos;
        c.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        c.transform.localScale = new Vector3(diameter, length / 2f, diameter);
        Object.DestroyImmediate(c.GetComponent<Collider>());
        Paint(c.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    /// <summary>YZ 面の輪（管）。</summary>
    static void Ring(Transform parent, string name, string key, Vector3 center, float r, float tube, string material)
    {
        var path = new Vector3[RimPoints];
        for (int i = 0; i < RimPoints; i++)
        {
            float a = Mathf.PI * 2f * i / RimPoints;
            path[i] = new Vector3(0f, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
        var go = RoomiesRestyleKit.Tube(parent, name, key, path, tube, LoadMaterial(material), true, center);
        Paint(go.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    /// <summary>回転体（RoomiesRestyleKit のメッシュ。軸は Y）。</summary>
    static void Lathe(Transform parent, string name, string key, Vector2[] profile, string material)
    {
        var go = RoomiesRestyleKit.Lathe(parent, name, key, profile, Vector3.zero, LoadMaterial(material), default, 12);
        Paint(go.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }
}
#endif
