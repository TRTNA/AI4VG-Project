using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HealthController))]
public class Gate : MonoBehaviour, IObserver
{
    private bool isOpen = false;

    public void OnNotify(GameObject subject, object status)
    {
        if (status.GetType() == typeof(int) && (int)status == 0)
        {
            Debug.Log("notifying to " + gameObject.name + " that health is 0");
            isOpen = true;
            //temporary
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            gameObject.GetComponent<BoxCollider>().enabled = false;
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

    private void OnBreached()
    {
        isOpen = true;
    }

}
