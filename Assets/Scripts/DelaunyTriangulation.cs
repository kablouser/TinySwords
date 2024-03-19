using System;
using System.Collections.Generic;
using UnityEngine;
public struct DelaunayTriangulation2D
{
    public static void Triangulate(
        List<Vector2> vertices, List<int> triangles,
        List<Vector2Int> constrainedEdges,
        //DEBUG TODO REMOVE
        int steps,
        List<Vector2Int> satisfiedEdges
        )
    {
        triangles.Clear();
        satisfiedEdges.Clear();

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

        int expectedTrianglesCount = 2 * vertices.Count / 3 + 3;
        // add big triangle
        {
            // remove vertex duplicates
            HashSet<Vector2> uniqueVertices = new HashSet<Vector2>(vertices);

            //TODO
            /*          vertices.Add(new Vector2(+100f, +000f));
                        vertices.Add(new Vector2(-050f, +087f));
                        vertices.Add(new Vector2(-050f, -087f));*/
            /*            vertices.Add(new Vector2(+10f, +00f));
                        vertices.Add(new Vector2(-05f, +09f));
                        vertices.Add(new Vector2(-05f, -09f));*/

            Vector2 bigTriangleA = new Vector2(+00f, +10f);
            Vector2 bigTriangleB = new Vector2(-09f, -05f);
            Vector2 bigTriangleC = new Vector2(+09f, -05f);

            uniqueVertices.Remove(bigTriangleA);
            uniqueVertices.Remove(bigTriangleB);
            uniqueVertices.Remove(bigTriangleC);

            vertices.Clear();
            vertices.InsertRange(0, uniqueVertices);

            vertices.Reserve(vertices.Count + 3);
            triangles.Reserve(expectedTrianglesCount);

            vertices.Add(new Vector2(+00f, +10f));
            vertices.Add(new Vector2(-09f, -05f));
            vertices.Add(new Vector2(+09f, -05f));

            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 2);
            triangles.Add(vertices.Count - 1);
        }

