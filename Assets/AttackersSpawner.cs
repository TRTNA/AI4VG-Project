using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackersSpawner : MonoBehaviour
{

    public Transform toSpawn;
    [Range(1, 100)]
    public int max = 10;
    public int spawnCooldown = 2;
    private bool canSpawn = true;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Input.GetMouseButton(0) && canSpawn && max > 0)
        {
            if (Physics.Raycast(ray, out hit, 1000))
            {
                Debug.Log("Hit!");
                canSpawn = false;
                Instantiate(toSpawn, hit.point, Quaternion.identity);
                StartCoroutine(SpawnCooldown());
                max--;
            }
        }
        
    }

    IEnumerator SpawnCooldown()
    {
        Debug.Log("Spawn is in cooldown");
        yield return new WaitForSeconds(spawnCooldown);
        Debug.Log("Cooldown end");
        canSpawn = true;
    }

}
