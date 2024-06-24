using System.Collections.Generic;
using UnityEngine;
using System;

public static class Pathfind
{
    public static bool FindAStar(
        in NavigationGrid navigationGrid,
        in float maxRealMomentum, in Vector2 start, in Vector2 end,
        //output
        List<Vector2Int> path,
        //scratch pads
        Dictionary<Vector2Int, Vector2Int> visitedFrom,
        List<ScoreAStar> scores)
    {
        path.Clear();

        Vector2 elementSize = navigationGrid.GetElementSize();
        Vector2Int startIndex = navigationGrid.GetIndex(elementSize, start);
        Vector2Int endIndex = navigationGrid.GetIndex(elementSize, end);
        endIndex.x = Mathf.Clamp(endIndex.x, 0, navigationGrid.nodes.dimension0);
        endIndex.y = Mathf.Clamp(endIndex.y, 0, navigationGrid.nodes.dimension1);

        if (!navigationGrid.nodes.InRange(startIndex))
            return false;
        if (startIndex == endIndex)
        {
            path.Add(endIndex);
            return true;
        }

        //navigationGrid.AddBounds(agentBounds, -currentNavigationNode, elementSize);

        visitedFrom.Clear();
        scores.Clear();

        Vector2Int currentIndex = startIndex;
        float currentDistanceTravelled = 0;
        Span<Vector2Int> neighbours = stackalloc Vector2Int[8];

        Vector2Int nearestDistanceToEndIndex = currentIndex;
        float nearestDistanceToEnd = (currentIndex - endIndex).magnitude;

        visitedFrom.Add(currentIndex, currentIndex);

        int maxIts;
        {
            maxIts =
                navigationGrid.nodes.TryIndex(endIndex, out NavigationNode endNavigationNode) &&
                endNavigationNode.blocking == 0 ?
                    200 : // end is unblocked, there could be a path
                    100; // end is blocked, there's no path, find somewhere nearby
            maxIts = Mathf.Min(maxIts, navigationGrid.nodes.dimension0 * navigationGrid.nodes.dimension1);
        }

        int maxMomentumScaled = Mathf.Max(1, Mathf.RoundToInt(maxRealMomentum * NavigationNode.MOMENTUM_SCALE));
        float movableThresholdMomentumScaled = maxRealMomentum * NavigationNode.MOMENTUM_SCALE * 0.9f;

        do
        {
            NavigationGrid.GetNeighbours(currentIndex, neighbours);
            foreach (Vector2Int neighbour in neighbours)
            {
                Vector2Int currentToNeighbour = neighbour - currentIndex;
                Vector2 currentToNeighbourFloat = currentToNeighbour;
                float currentToNeighbourLength = currentToNeighbour.magnitude;

                NavigationNode theoreticalMomentumInNeighbour = new NavigationNode
                {
                    scaledMomentum = maxMomentumScaled * currentToNeighbour,
                    blocking = 0,
                };

                if (navigationGrid.nodes.TryIndex(neighbour, out NavigationNode neighbourMomentum) &&
                    movableThresholdMomentumScaled * currentToNeighbourLength <= currentToNeighbour.Dot(neighbourMomentum.CombineScaledMomentum(theoreticalMomentumInNeighbour)))
                {
                    if (1.1f < currentToNeighbourLength)
                    {
                        // deal with diagonal collision
                        if (!(navigationGrid.nodes.TryIndex(new Vector2Int(currentIndex.x, neighbour.y), out neighbourMomentum) &&
                            0 < currentToNeighbour.Dot(neighbourMomentum.CombineScaledMomentum(theoreticalMomentumInNeighbour)) &&
                            navigationGrid.nodes.TryIndex(new Vector2Int(neighbour.x, currentIndex.y), out neighbourMomentum) &&
                            0 < currentToNeighbour.Dot(neighbourMomentum.CombineScaledMomentum(theoreticalMomentumInNeighbour))))
                            continue;
                    }

                    if (visitedFrom.TryAdd(neighbour, currentIndex))
                    {
                        float newDistanceTravelled = currentDistanceTravelled + currentToNeighbourLength;
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

                scores.RemoveAt(scores.Count - 1);
                maxIts--;
            }
        }
        while (0 < maxIts);

        //navigationGrid.AddBounds(agentBounds, currentNavigationNode, elementSize);

        if (currentIndex != endIndex)
        {
            path.Add(nearestDistanceToEndIndex);
            currentIndex = visitedFrom[nearestDistanceToEndIndex];
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
            // descending order
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
    /*
    public static bool FindFormation(
        in NavigationGrid navigationGrid,
        in Vector2 formationSuggestCenter, int formationCount,
        //output
        List<Vector2> outFormationPositions,
        //scratchpad
        List<(Vector2Int, float)> scores,
        HashSet<Vector2Int> nodesVisited)
    {
        outFormationPositions.Clear();
        scores.Clear();
        nodesVisited.Clear();

        if (formationCount == 0)
            return true;

        Vector2 elementSize = navigationGrid.GetElementSize();
        Vector2Int formationSuggestIndex = navigationGrid.GetIndex(elementSize, formationSuggestCenter);
        formationSuggestIndex.x = Mathf.Clamp(formationSuggestIndex.x, 0, navigationGrid.nodes.dimension0);
        formationSuggestIndex.y = Mathf.Clamp(formationSuggestIndex.y, 0, navigationGrid.nodes.dimension1);

        Span<Vector2Int> neighbours = stackalloc Vector2Int[8];

        // find nearest open node to formation index
        Vector2Int formationOriginIndex = formationSuggestIndex;
        nodesVisited.Add(formationOriginIndex);
        while (
            navigationGrid.nodes.TryIndex(formationOriginIndex, out NavigationNode navigationNode) &&
            0 < navigationNode.blocking)
        {
            NavigationGrid.GetNeighbours(formationOriginIndex, neighbours);
            foreach(ref Vector2Int neighbour in neighbours)
            {
                if (nodesVisited.Add(neighbour))
                {
                    (Vector2Int, float) score = (neighbour, (formationOriginIndex - neighbour).magnitude);

                    int scoreIndex = scores.BinarySearch(score, new IndexScoreDescending());
                    if (scoreIndex <= 0)
                    {
                        scores.Insert(~scoreIndex, score);
                    }
                }
            }

            if (0 == scores.Count)
                return false;
            else
            {
                Span<(Vector2Int, float)> scoresSpan = scores.AsSpan();
                ref (Vector2Int, float) bestScore = ref scoresSpan[scores.Count - 1];

                formationOriginIndex = bestScore.Item1;
                nodesVisited.Add(formationOriginIndex);
            }
        }

        // dont clear nodesVisited, all elements are guaranteed to be blocking or findOpenIndex itself

        // find formation positions
        Vector2Int nextFormationIndex = formationOriginIndex;
        outFormationPositions.Add(nextFormationIndex);
        formationCount--;
        while (
            0 < formationCount &&
            navigationGrid.nodes.TryIndex(formationOriginIndex, out NavigationNode navigationNode) &&
            navigationNode.blocking == 0)
        {
            NavigationGrid.GetNeighbours(formationOriginIndex, neighbours);
            foreach (ref Vector2Int neighbour in neighbours)
            {
                if (nodesVisited.Add(neighbour))
                {
                    ManhattanScore score = new ManhattanScore(neighbour, formationOriginIndex);

                    int scoreIndex = scores.BinarySearch(score);
                    if (scoreIndex <= 0)
                    {
                        scores.Insert(~scoreIndex, score);
                    }
                }
            }

            if (0 == scores.Count)
                return false;
            else
            {
                Span<ManhattanScore> scoresSpan = scores.AsSpan();
                ref ManhattanScore bestScore = ref scoresSpan[scores.Count - 1];

                formationOriginIndex = bestScore.index;
                nodesVisited.Add(formationOriginIndex);
            }
        }
        return true;
    }

    private struct IndexScoreDescending : IComparer<(Vector2Int, float)>
    {
        public int Compare((Vector2Int, float) lhs, (Vector2Int, float) rhs)
        {
            // duplicates allowed
            // descending order
            if (lhs.Item2 < rhs.Item2)
                return 1;
            else if (rhs.Item2 < lhs.Item2)
                return -1;

            // index affects ordering
            if (lhs.Item1.x < rhs.Item1.x)
                return 1;
            else if (rhs.Item1.x < lhs.Item1.x)
                return -1;

            if (lhs.Item1.y < rhs.Item1.y)
                return 1;
            else
                return -1;
        }
    }

    private struct SearchFormationNode : IComparable<SearchFormationNode>
    {
        public int CompareTo(SearchFormationNode other)
        {
            throw new NotImplementedException();
        }
    }*/
}
