using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UILineGraph : Graphic
{
    [Header("Points (UI, in order)")]
    public List<RectTransform> points = new List<RectTransform>();

    [Header("Style")]
    [Min(1f)] public float thickness = 3f;
    public bool closedLoop = false;   // keep OFF for a line graph

    void LateUpdate() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Count < 2) return;

        // Convert dot world positions -> this RectTransform's local space
        var cam = canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (canvas ? canvas.worldCamera : null);

        int n = points.Count;
        var lp = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            if (!points[i]) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                RectTransformUtility.WorldToScreenPoint(cam, points[i].position),
                cam,
                out lp[i]);
        }

        int segCount = closedLoop ? n : n - 1;
        if (segCount < 1) return;

        // Segment normals
        var segNormals = new Vector2[segCount];
        for (int i = 0; i < segCount; i++)
        {
            Vector2 p0 = lp[i];
            Vector2 p1 = lp[(i + 1) % n];
            Vector2 dir = (p1 - p0).normalized;
            segNormals[i] = new Vector2(-dir.y, dir.x); // perp
        }

        // Miter (join) normals per point to keep a single strip
        var miter = new Vector2[n];
        float half = thickness * 0.5f;

        for (int i = 0; i < n; i++)
        {
            Vector2 n0 = segNormals[(i - 1 + segCount) % segCount];
            Vector2 n1 = segNormals[i % segCount];

            if (!closedLoop && i == 0)        miter[i] = n1;   // start cap
            else if (!closedLoop && i == n-1) miter[i] = n0;   // end cap
            else
            {
                // miter = normalized(n0 + n1), scale to preserve thickness
                Vector2 m = (n0 + n1).normalized;
                float denom = Mathf.Max(0.0001f, Vector2.Dot(m, n1));
                miter[i] = m * (1f / denom);
            }
        }

        // Verts (left/right per point)
        for (int i = 0; i < n; i++)
        {
            Vector2 offset = miter[i] * half;

            AddVert(vh, lp[i] + offset, color);
            AddVert(vh, lp[i] - offset, color);
        }

        // Triangles for the strip
        for (int i = 0; i < segCount; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = ((i + 1) % n) * 2;
            int i3 = i2 + 1;

            vh.AddTriangle(i0, i2, i1);
            vh.AddTriangle(i2, i3, i1);
        }
    }

    static void AddVert(VertexHelper vh, Vector2 pos, Color32 col)
    {
        var v = UIVertex.simpleVert;
        v.color = col;
        v.position = pos;
        vh.AddVert(v);
    }
}
