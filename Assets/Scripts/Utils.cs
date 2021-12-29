using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static void Shuffle<T>(ref T[] objs)
    {
        for (int i = 0; i < objs.Length; i++)
        {
            int rand = UnityEngine.Random.Range(0, objs.Length);
            T temp = objs[i];
            objs[i] = objs[rand];
            objs[rand] = temp;
        }
    }

    public static GameObject GetNearestObject(Vector3 source, GameObject[] objs)
    {
        if (objs.Length == 0) return null;
        float minDist = float.MaxValue;
        int index = -1;
        for (int i = 0; i < objs.Length; i++)
        {
            float dist = (source - objs[i].transform.position).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                index = i;
            }
        }
        return objs[index];
        
    }

    public static GameObject GetNearestObjectIf(Vector3 source, GameObject[] objs, Predicate<GameObject> predicate)
    {
        return Utils.GetNearestObject(source, new List<GameObject>(objs).FindAll(predicate).ToArray());
    }
}
