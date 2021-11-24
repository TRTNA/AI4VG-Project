using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HealthController))]
public class Gate : MonoBehaviour, IObserver
{
    private bool isOpen = false;

    public void OnNotify(ISubject subject, object status)
    {
        if (typeof(HealthController) == subject.GetType())
        {
            Debug.Log("notifying to " + gameObject.name + " that health is 0");
            isOpen = true;
            //temporary
            gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        gameObject.GetComponent<HealthController>().AddObserver(this);
    }

    public bool IsOpen()
    {
        return isOpen;
    }

}
