using System.Collections.Generic;
using UnityEngine;
using System;

public static class GreedyPathfind
{
    public static bool FindGreedy(
        in OverlapGrid overlapGrid, in Bounds2D agentBounds, in Vector2 start, in Vector2 end,
        //output
        List<Vector2Int> path,
        //scratch pads
        Dictionary<Vector2Int, Vector2Int> visitedFrom,
        List<Score> scores)
    {
        path.Clear();

        Vector2 elementSize = overlapGrid.GetElementSize();
        Vector2Int startIndex = overlapGrid.GetIndex(elementSize, start);
        Vector2Int endIndex = overlapGrid.GetIndex(elementSize, end);

        if (!(overlapGrid.overlaps.InRange(startIndex) && overlapGrid.overlaps.InRange(endIndex)))
            return false;
        if (startIndex == endIndex)
            return true;

        {
            Span<Bounds2D> removeBounds = stackalloc Bounds2D[1] { agentBounds };
            //overlapGrid.RemoveBounds(removeBounds);
        }

        visitedFrom.Clear();
        scores.Clear();

        Vector2Int currentIndex = startIndex;
        float currentDistanceTravelled = 0;
        Span<Vector2Int> neighbours = stackalloc Vector2Int[8];

        int maxIts = overlapGrid.overlaps.dimension0 * overlapGrid.overlaps.dimension1;

        do
        {
            OverlapGrid.GetNeighbours(currentIndex, neighbours);
            foreach (Vector2Int neighbour in neighbours)
            {
                if (overlapGrid.overlaps.TryIndex(neighbour, out int overlaps) &&
                    0 == overlaps &&
                    visitedFrom.TryAdd(neighbour, currentIndex))
                {
                    float newDistanceTravelled = currentDistanceTravelled + (neighbour - currentIndex).magnitude;

                    Score score = new Score
                    {
                        score = (neighbour - endIndex).magnitude + newDistanceTravelled,
                        index = neighbour,
                        distanceTravelledSqr = newDistanceTravelled
                    };

                    int scoreIndex = scores.BinarySearch(score);
                    if (scoreIndex <= 0)
                    {
                        scores.Insert(~scoreIndex, score);
                    }                    
                }
            }

            
            if (0 == scores.Count)
                break;
            else
            {
                // next currentIndex
                currentIndex = scores[scores.Count - 1].index;

                if (currentIndex == endIndex)
                    break;

                currentDistanceTravelled = scores[scores.Count - 1].distanceTravelledSqr;
                scores.RemoveAt(scores.Count - 1);
                maxIts--;
            }            
        }
        while (0 < maxIts);

        {
            Span<Bounds2D> addBounds = stackalloc Bounds2D[1] { agentBounds };
            //overlapGrid.AddBounds(addBounds);
        }

        if (currentIndex == endIndex)
        {
            // construct path
            while (currentIndex != startIndex)
            {
                path.Add(currentIndex);
                currentIndex = visitedFrom[currentIndex];
            }
            //path.Add(startIndex);
            path.Reverse();

            return true;
        }

        return false;
    }

    public struct Score : IComparable<Score>
    {
        public float score;
        public Vector2Int index;
        public float distanceTravelledSqr;

        public int CompareTo(Score other)
        {
            // duplicates allowed
            // ascending order
            if (score < other.score)
                return 1;
            else if (other.score < score)
                return -1;

            // index affects ordering
            if (index.x < other.index.x)
                return 1;
            else if (other.index.x < index.x)
                return -1;

            if (index.y < other.index.y)
                return 1;
            else
                return -1;
        }
    }
}
