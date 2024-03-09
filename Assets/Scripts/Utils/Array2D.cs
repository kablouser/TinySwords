using UnityEngine;
using System;

[Serializable]
public struct Array2D<T>
{
    public T[] elements;
    public int dimension0;
#if UNITY_EDITOR
    public int dimension1;
#endif

    public Array2D(int dimension0, int dimension1)
    {
        this.dimension0 = dimension0;
#if UNITY_EDITOR
        this.dimension1 = dimension1;
#endif
        elements = new T[dimension0 * dimension1];
    }

    public ref T this[int index0, int index1]
    {
        get
        {
#if UNITY_EDITOR
            if (!(0 <= index0 && index0 < dimension0 && 0 <= index1 && index1 < dimension1))
            {
                Debug.LogAssertionFormat($"Array2D: indexes are out of range! 0<={index0}<{dimension0}, 0<={index1}<{dimension1}");
            }
#endif
            return ref elements[index0 + index1 * dimension0];
        }      
    }
}
