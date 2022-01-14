using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject toSpawn;
    [Range(1, 100)]
    public int unitsAvailable = 10;
    [Range(0.1f, 10f)]
    public float spawnCooldown = 0.5f; 
    [Range(1, 10)]
    public float yOffset = 1;
    private bool canSpawn = true;

    void Update()
    {
        
        if (canSpawn && unitsAvailable > 0 && Input.GetMouseButton(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000) && hit.transform.gameObject.layer == LayerMask.NameToLayer("SpawnAllowed"))
            {
                canSpawn = false;
                unitsAvailable--;
                Quaternion direction = Quaternion.identity;
                direction.SetLookRotation(Vector3.zero - hit.point, Vector3.up);
                Instantiate(toSpawn, new Vector3(hit.point.x, hit.point.y + yOffset, hit.point.z), direction);
                StartCoroutine(SpawnCooldown());
            }
        }
        
    }

    IEnumerator SpawnCooldown()
    {
        yield return new WaitForSeconds(spawnCooldown);
        canSpawn = true;
    }

}
