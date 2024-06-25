using System;
using System.Collections.Generic;

public enum IDType
{
    Invalid,
    Unit,
}

[Serializable]
public struct ID : IEquatable<ID>
{
    public IDType type;
    public int index;
    public int version;

    bool IEquatable<ID>.Equals(ID other)
    {
        return
            type == other.type &&
            index == other.index &&
            version == other.version;
    }

    public static bool operator ==(in ID a, in ID b)
    {
        return
            a.type == b.type &&
            a.index == b.index &&
            a.version == b.version;
    }

    public static bool operator !=(in ID a, in ID b)
    {
        return
            a.type != b.type ||
            a.index != b.index ||
            a.version != b.version;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(type, index, version);
    }

    public override bool Equals(object obj)
    {
        // avoid this at all costs, it boxes structs unnessarily
        // try to use IEquatable<ID>.Equals() as an alternative
        throw new NotImplementedException();
    }
}

[Serializable]
public struct VersionedPool<T> where T : struct
{
    // all have the same lengths
    public List<T> elements;
    public List<int> versions;
    public List<bool> isUsing;
    public IDType type;

    public ID Spawn(in T t)
    {
        int findUnusedIndex = isUsing.FindIndex(0, (isUsingX) => !isUsingX);
        if (0 <= findUnusedIndex)
        {
            elements[findUnusedIndex] = t;
            isUsing[findUnusedIndex] = true;
            return new ID
            {
                type = type,
                index = findUnusedIndex,
                version = versions[findUnusedIndex],
            };
        }
        else
        {
            elements.Add(t);
            versions.Add(0);
            isUsing.Add(true);
            return new ID
            {
                type = type,
                index = elements.Count - 1,
                version = 0,
            };
        }
    }

    public bool TryDespawn(in ID id, out T despawnedElement)
    {
        if (IsValidID(id))
        {
            despawnedElement = elements[id.index];
#if UNITY_EDITOR // validation
            elements[id.index] = default;
#endif
            versions[id.index]++;
            isUsing[id.index] = false;
            return true;
        }
        despawnedElement = default;
        return false;
    }

    public bool IsValidID(in ID id)
    {
        return id.type == type && id.index < elements.Count && id.index < versions.Count && versions[id.index] == id.version;
    }

    // enumerator over using indices
    public VersionedPoolUsingEnumerator<T> GetEnumerator()
    {
        return new VersionedPoolUsingEnumerator<T>(this);
    }

    public ID GetCurrentID(int index)
    {
        return new ID
        {
            type = type,
            index = index,
            version = versions[index]
        };
    }

    public int CountUsing()
    {
        int count = 0;
        foreach (var x in isUsing)
        {
            if (x)
                count++;
        }
        return count;
    }

    public void SpawnRange(List<T> range)
    {
        foreach (var x in range)
            Spawn(x);
    }

    public void SpawnRange<RangeType>(List<RangeType> range, Func<RangeType, ID, T> convert)
    {
        foreach (var x in range)
        {
            ID id = Spawn(new T());
            elements[id.index] = convert(x, id);
        }
    }

    public void Clear()
    {
        elements.Clear();
        versions.Clear();
        isUsing.Clear();
    }

    public bool Validate()
    {
        return elements.Count == versions.Count && elements.Count == isUsing.Count;
    }
}

public struct VersionedPoolUsingEnumerator<T> where T : struct
{
    public int index;
    public VersionedPool<T> pool;

    public VersionedPoolUsingEnumerator(in VersionedPool<T> pool)
    {
        index = -1;
        this.pool = pool;
    }

    public int Current
    {
        get
        {
            return index;
        }
    }

    public bool MoveNext()
    {
        while (true)
        {
            index++;

            if (pool.isUsing.Count <= index)
                return false;

            if (pool.isUsing[index])
                return true;
        }
    }

    public void Reset()
    {
        index = -1;
    }
}