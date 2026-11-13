#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 自然物 3：草の地面と草の束。何度実行しても同じ結果。
/// ・町の地面（GameRoom の Demo/pPlane1）：歩ける面と当たり判定はそのまま（平ら）。見た目だけ、草・土・小石を混ぜた大きなテクスチャ
///   （町全体に1枚、ノーマルマップ付き）＋近くで見たときの草の細かい模様（ディテール）にする。
/// ・境界の外の地面（遠景の「Outer terrain」）：ゆるい起伏のある地面。道・家・木などの足元は平らに残す（埋もれない・浮かない）。
///   遠景の配置ツール（Build Distant Backdrop）が作る。当たり判定は付けない（境界の外で歩けない）。
/// ・草の束（GameRoom の「Roomies_Grass」）：芝生の上だけに散らす。10m の区画ごとにまとめ、LODGroup で遠い区画は消える。
///   影は落とさない。当たり判定は付けない。歩道・道・建物・花壇・配達場所の上には置かない。
/// </summary>
internal static partial class RoomiesNature
{
    const string GrassRoot = "Roomies_Grass";
    const string TerrainGroup = "Outer terrain";
    const string GroundMeshPath = ArtDir + "Ground.asset";
    // 町の地面（pPlane1）の範囲
    const float TownX = 40f, TownZ = 25f;

