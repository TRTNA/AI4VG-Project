using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DefensivePosition : MonoBehaviour, ISubject
{
    public GameObject occupiedBy = null;
    private ISet<IObserver> observers;

    public void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        observers = new HashSet<IObserver>();
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
            occupiedBy = null;
            Notify();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (occupiedBy != null && other.CompareTag("Defender"))
        {
            occupiedBy = other.gameObject;
            Notify();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (occupiedBy == null && other.CompareTag("Defender"))
        {
            
            occupiedBy = other.gameObject;
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
