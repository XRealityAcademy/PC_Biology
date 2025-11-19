using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UILineConnector : Graphic
{
    [Header("Points (UI, in order)")]
    public List<RectTransform> points = new List<RectTransform>();

    [Header("Style")]
    [Min(1f)] public float thickness = 3f;   // pixels
    public bool closedLoop = false;          // usually false for a line graph

    // smooth corners (simple miter)
    [Range(0f, 1f)] public float joinSmoothing = 0.5f;

    void LateUpdate() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (points == null || points.Count < 2) return;

        // 1) Convert all point world positions -> local space of this rect
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        List<Vector2> lp = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            if (!points[i]) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                RectTransformUtility.WorldToScreenPoint(cam, points[i].position),
                cam,
                out Vector2 p);
            lp.Add(p);
        }
        if (closedLoop) lp.Add(lp[0]); // repeat first at end

        int n = lp.Count;
        if (n < 2) return;

        float half = thickness * 0.5f;

        // 2) Build a single polystrip (two verts per point with simple join)
        var lefts  = new Vector2[n];
        var rights = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = lp[ Mathf.Clamp(i - 1, 0, n - 1) ];
            Vector2 curr = lp[i];
            Vector2 next = lp[ Mathf.Clamp(i + 1, 0, n - 1) ];

            Vector2 d1 = (curr - prev).normalized;
            Vector2 d2 = (next - curr).normalized;

            // average direction for smoother joins
            Vector2 avg = (d1 + d2).normalized;
            if (avg == Vector2.zero) avg = (d1 == Vector2.zero ? (d2 == Vector2.zero ? Vector2.right : d2) : d1);

            // perpendicular to averaged dir
            Vector2 nrm = new Vector2(-avg.y, avg.x);

            // soften the corner thickness a bit
            float cornerScale = Mathf.Lerp(1f, Mathf.Abs(Vector2.Dot(nrm, new Vector2(-d1.y, d1.x))), joinSmoothing);

            lefts[i]  = curr + nrm * half * cornerScale;
            rights[i] = curr - nrm * half * cornerScale;
        }

        // 3) Add verts & triangles for the strip
        int vertStart = 0;
        for (int i = 0; i < n; i++)
        {
            var v = UIVertex.simpleVert; v.color = color;

            v.position = lefts[i];  vh.AddVert(v);
            v.position = rights[i]; vh.AddVert(v);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int i0 = vertStart + i * 2;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int i3 = i0 + 3;

            vh.AddTriangle(i0, i2, i1);
            vh.AddTriangle(i2, i3, i1);
        }
    }
}
