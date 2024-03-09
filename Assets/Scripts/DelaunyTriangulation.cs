using System;
using System.Collections.Generic;
using UnityEngine;
public struct DelaunayTriangulation2D
{
    public static void Triangulate(List<Vector2> vertices, List<int> triangles)
    {
        if (vertices.Count <= 2) return;

        // Scaling into ([0,1], [0,1])
        float scale;
        Vector2 scaleCenter;
        {
            Vector2
                min = vertices[0],
                max = vertices[1];
            foreach (Vector2 v in vertices)
            {
                if (v.x < min.x)
                    min.x = v.x;
                else if (max.x < v.x)
                    max.x = v.x;

                if (v.y < min.y)
                    min.y = v.y;
                else if (max.y < v.y)
                    max.y = v.y;
            }

            float dx = max.x - min.x;
            float dy = max.y - min.y;
            if (dx < dy)
                // tall []
                scale = dy;
            else
                // wide ==
                scale = dx;

            if (scale == 0f)
                return;
            scale *= 0.5f;
            float scaleInverse = 1f / scale;
            scaleCenter = (max + min) * 0.5f;

            Span<Vector2> span = vertices.AsSpan();
            foreach (ref Vector2 v in span)
            {
                v = (v - scaleCenter) * scaleInverse;
            }
        }

        // TODO bins (optional)

        // add big triangle
        {
            vertices.Reserve(vertices.Count + 3);
            triangles.Reserve(vertices.Count / 3 + 3);

            vertices.Add(new Vector2(+100f, +000f));
            vertices.Add(new Vector2(-050f, +087f));
            vertices.Add(new Vector2(-050f, -087f));
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 2);
            triangles.Add(vertices.Count - 1);
        }

        triangles.TrimExcess();
    }

    public static bool IsInTriangle(in Vector2 vertex, List<Vector2> vertices, List<int> triangles, int triangleIndex)
    {
        Span<int> triangleSpan = triangles.AsSpan().Slice(triangleIndex, 3);         
        Span<Vector2> verticesSpan = vertices.AsSpan();

        ref Vector2 v0 = ref verticesSpan[triangleSpan[0]];
        ref Vector2 v1 = ref verticesSpan[triangleSpan[1]];
        ref Vector2 v2 = ref verticesSpan[triangleSpan[2]];

        Vector2 n0 = Vector2.Perpendicular(v1 - v0);
        Vector2 n1 = Vector2.Perpendicular(v2 - v1);
        Vector2 n2 = Vector2.Perpendicular(v0 - v2);

        float d0 = Vector2.Dot(vertex - v0, n0);
        float d1 = Vector2.Dot(vertex - v1, n1);
        float d2 = Vector2.Dot(vertex - v2, n2);

        ValueTuple<bool, bool> d0Res = IsPositiveOrNegativeIncludingZero(d0);
        ValueTuple<bool, bool> d1Res = IsPositiveOrNegativeIncludingZero(d1);
        ValueTuple<bool, bool> d2Res = IsPositiveOrNegativeIncludingZero(d2);

        if (d0Res.Item1 == d1Res.Item1 == d2Res.Item1)
            return true;
        else if (d0Res.Item2 == d1Res.Item2 == d2Res.Item2)
            return true;
        else
            return false;
    }

    public static ValueTuple<bool, bool> IsPositiveOrNegativeIncludingZero(float f)
    {
        if (Mathf.Approximately(f, 0f))
            return new(true, true);
        else
        {
            bool isPositive = 0f < f;
            return new(isPositive, !isPositive);
        }
    }
}
