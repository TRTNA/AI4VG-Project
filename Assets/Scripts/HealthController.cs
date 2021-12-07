using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthController : Target, ISubject
{
    public List<IObserver> observers = new List<IObserver>(); 
    public float _health = 100f;

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

    public void Notify()
    {
        foreach (IObserver observer in observers)
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
