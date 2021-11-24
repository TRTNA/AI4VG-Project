using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthController : MonoBehaviour, ITarget, ISubject
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
            } else
            {
                _health = value;
            }
        }
    }


    public float GetHealth()
    {
        return Health;
    }


    public void TakeDamage(float damage)
    {
        Debug.Log(gameObject.name + " suffered " + damage + " damages");
        Health -= damage;
    }

    public void Notify()
    {
        foreach (IObserver observer in observers)
        {
            observer.OnNotify(this, 0);
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