        // find triangle 
        {
            // adjacency[vertexIndexOppositeSide] = adjacentTriangle
            List<int> adjacency = new List<int>(expectedTrianglesCount)
            {
                -1, -1, -1
            };

            Stack<int> dirtyTriangles = new Stack<int>(expectedTrianglesCount);
            // vertices no more Add()
            Span<Vector2> verticesSpan = vertices.AsSpan();

            for (int vertexIndex = 0; vertexIndex < vertices.Count - 3 && 0 < steps; vertexIndex++)
            {
                // TODO navigate to new triangle from previous worked triangle (optional)

                for (int triangle = 0; triangle < triangles.Count; triangle += 3)
                {
                    if (IsInTriangle(verticesSpan[vertexIndex], vertices, triangles, triangle))
                    {
                        /* Delete triangle:
                         * 
                         * 
                         *                 x
                         *
                         *               vNew
                         *
                         *      y                   z
                         */
                        Vector3Int oldTriangle = new Vector3Int(
                            triangles[triangle],
                            triangles[triangle + 1],
                            triangles[triangle + 2]);
                        // top left triangle
                        triangles[triangle] = vertexIndex;
                        triangles[triangle + 1] = oldTriangle.x;
                        triangles[triangle + 2] = oldTriangle.y;
                        //int topLeftTriangle = triangleIndex;
                        // bottom triangle
                        int bottomTriangle = triangles.Count;
                        triangles.Add(vertexIndex);
                        triangles.Add(oldTriangle.y);
                        triangles.Add(oldTriangle.z);
                        // top right triangle
                        int topRightTriangle = triangles.Count;
                        triangles.Add(vertexIndex);
                        triangles.Add(oldTriangle.z);
                        triangles.Add(oldTriangle.x);

                        // adjacency
                        Vector3Int oldAdjacency = new Vector3Int(
                            adjacency[triangle],
                            adjacency[triangle + 1],
                            adjacency[triangle + 2]);

                        /*
                         *                x
                         *                              ______
                         *           original tri       \adjacent
                         *                               \  /
                         *      y                  z      \/ 
                         */
                        void UpdateTriangleAdjacency(int adjacentTriangle, int oldTriangle, int newTriangle, List<int> adjacency)
                        {
                            if (-1 != adjacentTriangle)
                            {
                                for (int i = adjacentTriangle; i < adjacentTriangle + 3; i++)
                                {
                                    if (adjacency[i] == oldTriangle)
                                    {
                                        adjacency[i] = newTriangle;
                                        break;
                                    }
                                }
                            }
                        }

                        // bottom neighbour
                        UpdateTriangleAdjacency(oldAdjacency.x, triangle, bottomTriangle, adjacency);
                        // top right neighbour
                        UpdateTriangleAdjacency(oldAdjacency.y, triangle, topRightTriangle, adjacency);
                        // top left neighbour
                        //UpdateTriangleAdjacency(oldAdjacency.z, triangleIndex, triangleIndex, adjacency);

                        /*                 x
                         *
                         *               vNew
                         *
                         *      y                  z
                         */
                        // adjacency of new triangles
                        // top left triangle
                        adjacency[triangle] = oldAdjacency.z;
                        adjacency[triangle + 1] = bottomTriangle;
                        adjacency[triangle + 2] = topRightTriangle; 
                        // bottom triangle
                        adjacency.Add(oldAdjacency.x);
                        adjacency.Add(topRightTriangle);
                        adjacency.Add(triangle);
                        // top right triangle
                        adjacency.Add(oldAdjacency.y);
                        adjacency.Add(triangle);
                        adjacency.Add(bottomTriangle);

                        // stacking
                        dirtyTriangles.Push(triangle);
                        dirtyTriangles.Push(bottomTriangle);
                        dirtyTriangles.Push(topRightTriangle);

                        do
                        {
                            int dirtyTriangle = dirtyTriangles.Pop();
                            int adjacentTriangle = adjacency[dirtyTriangle];

                            if (adjacentTriangle == -1)
                                continue;

                            int v1Index = triangles[dirtyTriangle + 2];
                            int v2Index = triangles[dirtyTriangle + 1];
                            int v3Index = -1;
                            int adjacentTriangleV1 = -1;
                            int adjacentTriangleV2 = -1;

                            for (int i = 0; i < 3; ++i)
                            {
                                int viIndex = triangles[adjacentTriangle + i];
                                if (viIndex == v1Index ||
                                    viIndex == v2Index)
                                    continue;

                                adjacentTriangleV1 = adjacentTriangle + (i + 1) % 3;
                                adjacentTriangleV2 = adjacentTriangle + (i + 2) % 3;
                                v3Index = viIndex;
                                break;
                            }

                            ref Vector2 p = ref verticesSpan[triangles[dirtyTriangle]];
                            ref Vector2 v1 = ref verticesSpan[v1Index];
                            ref Vector2 v2 = ref verticesSpan[v2Index];
                            ref Vector2 v3 = ref verticesSpan[v3Index];

                            Vector2 v13 = v1 - v3;
                            Vector2 v23 = v2 - v3;
                            Vector2 v1p = v1 - p;
                            Vector2 v2p = v2 - p;

                            float cosA = Vector2.Dot(v13, v23);
                            float cosB = Vector2.Dot(v1p, v2p);

                            bool isSwap;

                            if (0 <= cosA && 0 <= cosB)
                                isSwap = false;
                            else if (cosA < 0 && cosB < 0)
                                isSwap = true;
                            else
                            {
                                float sinA = Cross(v13, v23);
                                float sinB = Cross(v2p, v1p);
                                float sinAB = sinA * cosB + sinB * cosA;
                                isSwap = sinAB < 0;
                            }

                            if (isSwap)
                            {
                                // get adjacents before changing triangle soup
                                int triangleA = adjacency[adjacentTriangleV1];
                                int triangleB = adjacency[adjacentTriangleV2];
                                int triangleC = adjacency[dirtyTriangle + 1];

                                // L triangle
                                int pIndex = triangles[dirtyTriangle];
                                triangles[dirtyTriangle + 1] = v2Index;
                                triangles[dirtyTriangle + 2] = v3Index;

                                // R triangle
                                triangles[adjacentTriangle] = pIndex;
                                triangles[adjacentTriangle + 1] = v3Index;
                                triangles[adjacentTriangle + 2] = v1Index;

                                UpdateTriangleAdjacency(triangleA, adjacentTriangle, dirtyTriangle, adjacency);
                                UpdateTriangleAdjacency(triangleC, dirtyTriangle, adjacentTriangle, adjacency);

                                // L triangle
                                adjacency[dirtyTriangle] = triangleA;
                                adjacency[dirtyTriangle + 1] = adjacentTriangle;
                                // adjacency[dirtyTriangle + 2] = unchanged;

                                // R triangle
                                adjacency[adjacentTriangle] = triangleB;
                                adjacency[adjacentTriangle + 1] = triangleC;
                                adjacency[adjacentTriangle + 2] = dirtyTriangle;

                                if (triangleA != -1)
                                    dirtyTriangles.Push(dirtyTriangle);
                                if (triangleB != -1)
                                    dirtyTriangles.Push(adjacentTriangle);
                            }
                        }
                        while (0 < dirtyTriangles.Count && 0 < steps);
                        break;
                    }
                }
                steps--;
            }
        }

