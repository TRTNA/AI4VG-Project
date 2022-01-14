using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DefensivePosition : MonoBehaviour
{
    [Range(1, 20)]
    public float reservationExpiration = 10f;
    private GameObject occupiedBy;
    private GameObject reservedBy;

    public void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    public bool Reserve(GameObject obj)
    {
        if (reservedBy == obj || occupiedBy == obj) return true;
        if (reservedBy != null || occupiedBy != null) return false;
        reservedBy = obj;
        StartCoroutine(ReservationExpire());
        return true;

    }

    public bool IsOccupied()
    {
        return occupiedBy != null;
    }

    public GameObject OccupiedBy()
    {
        return occupiedBy;
    }

    IEnumerator ReservationExpire()
    {
        yield return new WaitForSeconds(reservationExpiration);
        if (occupiedBy != reservedBy)
        {
            reservedBy = null;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Defender"))
        {
            occupiedBy = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (occupiedBy == null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
            reservedBy = null;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (occupiedBy == null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
            reservedBy = null;
        }
    }
}
