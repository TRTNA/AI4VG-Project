using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HealthController))]
public class Gate : MonoBehaviour, IObserver
{
    private bool isOpen = false;

    public void OnNotify(GameObject subject, object status)
    {
        isOpen = true;
        gameObject.GetComponent<MeshRenderer>().enabled = false;
        gameObject.GetComponent<BoxCollider>().enabled = false;
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
