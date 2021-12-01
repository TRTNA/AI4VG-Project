using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HealthController))]
public class TempDefender : MonoBehaviour, IObserver
{
    public void OnNotify(ISubject subject, object status)
    {
        if (typeof(HealthController) == subject.GetType())
        {
            Debug.Log("notifying to " + gameObject.name + " that health is 0");
            gameObject.SetActive(false);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        gameObject.GetComponent<HealthController>().AddObserver(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
