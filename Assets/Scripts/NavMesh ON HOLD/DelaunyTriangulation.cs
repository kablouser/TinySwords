using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public struct DelaunayTriangulation2D
{
    public static void Triangulate(
        List<Vector2> vertices, List<int> triangles,
        List<Vector2Int> constrainedEdges,
        //DEBUG TODO REMOVE
        int steps,
        List<Vector2Int> intersectingEdges,
        List<int> adjacency
        )
    {
        triangles.Clear();
        intersectingEdges.Clear();

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

        // adjacency[triangleIndex] = adjacentTriangle
        //List<int> adjacency;
        // find triangle 
        {
            /*            adjacency = new List<int>(expectedTrianglesCount)
                        {
                            -1, -1, -1
                        };*/
            adjacency.Clear();
            adjacency.Add(-1);
            adjacency.Add(-1);
            adjacency.Add(-1);

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

            //TODO remove after debugging
            List<int> offsets = new List<int>(triangles.Count / 3);
            int currentOffset = 0;

            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                if (vertices.Count <= triangles[i] ||
                    vertices.Count <= triangles[i + 1] ||
                    vertices.Count <= triangles[i + 2])
                {
                    triangles.RemoveRange(i, 3);
                    adjacency.RemoveRange(i, 3);
                    i -= 3;

                    currentOffset -= 3;
                    offsets.Add(-1);
                }
                else
                {
                    offsets.Add(currentOffset);
                }
            }

            for (int i = 0; i < adjacency.Count; i++)
            {
                int adj = adjacency[i];
                if (adj != -1)
                {
                    int off = offsets[adj / 3];
                    if (off == -1)
                    {
                        adjacency[i] = -1;
                    }
                    else
                    {
                        adjacency[i] = adj + off;
                    }
                }
            }
            if (triangles.Count < adjacency.Count)
                adjacency.RemoveRange(triangles.Count, adjacency.Count - triangles.Count);
        }

        // constraint
        {
            Span<Vector2Int> constrainedEdgesSpan = constrainedEdges.AsSpan();

            Stack<int> visitTriangles = new Stack<int>(16);
            HashSet<int> visitedTriangles = new HashSet<int>(16);

            foreach (ref Vector2Int constrainedEdge in constrainedEdgesSpan)
            {
                if (steps > 0)
                    steps--;
                else
                    break;

                if (constrainedEdge.x == constrainedEdge.y ||

                    !(
                    0 <= constrainedEdge.x && constrainedEdge.x < vertices.Count &&
                    0 <= constrainedEdge.y && constrainedEdge.y < vertices.Count))
                {
                    continue;
                }

                Vector2Int constrainedEdgePossiblySwapped = new Vector2Int();
                int startTriangleIndex = -1;
                for (int i = 0; i < triangles.Count; i++)
                {
                    int vertexIndex = triangles[i];
                    if (vertexIndex == constrainedEdge.x)
                    {
                        constrainedEdgePossiblySwapped = constrainedEdge;
                        startTriangleIndex = i;
                        break;
                    }
                    else if(vertexIndex == constrainedEdge.y)
                    {
                        constrainedEdgePossiblySwapped = new Vector2Int(constrainedEdge.y, constrainedEdge.x);
                        startTriangleIndex = i;
                        break;
                    }
                }

                if (startTriangleIndex != -1)
                {
                    IntersectConstrainedEdge intersectConstrainedEdge = new IntersectConstrainedEdge
                    {
                        vertices = vertices,
                        triangles = triangles,
                        adjacency = adjacency,

                        constrainedEdgeIndices = constrainedEdgePossiblySwapped,
                        constrainedVertex0 = vertices[constrainedEdgePossiblySwapped.x],
                        constrainedVertex1 = vertices[constrainedEdgePossiblySwapped.y],
                        constrainedEdge = vertices[constrainedEdgePossiblySwapped.y] - vertices[constrainedEdgePossiblySwapped.x],

                        visitTriangles = visitTriangles,
                        visitedTriangles = visitedTriangles,

                        isEnded = false,
                        nextAroundTriangleIndex = startTriangleIndex,
                        nextSkipLambda = 0,

                        steps = steps,
                        intersectingEdges = intersectingEdges,
                    };

                    visitTriangles.Clear();
                    visitedTriangles.Clear();

                    do
                    {
                        intersectConstrainedEdge.isEnded = true;
                        RotateAroundVertex(
                            triangles,
                            adjacency,
                            visitTriangles,
                            visitedTriangles,
                            intersectConstrainedEdge.nextAroundTriangleIndex,                            
                            ref intersectConstrainedEdge,
                            intersectConstrainedEdge.nextSkipLambda);
                        steps = intersectConstrainedEdge.steps;
                    }
                    while (!intersectConstrainedEdge.isEnded);
                }

                /*if (foundParallel)
                {
                    //satisfiedEdges.Add(constrainedEdge);
                    continue;
                }*/
            }
        }

        if (0 < steps)
        {
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
    public static bool IsLinesIntersect(in Vector2 line0Begin, in Vector2 line0End, in Vector2 line1Begin, in Vector2 line1End)
    {
        Vector2 a1b1 = line0End - line0Begin;
        Vector2 b1b2 = line1End - line0End;
        Vector2 b1a2 = line1Begin - line0End;
        Vector2 a2b2 = line1End - line1Begin;
        Vector2 b2b1 = line0End - line1End;
        Vector2 b2a1 = line0Begin - line1End;
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

    public interface IRotateAroundVertex { public void Run(int triangleIndex, int vertexIndex); }
    public static void RotateAroundVertex<TLambda>(List<int> triangles, List<int> adjacency, Stack<int> visitTriangles, HashSet<int> visitedTriangles, int aroundTriangleIndex, ref TLambda lambda, int skipLambda)
        where TLambda : IRotateAroundVertex
    {
        //visitTriangles.Clear();
        visitTriangles.Push(aroundTriangleIndex / 3 * 3);

        //visitedTriangles.Clear();
        visitedTriangles.Add(aroundTriangleIndex / 3 * 3);

        int aroundVertexIndex = triangles[aroundTriangleIndex];

        while (visitTriangles.TryPeek(out int visitTriangle))
        {
            bool isAroundVertex = false;

            // execute lambda if triangle contains aroundVertexIndex
            for (int i = visitTriangle; i < visitTriangle + 3; i++)
            {
                int vertexIndex = triangles[i];
                if (vertexIndex == aroundVertexIndex)
                {
                    if (0 < skipLambda)
                    {
                        skipLambda--;
                    }
                    else
                    {
                        lambda.Run(i, vertexIndex);
                    }
                    isAroundVertex = true;
                    break;
                }
            }

            // allow lambda to control flow
            if (visitTriangles.Count == 0)
                return;
            visitTriangles.Pop();

            if (!isAroundVertex)
                continue;

            // visit all neighbour triangles
            for (int i = visitTriangle; i < visitTriangle + 3; i++)
            {
                int adjacentTriangle = adjacency[i];
                if (adjacentTriangle != -1 && visitedTriangles.Add(adjacentTriangle))
                {
                    visitTriangles.Push(adjacentTriangle);
                }
            }
        }
    }

    public struct IntersectConstrainedEdge : IRotateAroundVertex
    {
        public List<Vector2> vertices;
        public List<int> triangles;
        public List<int> adjacency;

        public Vector2Int constrainedEdgeIndices;
        public Vector2 constrainedVertex0;
        public Vector2 constrainedVertex1;
        public Vector2 constrainedEdge;

        public Stack<int> visitTriangles;
        public HashSet<int> visitedTriangles;

        public bool isEnded;
        public int nextAroundTriangleIndex;
        public int nextSkipLambda;

        public int steps;
        public List<Vector2Int> intersectingEdges;

        public void Run(int triangleIndex, int _)
        {
            if (steps <= 0)
                return;
            steps--;

            int triangleBaseIndex = triangleIndex / 3 * 3;
            Span<Vector2> triangleVertices = stackalloc Vector2[3];

            // get vertex positions
            for (int i = 0; i < 3; i++)
            {
                int vertexIndex = triangles[triangleBaseIndex + i];
                if (vertexIndex == constrainedEdgeIndices.y)
                {
                    isEnded = true;
                    visitTriangles.Clear();
                    return;
                }
                triangleVertices[i] = vertices[vertexIndex];
            }

            // find parallel vert furthest away from edge.x
            {
                int bestParallelI = -1;
                float bestParallelDistance = 0f;
                for (int i = 0; i < 3; i++)
                {
                    ref Vector2 triangleVertex = ref triangleVertices[i];
                    Vector2 constrainedV0ToVertex = triangleVertex - constrainedVertex0;

                    // is parallel and points in the same direction
                    if (Mathf.Abs(Cross(constrainedV0ToVertex, constrainedEdge)) < 1E-05 &&
                        0f < Vector2.Dot(constrainedV0ToVertex, constrainedEdge))
                    {
                        float sqrMagnitude = constrainedV0ToVertex.sqrMagnitude;
                        if (bestParallelDistance < sqrMagnitude)
                        {
                            bestParallelI = i;
                            bestParallelDistance = sqrMagnitude;
                        }
                    }
                }

                // if not visited
                // if parallel exists, rotate around bestParallelI, go to neighbours
                if (bestParallelI != -1 && visitedTriangles.Add(triangleBaseIndex + bestParallelI))
                {
                    // TODO need to stop checking equal vertexIndex for this triangleIndex!

                    nextAroundTriangleIndex = triangleBaseIndex + bestParallelI;
                    // lambda will be run on this exact triangleBaseIndex again, but we want the neighbours
                    nextSkipLambda = 1;
                    // continue outer loop around this best parallel vertex
                    isEnded = false;
                    visitTriangles.Clear();
                    return;
                }
            }

            // find find intersecting edges furthest away from edge.x
            {
                int bestIntersectI = -1;
                float bestIntersectDistance = 0f;
                for (int i = 0; i < 3; i++)
                {
                    ref Vector2 v0 = ref triangleVertices[i];
                    ref Vector2 v1 = ref triangleVertices[(i + 1) % 3];
                    Vector2 edge = v1 - v0;

                    if (IsLinesIntersect(v0, v1, constrainedVertex0, constrainedVertex1))
                    {
                        float intersectDistance = Mathf.Max((v0 - constrainedVertex0).sqrMagnitude, (v1 - constrainedVertex0).sqrMagnitude);
                        if (bestIntersectDistance < intersectDistance)
                        {
                            bestIntersectI = i;
                            bestIntersectDistance = intersectDistance;
                        }
                    }
                }
                                
                if (bestIntersectI != -1)
                {
                    // other i in intersect edge
                    int bestIntersectI1 = (bestIntersectI + 1) % 3;
                    // i not in edge
                    int awayI = (bestIntersectI + 2) % 3;

                    int adjacentTriangle = adjacency[triangleBaseIndex + awayI];
                    if (adjacentTriangle != -1)
                    {
                        // find new vertex index in adjacentTriangle
                        int intersectVertexIndex0 = triangles[triangleBaseIndex + bestIntersectI];
                        int intersectVertexIndex1 = triangles[triangleBaseIndex + bestIntersectI1];
                        int newTriangleIndex = -1;
                        for (int i = 0; i < 3; i++)
                        {
                            int vertexIndex = triangles[adjacentTriangle + i];
                            if (vertexIndex != intersectVertexIndex0 &&
                                vertexIndex != intersectVertexIndex1)
                            {
                                newTriangleIndex = adjacentTriangle + i;
                                break;
                            }
                        }

                        if (newTriangleIndex == -1)
                        {
                            // should never happen
                            throw new Exception();
                        }

                        // if not visited before
                        if (newTriangleIndex != -1 && visitedTriangles.Add(newTriangleIndex))
                        {
                            // ensure not to go backwards later
                            visitedTriangles.Add(triangleBaseIndex + awayI);

                            // TODO mark edge
                            intersectingEdges.Add(new Vector2Int(intersectVertexIndex0, intersectVertexIndex1));

                            // continue outer loop
                            isEnded = false;
                            nextAroundTriangleIndex = newTriangleIndex;
                            visitTriangles.Clear();
                            return;
                        }
                    }
                }
            }
        }
    }
}
