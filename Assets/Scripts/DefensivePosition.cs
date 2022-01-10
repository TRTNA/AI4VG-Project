using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DefensivePosition : MonoBehaviour, ISubject
{
    [Range(1, 20)]
    public float reservationExpiration = 10f;
    public GameObject occupiedBy = null;
    private ISet<IObserver> observers;
    public GameObject reservedBy = null;

    public void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        observers = new HashSet<IObserver>();
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
            Notify();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (occupiedBy == null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
            reservedBy = null;
            Notify();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (occupiedBy == null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
            reservedBy = null;
            Notify();
        }
    }

    public void Notify()
    {
        foreach (var o in observers)
        {
            o.OnNotify(gameObject, occupiedBy != null);
        }
    }

    public void AddObserver(IObserver o)
    {
        if (o != null) observers.Add(o);
    }

    public void RemoveObserver(IObserver o)
    {
        observers.Remove(o);
    }
}
