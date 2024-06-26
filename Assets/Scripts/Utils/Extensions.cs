using System;
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

    public static bool Approximately(float a, float b, float slack)
    {
        return Mathf.Abs(b - a) < slack;
    }

    public static void ReserveArrayClear<T>(ref T[] array, int capacity)
    {
        if (array.Length <= capacity)
        {
            array = new T[capacity << 1];
        }
        else
        {
            Array.Clear(array, 0, capacity);
        }
    }
}
