using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DefensivePosition : MonoBehaviour
{
    private GameObject occupiedBy = null;

    public void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    public bool IsOccupied()
    {
        return occupiedBy != null;
    }

    public GameObject OccupiedBy()
    {
        return occupiedBy;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Defender"))
        {
            Debug.Log("Exit!");
            occupiedBy = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (occupiedBy != null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (occupiedBy == null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
        }
    }
}
