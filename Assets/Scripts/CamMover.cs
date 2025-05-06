using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMover : MonoBehaviour
{
    public float speed = 0.1f; // speed of the camera rotation
   
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // while w is pressed, rotate the camera around the y-axis
        if (Input.GetKey(KeyCode.A))
        {
            transform.Rotate(Vector3.up, -speed*Time.deltaTime);
        }
        // while d is pressed, rotate the camera around the y-axis in the opposite direction
        if (Input.GetKey(KeyCode.D))
        {
            transform.Rotate(Vector3.up, speed*Time.deltaTime);
        }
    }
}
