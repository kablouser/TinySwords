using UnityEngine;
using System.Collections.Generic;

public struct Pool
{
    public static GameObject SpawnOut(List<GameObject> pool, GameObject maybePrefab)
    {
        if (0 < pool.Count)
        {
            GameObject spawned = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            spawned.SetActive(true);
            return spawned;
        }
        else
            return Object.Instantiate(maybePrefab);
    }

    /*    public static void SpawnMany(List<GameObject> pool, GameObject maybePrefab, uint n, List<GameObject> outGameObjects)
        {
            if ((long)n - 1 < pool.Count)
            {
                GameObject spawned = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                spawned.SetActive(true);
                return spawned;
            }
            else
                return Object.Instantiate(maybePrefab);
        }*/

    public static void DespawnInto(List<GameObject> pool, in GameObject gameObject)
    {
        pool.Add(gameObject);
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public static void Prepopulate(List<GameObject> pool, GameObject prefab, int n)
    {
        foreach (var gameObject in pool)
        {
            Object.DestroyImmediate(gameObject);
        }
        pool.Clear();

        for (int i = 0; i < n; i++)
        {
            pool.Add(Object.Instantiate(prefab));
        }

        foreach (var gameObject in pool)
        {
            gameObject.SetActive(false);
        }
    }
#endif
}
