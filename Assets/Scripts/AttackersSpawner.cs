using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackersSpawner : MonoBehaviour
{
    public Transform toSpawn;
    [Range(1, 100)]
    public int unitsAvailable = 10;
    [Range(1, 10)]
    public int spawnCooldown = 2; //TODO: possibile feature, diminuire cooldown verso fine partita?
    [Range(1, 10)]
    public float yOffset = 1;
    private bool canSpawn = true;
    private Vector3 attackerDirectionAtSpawn = Vector3.zero;

    // Update is called once per frame
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
                direction.SetLookRotation(attackerDirectionAtSpawn - hit.point, Vector3.up);
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
