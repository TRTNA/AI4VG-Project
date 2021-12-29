using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthController : Target, ISubject
{
    public List<IObserver> observers = new List<IObserver>(); 
    public float _health = 100f;
    private float health = 100f;
    private OnHealthDroppedToZero onHealthDroppedToZero;

    public float Health
    {
        get
        {
            return _health;
        }
        set
        {
            if (value <= 0)
            {
                _health = 0;
                Notify();
                observers.Clear();
                onHealthDroppedToZero?.Invoke();
            } else
            {
                _health = value;
            }
        }
    }


    public override float GetHealth()
    {
        return Health;
    }


    public override void TakeDamage(float damage)
    {
        Debug.Log(gameObject.name + " suffered " + damage + " damages");
        Health -= damage;
    }

    public void SetOnHealthDroppedToZero(OnHealthDroppedToZero callback)
    {
        if (callback != null)
        {
            onHealthDroppedToZero = callback;
        }
    }

    public void Notify()
    {
        //shallow copy of list to allow called observers to remove theirsevlves inside the OnNotify method
        foreach (IObserver observer in new List<IObserver>(observers))
        {
            observer.OnNotify(gameObject, 0);
        }
    }

    public void AddObserver(IObserver observer)
    {
        if (observer != null)
        {
            observers.Add(observer);
        }
    }

    public void RemoveObserver(IObserver observer)
    {
        if (observer != null)
        {
            observers.Remove(observer);
        }
    }
}

public delegate void OnHealthDroppedToZero();
