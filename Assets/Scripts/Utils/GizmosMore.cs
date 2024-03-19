using UnityEngine;
using System.Collections.Generic;

public static class GizmosMore
{
    public static void DrawArrow(in Vector3 from, in Vector3 to)
    {
        Color color = Gizmos.color;
        Gizmos.DrawLine(from, to);
        Vector3 direction = to - from;
        Vector3 right = (Quaternion.Euler(0, 0, 30f) * -direction) * 0.1f;
        Vector3 left = (Quaternion.Euler(0, 0, -30f) * -direction) * 0.1f;
        Gizmos.color = color * 0.5f;
        Gizmos.DrawLine(to, right + to);
        Gizmos.DrawLine(to, left + to);
        Gizmos.color = color;
    }

    public static void DrawTriangle(in Vector3 a, in Vector3 b, in Vector3 c)
    {
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, a);
    }

    public static void DrawTriangle(List<Vector2> vertices, List<int> triangles, int triangle)
    {
        if (0 <= triangle && triangle + 2 < triangles.Count)
        {
            triangle = triangle / 3 * 3;
            Vector3Int triangleInts = new Vector3Int(triangles[triangle], triangles[triangle + 1], triangles[triangle + 2]);

            DrawTriangle(vertices[triangleInts.x], vertices[triangleInts.y], vertices[triangleInts.z]);
        }
    }
}
