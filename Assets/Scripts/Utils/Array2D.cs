using UnityEngine;
using System;
using System.Reflection;

[Serializable]
public struct Array2D<T>
{
    public T[] elements;
    public int dimension0;
    public int dimension1;

    public Array2D(int dimension0, int dimension1)
    {
        this.dimension0 = dimension0;
        this.dimension1 = dimension1;
        elements = new T[dimension0 * dimension1];
    }

    public ref T this[int index0, int index1]
    {
        get
        {
#if UNITY_EDITOR
            if (!(InRange(index0, index1)))
            {
                Debug.LogAssertionFormat($"Array2D: indexes are out of range! 0<={index0}<{dimension0}, 0<={index1}<{dimension1}");
            }
#endif
            return ref elements[index0 + index1 * dimension0];
        }      
    }

    public ref T this[in Vector2Int index]
    {
        get
        {
#if UNITY_EDITOR
            if (!(InRange(index.x, index.y)))
            {
                Debug.LogAssertionFormat($"Array2D: indexes are out of range! 0<={index.x}<{dimension0}, 0<={index.y}<{dimension1}");
            }
#endif
            return ref elements[index.x + index.y * dimension0];
        }
    }

    public bool InRange(int index0, int index1)
    {
        return 0 <= index0 && index0 < dimension0 && 0 <= index1 && index1 < dimension1;
    }

    public bool InRange(in Vector2Int index)
    {
        return 0 <= index.x && index.x < dimension0 && 0 <= index.y && index.y < dimension1;
    }

    public bool TryIndex(in Vector2Int index, out T t)
    {
        if (InRange(index))
        {
            t = elements[index.x + index.y * dimension0];
            return true;
        }
        t = default;
        return false;
    }
}
