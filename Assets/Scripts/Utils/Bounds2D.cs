using System.Runtime.CompilerServices;
using UnityEngine;

[System.Serializable]
public struct Bounds2D
{
    public Vector2 min;
    public Vector2 max;

    public Vector2 center
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return (min + max) / 2f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            Vector2 offset = value - (min + max) / 2f;
            min += offset;
            max += offset;
        }
    }

    public Vector2 extents
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return (max - min) / 2f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            Vector2 currentCenter = center;
            min = currentCenter - value;
            max = currentCenter + value;
        }
    }

    //
    // Summary:
    //     The total size of the box. This is always twice as large as the extents.
    public Vector2 size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return max - min;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            Vector2 currentCenter = center;
            min = currentCenter - value / 2f;
            max = currentCenter + value / 2f;
        }
    }

    //
    // Summary:
    //     Creates a new Bounds.
    //
    // Parameters:
    //   center:
    //     The location of the origin of the Bounds.
    //
    //   size:
    //     The dimensions of the Bounds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bounds2D(in Bounds bounds3D)
    {
        Vector2 newCenter = bounds3D.center;
        Vector2 newExtends = bounds3D.extents;
        min = newCenter - newExtends;
        max = newCenter + newExtends;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bounds2D NaN()
    {
        return new Bounds2D
        {
            min = new Vector2(float.NaN, float.NaN),
            max = new Vector2(float.NaN, float.NaN),
        };
    }

    //
    // Summary:
    //     Grows the Bounds to include the point.
    //
    // Parameters:
    //   point:
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Encapsulate(Vector2 point)
    {
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }
}
