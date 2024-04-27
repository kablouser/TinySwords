using System.Collections.Generic;
using UnityEngine;
using System;
using static UnityEngine.RuleTile.TilingRuleOutput;

public static class Pathfind
{
    public static bool FindAStar(
        in OverlapGrid overlapGrid, in Bounds2D agentBounds, in Vector2 start, in Vector2 end,
        //output
        List<Vector2Int> path,
        //scratch pads
        Dictionary<Vector2Int, Vector2Int> visitedFrom,
        List<ScoreAStar> scores,

        // TODO REMOVE
        int steps)
    {
        path.Clear();

        Vector2 elementSize = overlapGrid.GetElementSize();
        Vector2Int startIndex = overlapGrid.GetIndex(elementSize, start);
        Vector2Int endIndex = overlapGrid.GetIndex(elementSize, end);

        if (!(overlapGrid.overlaps.InRange(startIndex) && overlapGrid.overlaps.InRange(endIndex)))
            return false;
        if (startIndex == endIndex)
        {
            path.Add(endIndex);
            return true;
        }

        overlapGrid.RemoveBounds(agentBounds);

/*        {
            Vector2Int copyEndIndex = endIndex;
            if (!FindNearestEmpty(overlapGrid, copyEndIndex, out endIndex))
            {
                overlapGrid.AddBounds(agentBounds);
                return false;
            }
        }
        if (startIndex == endIndex)
        {
            overlapGrid.AddBounds(agentBounds);
            path.Add(endIndex);
            return true;
        }*/

        visitedFrom.Clear();
        scores.Clear();

        Vector2Int currentIndex = startIndex;
        float currentDistanceTravelled = 0;
        Span<Vector2Int> neighbours = stackalloc Vector2Int[8];

        Vector2Int nearestDistanceToEndIndex = currentIndex;
        float nearestDistanceToEnd = (currentIndex - endIndex).magnitude;

        visitedFrom.Add(currentIndex, currentIndex);

        int maxIts = overlapGrid.overlaps.dimension0 * overlapGrid.overlaps.dimension1;

        do
        {
            OverlapGrid.GetNeighbours(currentIndex, neighbours);
            foreach (Vector2Int neighbour in neighbours)
            {
                if (overlapGrid.overlaps.TryIndex(neighbour, out int overlaps) &&
                    0 == overlaps)
                {                    
                    if (1 < (neighbour - currentIndex).sqrMagnitude)
                    {
                        // deal with diagonal collision
                        if (!(overlapGrid.overlaps.TryIndex(new Vector2Int(currentIndex.x, neighbour.y), out int diagonal0Overlaps) &&
                            0 == diagonal0Overlaps &&
                            overlapGrid.overlaps.TryIndex(new Vector2Int(neighbour.x, currentIndex.y), out int diagonal1Overlaps) &&
                            0 == diagonal1Overlaps))
                            continue;
                    }

                    if (visitedFrom.TryAdd(neighbour, currentIndex))
                    {
                        float newDistanceTravelled = currentDistanceTravelled + (neighbour - currentIndex).magnitude;
                        float newDistanceToEnd = (neighbour - endIndex).magnitude;

                        ScoreAStar score = new ScoreAStar
                        {
                            score = newDistanceToEnd + newDistanceTravelled,
                            index = neighbour,
                            distanceTravelled = newDistanceTravelled,
                            distanceToEnd = newDistanceToEnd
                        };

                        int scoreIndex = scores.BinarySearch(score);
                        if (scoreIndex <= 0)
                        {
                            scores.Insert(~scoreIndex, score);
                        }
                    }
                }
            }

            
            if (0 == scores.Count)
                break;
            else
            {
                // next currentIndex
                {
                    Span<ScoreAStar> scoresSpan = scores.AsSpan();
                    ref ScoreAStar nextBestScore = ref scoresSpan[scores.Count - 1];
                    currentIndex = nextBestScore.index;

                    if (currentIndex == endIndex)
                        break;

                    if (nextBestScore.distanceToEnd < nearestDistanceToEnd)
                    {
                        nearestDistanceToEndIndex = currentIndex;
                        nearestDistanceToEnd = nextBestScore.distanceToEnd;
                    }

                    currentDistanceTravelled = nextBestScore.distanceTravelled;
                }
                scores.RemoveAt(scores.Count - 1);
                maxIts--;
            }            
        }
        while (0 < maxIts);

        overlapGrid.AddBounds(agentBounds);

        if (currentIndex != endIndex)
        {
            currentIndex = nearestDistanceToEndIndex;
        }

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

    public struct ScoreAStar : IComparable<ScoreAStar>
    {
        public float score;
        public Vector2Int index;
        public float distanceTravelled;
        public float distanceToEnd;

        public int CompareTo(ScoreAStar other)
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

    public static bool FindNearestEmpty(in OverlapGrid overlapGrid, in Vector2Int index, out Vector2Int nearestEmpty)
    {
        if (!overlapGrid.overlaps.InRange(index))
        {
            nearestEmpty = default;
            return false;
        }

        if (overlapGrid.overlaps[index] == 0)
        {
            nearestEmpty = index;
            return true;
        }

        bool isBounded = false;
        for (int i = 1; !isBounded; i++)
        {
            isBounded = true;

            if (index.x + i < overlapGrid.overlaps.dimension0)
            {
                isBounded = false;

                int yMax = Mathf.Min(index.y + i, overlapGrid.overlaps.dimension1);
                for (int y = Mathf.Max(index.y - i + 1, 0); y < yMax; y++)
                {
                    nearestEmpty = new Vector2Int(index.x + i, y);
                    if (overlapGrid.overlaps[nearestEmpty] == 0)
                    {
                        return true;
                    }
                }
            }

            if (0 <= index.x - i)
            {
                isBounded = false;

                int yMax = Mathf.Min(index.y + i, overlapGrid.overlaps.dimension1);
                for (int y = Mathf.Max(index.y - i + 1, 0); y < yMax; y++)
                {
                    nearestEmpty = new Vector2Int(index.x - i, y);
                    if (overlapGrid.overlaps[nearestEmpty] == 0)
                    {
                        return true;
                    }
                }
            }

            if (index.y + i < overlapGrid.overlaps.dimension1)
            {
                isBounded = false;

                int xMax = Mathf.Min(index.x + i + 1, overlapGrid.overlaps.dimension0);
                for (int x = Mathf.Max(index.x - i, 0); x < xMax; x++)
                {
                    nearestEmpty = new Vector2Int(x, index.y + i);
                    if (overlapGrid.overlaps[nearestEmpty] == 0)
                    {
                        return true;
                    }
                }
            }

            if (0 <= index.y - i)
            {
                isBounded = false;

                int xMax = Mathf.Min(index.x + i + 1, overlapGrid.overlaps.dimension0);
                for (int x = Mathf.Max(index.x - i, 0); x < xMax; x++)
                {
                    nearestEmpty = new Vector2Int(x, index.y - i);
                    if (overlapGrid.overlaps[nearestEmpty] == 0)
                    {
                        return true;
                    }
                }
            }
        }

        nearestEmpty = default;
        return false;
    }
}
