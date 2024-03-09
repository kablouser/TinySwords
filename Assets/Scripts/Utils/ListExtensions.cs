using System.Collections.Generic;

public static class ListExtensions
{
    public static void Reserve<T>(this List<T> list, int capacity)
    {
        if (list.Count < capacity)
        {
            list.Capacity = capacity;
        }
    }
}
