using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 折れ線グラフ（UI）。点の列を渡すと、枠いっぱいに線と、線の下の薄い塗りを描く（2026/11/25、部屋の PC の株アプリ）。
/// 物を作らず、この Graphic の形を作り直すだけ。色と太さはインスペクター。
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UILineChart : MaskableGraphic
{
    [SerializeField, Min(0.5f)] private float thickness = 4f;
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.12f);
    [Tooltip("上下の余白（枠の高さの割合）")]
    [SerializeField, Range(0f, 0.4f)] private float padding = 0.08f;

    private float[] points = new float[0];

    public int PointCount => points.Length;

    public void SetPoints(int[] values, Color lineColor)
    {
        if (values == null) values = new int[0];
        if (points.Length != values.Length) points = new float[values.Length];
        for (int i = 0; i < values.Length; i++) points[i] = values[i];
        color = lineColor;
        fillColor = new Color(lineColor.r, lineColor.g, lineColor.b, fillColor.a);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int n = points.Length;
        if (n < 2) return;
        Rect r = GetPixelAdjustedRect();
        float min = float.MaxValue, max = float.MinValue;
        foreach (float p in points) { min = Mathf.Min(min, p); max = Mathf.Max(max, p); }
        if (max - min < 0.0001f) { max += 1f; min -= 1f; }
        float padY = r.height * padding;
        Vector2 Pt(int i) => new Vector2(r.xMin + r.width * i / (n - 1), r.yMin + padY + (r.height - padY * 2) * (points[i] - min) / (max - min));

        // 線の下の塗り
        Color32 fill = fillColor, clear = new Color(fillColor.r, fillColor.g, fillColor.b, 0f);
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 a = Pt(i), b = Pt(i + 1);
            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(a.x, r.yMin), clear, Vector2.zero);
            vh.AddVert(new Vector3(a.x, a.y), fill, Vector2.zero);
            vh.AddVert(new Vector3(b.x, b.y), fill, Vector2.zero);
            vh.AddVert(new Vector3(b.x, r.yMin), clear, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }
        // 線（区間ごとの四角と、つなぎ目の小さな四角）
        Color32 line = color;
        float h = thickness * 0.5f;
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 a = Pt(i), b = Pt(i + 1);
            Vector2 d = (b - a).normalized;
            Vector2 nrm = new Vector2(-d.y, d.x) * h;
            int v = vh.currentVertCount;
            vh.AddVert(a - nrm, line, Vector2.zero);
            vh.AddVert(a + nrm, line, Vector2.zero);
            vh.AddVert(b + nrm, line, Vector2.zero);
            vh.AddVert(b - nrm, line, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }
        for (int i = 1; i < n - 1; i++)
        {
            Vector2 c = Pt(i);
            int v = vh.currentVertCount;
            vh.AddVert(c + new Vector2(-h, -h), line, Vector2.zero);
            vh.AddVert(c + new Vector2(-h, h), line, Vector2.zero);
            vh.AddVert(c + new Vector2(h, h), line, Vector2.zero);
            vh.AddVert(c + new Vector2(h, -h), line, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v, v + 2, v + 3);
        }
        // いまの値段の点
        Vector2 last = Pt(n - 1);
        float dot = thickness * 1.6f;
        int w = vh.currentVertCount;
        vh.AddVert(last + new Vector2(-dot, -dot), line, Vector2.zero);
        vh.AddVert(last + new Vector2(-dot, dot), line, Vector2.zero);
        vh.AddVert(last + new Vector2(dot, dot), line, Vector2.zero);
        vh.AddVert(last + new Vector2(dot, -dot), line, Vector2.zero);
        vh.AddTriangle(w, w + 1, w + 2);
        vh.AddTriangle(w, w + 2, w + 3);
    }
}
