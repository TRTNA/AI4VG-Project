using System;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Camera))]
public class CameraControl : MonoBehaviour
{

    public GameObject target;//the target object
    [Range(10, 1000)]
    public float rotationSpeed = 20.0f;

    [Range(1, 50)]
    public int maxOrthographicCameraSizeValue = 30;
    [Range(1, 50)]
    public int minOrthographicCameraSizeValue = 10;

    private Vector3 point;//the point coordinates where the camera looks at

    private float horizontalInput;
    private float scrollDelta;
    private Camera myCamera;

    void Start()
    {//Set up things on the start method
        if (maxOrthographicCameraSizeValue < minOrthographicCameraSizeValue)
        {
            maxOrthographicCameraSizeValue = 30;
            minOrthographicCameraSizeValue = 10;
        }
        point = target.transform.position;//get target's coord
        transform.LookAt(point);//makes the camera look to it
        myCamera = GetComponent<Camera>();
    }

    void Update()
    {//makes the camera rotate around "point" coords, rotating around its Y axis, 20 degrees per second times the speed modifier
        horizontalInput = Input.GetAxis("Horizontal");
        scrollDelta = Input.mouseScrollDelta.y;
        int cameraSizeDelta = 0;
        if (scrollDelta != 0) cameraSizeDelta = scrollDelta > 0 ? -1 : 1;
        transform.RotateAround(point, Vector3.up, -horizontalInput * rotationSpeed * Time.deltaTime);
        float newOrthographicSizeValue = Mathf.Clamp(myCamera.orthographicSize + cameraSizeDelta, minOrthographicCameraSizeValue, maxOrthographicCameraSizeValue);
        GetComponent<Camera>().orthographicSize = newOrthographicSizeValue;

    }


}