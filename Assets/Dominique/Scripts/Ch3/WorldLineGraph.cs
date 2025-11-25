using UnityEngine;

/// Draws ONE continuous world-space line through the given points (1→…→6).
/// Optional: keep a constant screen-space thickness in VR.
[RequireComponent(typeof(LineRenderer))]
public class WorldLineGraph : MonoBehaviour
{
    [Header("Points (world-space, in order)")]
    public Transform[] points;          // drag your 6 red-dot Transforms here
    public bool closedLoop = false;     // false for a line graph

    [Header("Appearance")]
    public Color lineColor = Color.red;
    [Tooltip("World meters if Constant Screen Width is OFF.")]
    public float worldWidth = 0.01f;

    [Header("VR: constant screen thickness")]
    public bool constantScreenWidth = true;
    [Tooltip("Desired thickness in screen pixels (per eye).")]
    public float pixelWidth = 3f;
    public Camera targetCamera;         // leave empty = Camera.main

    [Header("Performance")]
    public bool updateEveryFrame = true;

    LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();

        // Basic LineRenderer setup for a clean, camera-facing polyline
        lr.useWorldSpace   = true;
        lr.loop            = closedLoop;
        lr.alignment       = LineAlignment.View;   // face the camera
        lr.textureMode     = LineTextureMode.Stretch;
        lr.numCornerVertices = 2;                  // nicer joins
        lr.numCapVertices    = 2;                  // rounded ends
        lr.startColor = lr.endColor = lineColor;

        if (lr.sharedMaterial == null)
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        if (!targetCamera) targetCamera = Camera.main;

        ApplyPositions();
        ApplyWidth();
    }

    void LateUpdate()
    {
        if (updateEveryFrame)
        {
            ApplyPositions();
            ApplyWidth();
        }
    }

    void OnValidate()
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        if (!Application.isPlaying)
        {
            lr.useWorldSpace = true;
            lr.loop = closedLoop;
            lr.startColor = lr.endColor = lineColor;
        }
        ApplyPositions();
        ApplyWidth();
    }

    void ApplyPositions()
    {
        if (points == null || points.Length < 2) return;

        int n = points.Length + (closedLoop ? 1 : 0);
        lr.positionCount = n;

        // set positions in order; if closed, repeat the first at the end
        int i = 0;
        for (; i < points.Length; i++)
        {
            if (points[i]) lr.SetPosition(i, points[i].position);
        }
        if (closedLoop && points[0])
            lr.SetPosition(i, points[0].position);
    }

    void ApplyWidth()
    {
        if (!lr) return;

        if (!constantScreenWidth || !targetCamera)
        {
            lr.startWidth = lr.endWidth = Mathf.Max(0.0001f, worldWidth);
            return;
        }

        // Convert desired pixel width → world meters at the line’s average distance
        // pixelSizeWorld = 2 * d * tan(fov/2) / screenHeight
        // widthWorld = pixelSizeWorld * pixelWidth
        float d = AverageDistanceToCamera();
        float fovRad = targetCamera.fieldOfView * Mathf.Deg2Rad;
        float pixelSizeWorld = 2f * d * Mathf.Tan(fovRad * 0.5f) / Mathf.Max(1f, targetCamera.pixelHeight);
        float widthWorldPixels = Mathf.Max(0.00005f, pixelSizeWorld * pixelWidth);

        lr.startWidth = lr.endWidth = widthWorldPixels;
    }

    float AverageDistanceToCamera()
    {
        if (!targetCamera || points == null || points.Length == 0) return 1f;
        Vector3 camPos = targetCamera.transform.position;
        float sum = 0f; int count = 0;

        for (int i = 0; i < points.Length; i++)
        {
            if (!points[i]) continue;
            sum += Vector3.Distance(camPos, points[i].position);
            count++;
        }
        return (count > 0) ? sum / count : 1f;
    }
}
