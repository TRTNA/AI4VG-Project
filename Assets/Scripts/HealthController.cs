using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthController : Target, ISubject
{
    public List<IObserver> observers = new List<IObserver>(); 
    public float health = 100f;
    private OnHealthDroppedToZero onHealthDroppedToZero;

    public float Health
    {
        get => health;
        set
        {
            if (value <= 0)
            {
                health = 0;
                Notify();
                observers.Clear();
                onHealthDroppedToZero?.Invoke();
            } else
            {
                health = value;
            }
        }
    }


    public override float GetHealth()
    {
        return Health;
    }


    public override void TakeDamage(float damage, GameObject attacker)
    {
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
        //shallow copy of list to allow called observers to remove themselves inside the OnNotify method
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
