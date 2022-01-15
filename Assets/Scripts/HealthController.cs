using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthController : Target, ISubject
{
    public List<IObserver> observers = new List<IObserver>(); 
    public float health = 100f;
    public Material damagedMaterial;

    private Color baseMaterialColor;
    private Renderer renderer;
    private float maxHealth;
    private OnHealthDroppedToZero onHealthDroppedToZero;
    private bool isPlaying;

    void Start()
    {
        maxHealth = health;
        renderer = GetComponent<Renderer>();
        baseMaterialColor = renderer.material.color;
    }

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
                if (!isPlaying) StartCoroutine(ShowDamages());

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

    IEnumerator ShowDamages()
    {
        isPlaying = true;
        float t = 0f;
        float incr = 0.1f;
        while(t < 1f)
        {
            gameObject.GetComponent<Renderer>().material.color = Color.Lerp(baseMaterialColor, damagedMaterial.color, t);
            t += incr;
            yield return new WaitForSeconds(0.01f);
        }

        t = 0f;
        while (t < 1f)
        {
            gameObject.GetComponent<Renderer>().material.color = Color.Lerp(damagedMaterial.color, baseMaterialColor, t);
            t += incr;
            yield return new WaitForSeconds(0.01f);
        }


        gameObject.GetComponent<Renderer>().material.color = baseMaterialColor;
        isPlaying = false;

    }
}

public delegate void OnHealthDroppedToZero();

