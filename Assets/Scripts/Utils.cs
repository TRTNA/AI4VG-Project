using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static void Shuffle<T>(ref T[] objs)
    {
        for (int i = 0; i < objs.Length; i++)
        {
            int rand = Random.Range(0, objs.Length);
            T temp = objs[i];
            objs[i] = objs[rand];
            objs[rand] = temp;
        }
    }

    public static GameObject GetNearestObject(Vector3 source, GameObject[] objs)
    {
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
}
