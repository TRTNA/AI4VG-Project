using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    public static GameObject GetNearestObject(Vector3 source, GameObject[] objs)
    {
        if (objs.Length == 0) return null;
        float minDist = float.MaxValue;
        int index = -1;
        for (int i = 0; i < objs.Length; i++)
        {
            if (objs[i] == null) continue;
            float dist = (source - objs[i].transform.position).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                index = i;
            }
        }
        return objs[index];
        
    }

    public static GameObject[] SortByDistance(Vector3 source, GameObject[] objs)
    {
        List<GameObject> temp = new List<GameObject>(objs);
        temp.Sort((go1, go2) =>
        {
            if (go1 == null || go2 == null) return 0;
            var d1 = (source - go1.transform.position).sqrMagnitude;
            var d2 = (source - go2.transform.position).sqrMagnitude;
            if (d1.Equals(d2)) return 0;
            return d1 > d2 ? 1 : -1;
        });
        return temp.ToArray();
    }
}
