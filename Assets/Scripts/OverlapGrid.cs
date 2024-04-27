using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public struct OverlapGrid
{
    // number of overlapping colliders in each grid spot
    public Array2D<int> overlaps;
    public Bounds2D bounds;

    public void Snapshot()
    {
        overlaps = new Array2D<int>(overlaps.dimension0, overlaps.dimension1);
        Collider2D[] overlappedColliders = Physics2D.OverlapAreaAll(bounds.min, bounds.max);
        Span<Bounds2D> startBounds = stackalloc Bounds2D[overlappedColliders.Length];
        for (int i = 0; i < overlappedColliders.Length; ++i)
        {
            startBounds[i] = new Bounds2D(overlappedColliders[i].bounds);
        }
        AddBounds(startBounds);
    }

    public void AddBounds(in Bounds2D addBound)
    {
        Span<Bounds2D> addBounds = stackalloc Bounds2D[1] { addBound };
        Rasterise(addBounds, new RasteriseAddBoundsLambda());
    }

    public void RemoveBounds(in Bounds2D removeBound)
    {
        Span<Bounds2D> removeBounds = stackalloc Bounds2D[1] { removeBound };
        Rasterise(removeBounds, new RasteriseRemoveBoundsLambda());
    }

    public void AddBounds(in Span<Bounds2D> addBounds)
    {
        Rasterise(addBounds, new RasteriseAddBoundsLambda());
    }

    public void RemoveBounds(in Span<Bounds2D> removeBounds)
    {
        Rasterise(removeBounds, new RasteriseRemoveBoundsLambda());
    }

    public interface IRasteriseLambda
    {
        public void Execute(ref int overlapCount);
    }
    public void Rasterise<TLabmda>(in Span<Bounds2D> inBounds, in TLabmda lambda) where TLabmda : struct, IRasteriseLambda
    {
        Vector2 elementSize = GetElementSize();

        foreach (ref Bounds2D elementBounds in inBounds)
        {
            GetBoundsIndex(elementSize, elementBounds, out Vector2Int min, out Vector2Int max);
            min = Vector2Int.Max(min, new Vector2Int(0, 0));
            max = Vector2Int.Min(max, new Vector2Int(overlaps.dimension0, overlaps.dimension1));

            for (int x = min.x; x < max.x; x++)
            {
                for (int y = min.y; y < max.y; y++)
                {
                    lambda.Execute(ref overlaps[x, y]);
                }
            }
        }
    }

    public struct RasteriseAddBoundsLambda : IRasteriseLambda
    {
        public void Execute(ref int overlapCount)
        {
            overlapCount++;
        }
    }

    public struct RasteriseRemoveBoundsLambda : IRasteriseLambda
    {
        public void Execute(ref int overlapCount)
        {
            overlapCount--;
        }
    }

    public Vector2 GetElementSize()
    {
        Vector2 boundsSize = bounds.size;
        return new Vector2(boundsSize.x / overlaps.dimension0, boundsSize.y / overlaps.dimension1);
    }

    public void GetBoundsIndex(
        in Vector2 elementSize, in Bounds2D elementBounds,
        out Vector2Int min, out Vector2Int max)
    {
        min = new Vector2Int(
            Mathf.FloorToInt((elementBounds.min.x - bounds.min.x) / elementSize.x),
            Mathf.FloorToInt((elementBounds.min.y - bounds.min.y) / elementSize.y));

        max = new Vector2Int(
            Mathf.CeilToInt((elementBounds.max.x - bounds.min.x) / elementSize.x),
            Mathf.CeilToInt((elementBounds.max.y - bounds.min.y) / elementSize.y));
    }

    public Vector2Int GetIndex(in Vector2 elementSize, in Vector2 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt((position.x - bounds.min.x) / elementSize.x),
            Mathf.FloorToInt((position.y - bounds.min.y) / elementSize.y));
    }

    public Vector3 GetElementWorldPosition(in Vector2 boundsSize, in Vector2 halfElementSize, in Vector2Int index)
    {
        return new Vector3(
            index.x / (float)overlaps.dimension0 * boundsSize.x + bounds.min.x + halfElementSize.x,
            index.y / (float)overlaps.dimension1 * boundsSize.y + bounds.min.y + halfElementSize.y,
            0f);
    }

    // includes diagonals
    public static void GetNeighbours(in Vector2Int index, Span<Vector2Int> neighbours)
    {
        neighbours[0] = index + new Vector2Int(1, 0);
        neighbours[1] = index + new Vector2Int(1, 1);
        neighbours[2] = index + new Vector2Int(0, 1);
        neighbours[3] = index + new Vector2Int(-1, 1);
        neighbours[4] = index + new Vector2Int(-1, 0);
        neighbours[5] = index + new Vector2Int(-1, -1);
        neighbours[6] = index + new Vector2Int(0, -1);
        neighbours[7] = index + new Vector2Int(1, -1);
    }

#if UNITY_EDITOR
    public void OnSceneGUI()
    {
        // draw grid
        if (0 < overlaps.dimension0 * overlaps.dimension1)
        {
            Vector3[] lineSegs = new Vector3[(overlaps.dimension0 + 1 + overlaps.dimension1 + 1) * 2];

            Vector2 elementSize = GetElementSize();
            Vector2 min = bounds.min;
            Vector2 max = bounds.max;

            int lineSegsI = 0;
            for (int i = 0; i <= overlaps.dimension0; i++)
            {
                lineSegs[lineSegsI] = new Vector3(min.x + i * elementSize.x, min.y);
                lineSegs[lineSegsI + 1] = new Vector3(min.x + i * elementSize.x, max.y);
                lineSegsI += 2;
            }
            for (int i = 0; i <= overlaps.dimension1; i++)
            {
                lineSegs[lineSegsI] = new Vector3(min.x, min.y + i * elementSize.y);
                lineSegs[lineSegsI + 1] = new Vector3(max.x, min.y + i * elementSize.y);
                lineSegsI += 2;
            }
            Handles.DrawLines(lineSegs);
        }

        // draw overlap nums
/*        if (overlaps.dimension0 * overlaps.dimension1 == overlaps.elements.Length)
        {
            Vector2 boundsSize = bounds.size;
            Vector2 halfElementSize = GetElementSize() * 0.5f;

            for (int x = 0; x < overlaps.dimension0; x++)
            {
                for (int y = 0; y < overlaps.dimension1; y++)
                {
                    Handles.Label(
                        GetElementWorldPosition(boundsSize, halfElementSize, new Vector2Int(x, y)),
                        $"{overlaps[x, y]}");
                }
            }
        }*/
    }
#endif
}
