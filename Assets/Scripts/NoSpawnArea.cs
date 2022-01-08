using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class NoSpawnArea : MonoBehaviour
{
    public Material flashMaterial;

    private bool flashing = false;
    private Material baseMaterial;
    private Renderer myRenderer;

    void Start()
    {
        myRenderer = GetComponent<Renderer>();
        baseMaterial = myRenderer.material;
    }

    // Update is called once per frame
    void Update()
    {

        if (!flashing && Input.GetMouseButton(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit, 1000) && hit.transform.gameObject == gameObject)
            {
                StartCoroutine(Flash());

            }
        }
    }

    IEnumerator Flash()
    {
        flashing = true;
        myRenderer.material = flashMaterial;
        for (var i = 0; i < 10; i++)
        {
            myRenderer.enabled = ! myRenderer.enabled;
            yield return new WaitForSeconds(0.25f);
        }

        myRenderer.material = baseMaterial;
        flashing = false;
    }

}
