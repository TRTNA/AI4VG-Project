using UnityEngine;
using System.Collections;

public class CameraControl : MonoBehaviour
{
    public GameObject target;//the target object
    public float rotationSpeed = 20.0f;
    public float zoomMultiplier = 10.0f;
    private float speedMod = 10.0f;//a speed modifier
    private Vector3 point;//the coord to the point where the camera looks at
    private float horizontalInput;
    private float scrollDelta;

    void Start()
    {//Set up things on the start method
        point = target.transform.position;//get target's coord
        transform.LookAt(point);//makes the camera look to it
    }

    void Update()
    {//makes the camera rotate around "point" coords, rotating around its Y axis, 20 degrees per second times the speed modifier
        horizontalInput = Input.GetAxis("Horizontal");
        scrollDelta = Input.mouseScrollDelta.y;
        transform.RotateAround(point, Vector3.up, -horizontalInput * rotationSpeed * Time.deltaTime * speedMod);
        transform.Translate(Vector3.forward * scrollDelta * zoomMultiplier * Time.deltaTime);
    }


}