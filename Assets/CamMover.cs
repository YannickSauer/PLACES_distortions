using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMover : MonoBehaviour
{
    public float speed = 50f; // speed of the camera rotation
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // when a or d are pressed, rotate the came to the left or right
        if (Input.GetKey(KeyCode.A))
        {
            transform.Rotate(Vector3.up, -speed*Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Rotate(Vector3.up, speed*Time.deltaTime);
        }
    }
}