    [MenuItem("Roomies/Art/Nature/3 Ground And Grass")]
    internal static void BuildGroundAndGrass()
    {
        var scene = SceneManager.GetActiveScene();
        var plane = scene.GetRootGameObjects().Select(g => g.transform.Find("pPlane1")).FirstOrDefault(t => t != null);
        if (plane == null) { Debug.LogWarning("[Nature] pPlane1 not found (open GameRoom)"); return; }
        var mat = TownGroundMaterial(plane.GetComponent<MeshFilter>().sharedMesh);
        var r = plane.GetComponent<MeshRenderer>();
        if (r.sharedMaterial != mat) { r.sharedMaterial = mat; EditorUtility.SetDirty(r); }
        int tufts = BuildGrass(scene);
        ApplyWind();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Nature] ground material set, grass tufts {tufts} (outer terrain: run Roomies/Environment/Build Distant Backdrop)");
    }

    // ================================================================ 町の地面
    static Material TownGroundMaterial(Mesh planeMesh)
    {
        var macro = Tex("nature_ground_town", 2048, 1280, TownGroundPixel, 5f, out var macroN, false, true);
        var detail = Tex("nature_grass_detail", 512, 512, GrassDetailPixel, 3.5f, out var detailN);
        var m = Lit("M_Ground_Town", macro, macroN, Color.white, .06f, false, .8f);
        // 元の板の UV の範囲に合わせて、テクスチャ1枚が町全体に乗るようにする
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var uv in planeMesh.uv) { min = Vector2.Min(min, uv); max = Vector2.Max(max, uv); }
        var size = max - min; if (size.x < 1e-4f || size.y < 1e-4f) size = Vector2.one;
        var tile = new Vector2(1f / size.x, 1f / size.y);
        m.SetTextureScale("_BaseMap", tile); m.SetTextureOffset("_BaseMap", -Vector2.Scale(min, tile));
        // 細かい草の模様は 0.8m ごと（UV の 1 が町の幅 80m・奥行き 50m）
        Detail(m, detail, detailN, 1, .45f);
        m.SetFloat("_DetailAlbedoMapScale", .6f);
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(TownX * 2 / .8f * tile.x, TownZ * 2 / .8f * tile.y));
        Debug.Log($"[Nature] pPlane1 uv {min} - {max}");
        return m;
    }

    // 町の芝生：もとの緑のまま、明るい所・濃い所・少し乾いた所、まばらな土の地肌と小石、小さな花
    static void TownGroundPixel(float u, float v, out Color c, out float h)
    {
        float big = Fbm(u, v, 5, 4, 101), mid = Fbm(u, v, 24, 3, 103), fine = Fbm(u, v, 160, 2, 107);
        Color light = new Color(.52f, .68f, .43f), deep = new Color(.37f, .57f, .37f), dry = new Color(.6f, .65f, .43f);
        c = Color.Lerp(deep, light, Smooth(.3f, .72f, big));
        c = Color.Lerp(c, dry, Smooth(.62f, .8f, mid) * .45f);
        c *= .92f + .16f * fine;
        // 土の地肌（まばら）と小石
        float dirt = Smooth(.74f, .82f, Fbm(u, v, 14, 4, 109) + (mid - .5f) * .2f);
        float pebbleN = VNoise(u * 900, v * 560, 0, 113);
        float pebble = Smooth(.86f, .93f, pebbleN) * (dirt * .9f + .08f);
        c = Color.Lerp(c, new Color(.5f, .4f, .29f) * (.9f + .2f * fine), dirt * .85f);
        c = Color.Lerp(c, new Color(.66f, .64f, .6f), pebble);
        // 小さな花（白と黄色、とてもまばら）
        float flower = VNoise(u * 1400, v * 875, 0, 127);
        if (flower > .985f && dirt < .3f) c = Color.Lerp(c, VNoise(u * 300, v * 190, 0, 131) > .5f ? new Color(.98f, .96f, .9f) : new Color(.98f, .86f, .3f), .8f);
        c.a = 1;
        h = big * .3f + mid * .4f + fine * .3f - dirt * .4f + pebble * .8f;
    }

    // 近くで見たときの草の模様（灰色のまわりの明暗。URP のディテールは 0.5 で変わらない）
    static void GrassDetailPixel(float u, float v, out Color c, out float h)
    {
        float s = 0;
        for (int k = 0; k < 3; k++)
        {
            float a = k * 1.1f;
            float x = u * Mathf.Cos(a) - v * Mathf.Sin(a), y = u * Mathf.Sin(a) + v * Mathf.Cos(a);
            float streak = VNoise(x * 90, y * 18, 0, 200 + k);
            s += Mathf.Pow(streak, 3);
        }
        s /= 3;
        float n = Fbm(u, v, 8, 3, 211);
        float g = .42f + s * .3f + (n - .5f) * .12f;
        c = new Color(g * .96f, g * 1.04f, g * .94f, 1);
        h = s;
    }

    // ================================================================ 草の束
    static int BuildGrass(Scene scene)
    {
        var tex = GrassTuftTexture(out var texN);
        var mat = Lit("M_Grass_Tuft", tex, texN, new Color(.95f, 1f, .92f), .1f, true, .5f);
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GrassRoot);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(GrassRoot) { isStatic = true };
        SceneManager.MoveGameObjectToScene(root, scene);

        // 置いてはいけない所：地面より上にある見た目の物（歩道・道・建物・花壇・配達場所など）の足元
        var blockers = new List<Bounds>();
        foreach (var g in scene.GetRootGameObjects())
        {
            if (g.name == "Roomies_Backdrop" || g.name == GrassRoot || g.name.StartsWith("Roomies_Mining") || g.name == "Roomies_RoomRestyle") continue;
            foreach (var rr in g.GetComponentsInChildren<Renderer>())
            {
                if (rr.name == "pPlane1" || rr.GetComponentInParent<LODGroup>() != null || rr is ParticleSystemRenderer) continue;
                var b = rr.bounds;
                if (b.min.y > .6f || b.max.y < .01f) continue;
                if (b.size.x > 120 || b.size.z > 120) continue;
                b.Expand(new Vector3(.35f, 0, .35f));
                blockers.Add(b);
            }
        }
        // 区画ごと（10m）
        const float cell = 10f, step = .9f;
        var rnd = new System.Random(5150);
        float R() => (float)rnd.NextDouble();
        int total = 0;
        var meshes = new List<Mesh>();
        for (float cx = -TownX; cx < TownX; cx += cell)
            for (float cz = -TownZ; cz < TownZ; cz += cell)
            {
                var mb = new MB(1);
                int n = 0;
                for (float x = cx; x < cx + cell; x += step)
                    for (float z = cz; z < cz + cell; z += step)
                    {
                        float px = x + R() * step, pz = z + R() * step;
                        if (Mathf.Abs(px) > TownX - .3f || Mathf.Abs(pz) > TownZ - .3f) continue;
                        // まばらにする（ノイズで濃い所・薄い所）
                        float dens = Fbm((px + 40) / 80f, (pz + 25) / 50f, 6, 3, 301);
                        if (R() > Mathf.Lerp(.25f, .95f, dens)) continue;
                        var p = new Vector3(px, 0, pz);
                        bool blocked = false;
                        foreach (var b in blockers) if (px >= b.min.x && px <= b.max.x && pz >= b.min.z && pz <= b.max.z) { blocked = true; break; }
                        if (blocked) continue;
                        Tuft(mb, p, R(), R(), R());
                        n++;
                    }
                if (n == 0) continue;
                var mesh = StoreMesh(GroundMeshPath, mb.ToMesh($"grass_{cx:0}_{cz:0}"));
                meshes.Add(mesh);
                var go = new GameObject($"Grass {cx:0} {cz:0}") { isStatic = true };
                go.transform.SetParent(root.transform, false);
                var lodGo = new GameObject("LOD0") { isStatic = true };
                lodGo.transform.SetParent(go.transform, false);
                lodGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = lodGo.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.22f, new Renderer[] { r }) });
                lod.fadeMode = LODFadeMode.None;
                lod.RecalculateBounds();
                total += n;
            }
        // 使わなくなった区画のメッシュを消す
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(GroundMeshPath))
            if (o is Mesh m && m.name.StartsWith("grass_") && !meshes.Contains(m)) Object.DestroyImmediate(m, true);
        return total;
    }

    /// <summary>草の束1つ：十字に立てた板2枚（高さ 0.22〜0.45m）。法線は上向き（地面と同じ明るさに見える）。</summary>
    static void Tuft(MB mb, Vector3 p, float a, float b, float c)
    {
        float hgt = .22f + a * .23f, w = .32f + b * .25f;
        float yaw = c * Mathf.PI;
        for (int k = 0; k < 2; k++)
        {
            float ang = yaw + k * Mathf.PI / 2;
            var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * w * .5f;
            var lean = new Vector3(Mathf.Sin(ang * 3), 0, Mathf.Cos(ang * 2)) * .05f;
            var up = Vector3.up; var nTop = (Vector3.up + lean).normalized;
            float u0 = (k + (a > .5f ? 0 : 0)) * .5f;   // テクスチャの左右半分を使い分ける
            mb.Quad(p - d, p + d, p + d + Vector3.up * hgt + lean, p - d + Vector3.up * hgt + lean, up, up, nTop, nTop, 0, new Rect(u0, 0, .5f, 1));
        }
    }

    // 草の葉：下から伸びる細い葉を束ねる（左右2種類）
    static Texture2D GrassTuftTexture(out Texture2D normal)
    {
        const int W = 512;
        var col = new Color[W * W]; var hei = new float[W * W];
        var rnd = new System.Random(919);
        float R() => (float)rnd.NextDouble();
        for (int half = 0; half < 2; half++)
            for (int k = 0; k < 46; k++)
            {
                float x0 = half * .5f + .08f + R() * .34f, lean = (R() - .5f) * .5f, len = .45f + R() * .5f, wid = .006f + R() * .006f;
                var cBase = new Color(.2f, .38f, .17f); var cTip = Color.Lerp(new Color(.5f, .7f, .3f), new Color(.62f, .7f, .35f), R());
                int steps = 80;
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float x = x0 + lean * t * t * .5f, y = t * len;
                    float w = wid * (1 - t * .85f) * W;
                    int px = (int)(x * W), py = (int)(y * W);
                    for (int dx = -(int)w - 1; dx <= (int)w + 1; dx++)
                    {
                        int xx = px + dx; if (xx < half * W / 2 || xx >= (half + 1) * W / 2 || py < 0 || py >= W) continue;
                        if (Mathf.Abs(dx) > w) continue;
                        int i = py * W + xx;
                        var cc = Color.Lerp(cBase, cTip, t); cc.a = 1; col[i] = cc; hei[i] = 1 - Mathf.Abs(dx) / Mathf.Max(w, 1);
                    }
                }
            }
        return Save("nature_grass_tuft", W, W, col, hei, 2f, out normal, true, true);
    }

    // ================================================================ 境界の外の地面（遠景の配置ツールから呼ぶ）
    /// <summary>境界の外に、ゆるい起伏のある地面を作る。道・家・木などの足元は平らに残す。backdrop の子「Outer terrain」。</summary>
    internal static void BuildOuterTerrain(Transform backdrop)
    {
        var old = backdrop.Find(TerrainGroup);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject(TerrainGroup) { isStatic = true }.transform;
        group.SetParent(backdrop, false);

        // 平らに残す所：遠景の物（山・電車の高架の上の部分は除く）の足元。向きのある箱として持つ
        var feet = new List<(Matrix4x4 toLocal, Bounds b, Matrix4x4 toWorld)>();
        foreach (var r in backdrop.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.transform.IsChildOf(group) || r.name.StartsWith("Mountain")) continue;
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var wb = r.bounds; if (wb.min.y > 3f) continue;                 // 宙にある物（高架の床・電車）は関係ない
            var lb = mf.sharedMesh.bounds;
            feet.Add((r.transform.worldToLocalMatrix, lb, r.transform.localToWorldMatrix));
        }
        // 足元までの距離（m）。大きな物はたくさんあるので、まず大まかな箱で絞る
        var coarse = feet.Select(f => { var w = GeometryUtility.CalculateBounds(Corners(f.b), f.toWorld); return w; }).ToArray();
        float FootDistance(Vector3 p, float limit)
        {
            float best = limit;
            for (int i = 0; i < feet.Count; i++)
            {
                var cb = coarse[i];
                if (p.x < cb.min.x - limit || p.x > cb.max.x + limit || p.z < cb.min.z - limit || p.z > cb.max.z + limit) continue;
                var lp = feet[i].toLocal.MultiplyPoint3x4(p);
                var cp = feet[i].b.ClosestPoint(new Vector3(lp.x, feet[i].b.center.y, lp.z));
                var wp = feet[i].toWorld.MultiplyPoint3x4(new Vector3(cp.x, lp.y, cp.z));
                float d = new Vector2(wp.x - p.x, wp.z - p.z).magnitude;
                if (d < best) best = d;
            }
            return best;
        }
        float Height(float x, float z)
        {
            float ox = Mathf.Max(0, Mathf.Abs(x) - TownX), oz = Mathf.Max(0, Mathf.Abs(z) - TownZ);
            float d = Mathf.Sqrt(ox * ox + oz * oz);
            float amp = Smooth(10, 60, d) * (3f + 10f * Smooth(60, 350, d));
            float n = Fbm(x / 900f + .5f, z / 900f + .5f, 6, 4, 401);
            float hill = Mathf.Max(0, n * 1.5f - .45f);
            float h = amp * hill;
            if (h <= .01f) return 0;
            float foot = FootDistance(new Vector3(x, 0, z), 14f);
            return h * Smooth(3f, 14f, foot);
        }

        var tex = Tex("nature_ground_outer", 1024, 1024, OuterGroundPixel, 4f, out var texN);
        var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "nature_grass_detail.png");
        var detailN = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "nature_grass_detail_n.png");
        if (detail == null) detail = Tex("nature_grass_detail", 512, 512, GrassDetailPixel, 3.5f, out detailN);
        var mat = Lit("M_Ground_Outer", tex, texN, Color.white, .05f, false, .7f);
        Detail(mat, detail, detailN, 50f, .4f);
        mat.SetFloat("_DetailAlbedoMapScale", .5f);

        const float half = 450f, cellSize = 6f; const int chunkCells = 25;
        int cells = Mathf.RoundToInt(half * 2 / cellSize);
        var keep = new List<Mesh>();
        for (int ci = 0; ci < cells; ci += chunkCells)
            for (int cj = 0; cj < cells; cj += chunkCells)
            {
                var mb = new MB(1);
                int n1 = Mathf.Min(chunkCells, cells - ci), n2 = Mathf.Min(chunkCells, cells - cj);
                int start = mb.v.Count;
                for (int j = 0; j <= n2; j++)
                    for (int i = 0; i <= n1; i++)
                    {
                        float x = -half + (ci + i) * cellSize, z = -half + (cj + j) * cellSize;
                        float h = Height(x, z);
                        mb.v.Add(new Vector3(x, -.08f + h, z)); mb.n.Add(Vector3.up); mb.uv.Add(new Vector2(x / 40f, z / 40f));
                    }
                int quads = 0;
                for (int j = 0; j < n2; j++)
                    for (int i = 0; i < n1; i++)
                    {
                        float x0 = -half + (ci + i) * cellSize, z0 = -half + (cj + j) * cellSize;
                        // 町の地面の下（完全に内側の格子）は作らない
                        if (x0 >= -TownX && x0 + cellSize <= TownX && z0 >= -TownZ && z0 + cellSize <= TownZ) continue;
                        int a = start + j * (n1 + 1) + i, b = a + 1, c = a + n1 + 1, d = c + 1;
                        mb.t[0].Add(a); mb.t[0].Add(c); mb.t[0].Add(b);
                        mb.t[0].Add(b); mb.t[0].Add(c); mb.t[0].Add(d);
                        quads++;
                    }
                if (quads == 0) continue;
                var mesh = mb.ToMesh($"terrain_{ci}_{cj}");
                mesh.RecalculateNormals(); mesh.RecalculateTangents();
                mesh = StoreMesh(GroundMeshPath, mesh);
                keep.Add(mesh);
                var go = new GameObject($"Terrain {ci} {cj}") { isStatic = true };
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(GroundMeshPath))
            if (o is Mesh m && m.name.StartsWith("terrain_") && !keep.Contains(m)) Object.DestroyImmediate(m, true);
    }

    static Vector3[] Corners(Bounds b)
    {
        var mn = b.min; var mx = b.max;
        return new[] { new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mn.x, mx.y, mn.z), new Vector3(mx.x, mx.y, mn.z),
                       new Vector3(mn.x, mn.y, mx.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mx.y, mx.z), new Vector3(mx.x, mx.y, mx.z) };
    }

    // 境界の外の草地（12m で繰り返す）：草の濃淡と、ところどころの土
    static void OuterGroundPixel(float u, float v, out Color c, out float h)
    {
        float big = Fbm(u, v, 3, 4, 501), mid = Fbm(u, v, 12, 3, 503), fine = Fbm(u, v, 64, 2, 507);
        c = Color.Lerp(new Color(.38f, .57f, .35f), new Color(.46f, .64f, .39f), Smooth(.3f, .7f, big));
        c = Color.Lerp(c, new Color(.56f, .62f, .4f), Smooth(.6f, .78f, mid) * .3f);
        float dirt = Smooth(.74f, .84f, Fbm(u, v, 6, 4, 509));
        c = Color.Lerp(c, new Color(.5f, .42f, .31f), dirt * .5f);
        c *= .92f + .16f * fine; c.a = 1;
        h = big * .3f + mid * .4f + fine * .3f - dirt * .3f;
    }
}
#endif