        // Remove big triangles
        if (steps > 0)
        {
            steps--;
            vertices.RemoveRange(vertices.Count - 3, 3);

            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                if (vertices.Count <= triangles[i] ||
                    vertices.Count <= triangles[i + 1] ||
                    vertices.Count <= triangles[i + 2])
                {
                    triangles.RemoveRange(i, 3);
                    i -= 3;
                }
            }
        }

        // constraint
        {
            Span<Vector2Int> constrainedEdgesSpan = constrainedEdges.AsSpan();
            Span<Vector2> verticesSpan = vertices.AsSpan();
            Span<int> trianglesSpan = triangles.AsSpan();


            HashSet<int> searchedParallelEdges = new HashSet<int>(8);
            List<int> searchingParallelEdges = new List<int>(8);

            List<int> traverseEdges = new List<int>(8);

            foreach (ref Vector2Int constrainedEdge in constrainedEdgesSpan)
            {
                if (steps > 0)
                {
                    steps--;
                }
                else
                    break;

                if (constrainedEdge.x == constrainedEdge.y ||

                    !(
                    0 <= constrainedEdge.x && constrainedEdge.x < vertices.Count &&
                    0 <= constrainedEdge.y && constrainedEdge.y < vertices.Count))
                {
                    continue;
                }

                searchedParallelEdges.Clear();
                searchingParallelEdges.Clear();
                traverseEdges.Clear();

                {
                    int matchCountPerTriangle = 0;
                    for (int edge = 0; edge < triangles.Count && matchCountPerTriangle < 2; edge++)
                    {
                        if (edge % 3 == 0)
                        {
                            matchCountPerTriangle = 0;
                        }

                        int vertexIndex = trianglesSpan[edge];
                        if (vertexIndex == constrainedEdge.x)
                        {
                            int face = edge / 3 * 3;
                            int edge0 = face + (edge + 1) % 3;
                            int edge1 = face + (edge + 2) % 3;

                            searchedParallelEdges.Add(edge0);
                            searchedParallelEdges.Add(edge1);

                            searchingParallelEdges.Add(edge0);
                            searchingParallelEdges.Add(edge1);

                            matchCountPerTriangle++;
                        }
                        else if (vertexIndex == constrainedEdge.y)
                        {
                            matchCountPerTriangle++;
                        }
                    }
                    if (2 <= matchCountPerTriangle)
                    {
                        satisfiedEdges.Add(constrainedEdge);
                        continue;
                    }
                }

                traverseEdges.AddRange(searchingParallelEdges);

                Vector2 xVertex = verticesSpan[constrainedEdge.x];
                Vector2 yVertex = verticesSpan[constrainedEdge.y];
                Vector2 edgeDirection = yVertex - xVertex;

                bool foundParallel = false;
                while (0 < searchingParallelEdges.Count && !foundParallel)
                {
                    int neighbourTriangleIndex = searchingParallelEdges[searchingParallelEdges.Count - 1];
                    searchingParallelEdges.RemoveAt(searchingParallelEdges.Count - 1);
                    int vertexIndex = trianglesSpan[neighbourTriangleIndex];
                    Vector2 vertex = verticesSpan[trianglesSpan[neighbourTriangleIndex]];

                    if (Mathf.Abs(Cross(vertex - xVertex, edgeDirection)) < 1E-05 &&
                        0f < Vector2.Dot(vertex - xVertex, edgeDirection))
                    {
                        // exactly parallel, traverse edge
                        if (vertexIndex == constrainedEdge.y)
                        {
                            // found parallel line
                            foundParallel = true;
                            break;
                        }

                        // TODO improve search by rotating around v0 instead
                        for (int edge = 0; edge < triangles.Count; edge++)
                        {
                            if (trianglesSpan[edge] == vertexIndex)
                            {
                                int face = edge / 3 * 3;
                                int edge0 = face + (edge + 0) % 3;
                                int edge1 = face + (edge + 1) % 3;
                                int edge2 = face + (edge + 2) % 3;

                                if (searchedParallelEdges.Add(edge0))
                                {
                                    searchingParallelEdges.Add(edge0);
                                }
                                if (searchedParallelEdges.Add(edge1))
                                {
                                    searchingParallelEdges.Add(edge1);
                                }
                                if (searchedParallelEdges.Add(edge2))
                                {
                                    searchingParallelEdges.Add(edge2);
                                }
                            }
                        }
                    }
                }

                if (foundParallel)
                {
                    satisfiedEdges.Add(constrainedEdge);
                    continue;
                }
            }
        }


        if (0 < steps) {
            steps--;
            Span<Vector2> span = vertices.AsSpan();
            foreach (ref Vector2 v in span)
            {
                v = v * scale + scaleCenter;
            }
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

        if (d0 == 0f && d1 == 0f && d2 == 0f)
            return false;

        ValueTuple<bool, bool> d0Res = IsPositiveOrNegativeIncludingZero(d0);
        ValueTuple<bool, bool> d1Res = IsPositiveOrNegativeIncludingZero(d1);
        ValueTuple<bool, bool> d2Res = IsPositiveOrNegativeIncludingZero(d2);

        if (d0Res.Item1 == d1Res.Item1 && d1Res.Item1 == d2Res.Item1)
            return true;
        else if (d0Res.Item2 == d1Res.Item2 && d1Res.Item2 == d2Res.Item2)
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

    public static float Cross(in Vector2 a, in Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    // Line direction -AP> relative to -AB>
    public static float GetDirection(in Vector2 A, in Vector2 B, in Vector2 P)
    {
        return Cross(P - A, B - A);
    }

    // Does line seg [a1, b1] intersect [a2, b2]
    public static bool IsLinesIntersect(in Vector2 a1, in Vector2 b1, in Vector2 a2, in Vector2 b2)
    {
        Vector2 a1b1 = b1 - a1;
        Vector2 b1b2 = b2 - b1;
        Vector2 b1a2 = a2 - b1;
        Vector2 a2b2 = b2 - a2;
        Vector2 b2b1 = b1 - b2;
        Vector2 b2a1 = a1 - b2;
        return
            Cross(a1b1, b1b2) * Cross(a1b1, b1a2) < 0f &&
            Cross(a2b2, b2b1) * Cross(a2b2, b2a1) < 0f;
    }

    public static float GetSignExcludeZero(float f)
    {
        if (f < 0f)
            return -1f;
        else if (0f < f)
            return 1f;
        else
            return 0f;
    }

    public static bool IsQuadConvex(in Vector2 a, in Vector2 b, in Vector2 c, in Vector2 d)
    {
        Vector2 ab = b - a;
        Vector2 bc = c - b;
        Vector2 cd = d - c;
        Vector2 da = a - d;
        float abCross = GetSignExcludeZero(Cross(da, ab));
        float bcCross = GetSignExcludeZero(Cross(ab, bc));
        float cdCross = GetSignExcludeZero(Cross(bc, cd));
        float daCross = GetSignExcludeZero(Cross(cd, da));
        return
            abCross != 0f &&
            abCross == bcCross &&
            bcCross == cdCross &&
            cdCross == daCross;
    }
}
