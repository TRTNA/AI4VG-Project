using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public int secondsToSelfDestruction = 3;
    
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(SelfDestruction());
    }

    IEnumerator SelfDestruction()
    {
        yield return new WaitForSeconds(secondsToSelfDestruction);
        Destroy(gameObject);
    }
}
