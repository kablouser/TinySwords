using System.Collections.Generic;
using UnityEngine;

public static class Extensions
{
    public static void Reserve<T>(this List<T> list, int capacity)
    {
        if (list.Count < capacity)
        {
            list.Capacity = capacity;
        }
    }

    public static int Dot(this Vector2Int lhs, in Vector2Int rhs)
    {
        return lhs.x * rhs.x + lhs.y * rhs.y;
    }
}
