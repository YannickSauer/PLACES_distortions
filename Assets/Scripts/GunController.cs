using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;


public class GunController : MonoBehaviour
{
    public GameObject hitDotPrefab; // Assign in inspector

    private GameObject hitDotInstance;

    void Start()
    {
        // Create the dot at runtime
        if (hitDotPrefab != null)
            hitDotInstance = Instantiate(hitDotPrefab);
    }    

    // Update is called once per frame
    void Update()
    {
        // perform raycast in direction of the gun and show a point at the hit position
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit))
        {
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
            // Move the dot to the hit point
            if (hitDotInstance != null)
            {
                hitDotInstance.SetActive(true);
                hitDotInstance.transform.position = hit.point;
                hitDotInstance.transform.rotation = Quaternion.LookRotation(hit.normal); // Optional: orient to surface
            }
        }
        else
        {
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1000, Color.white);
        }
       
    }
    
    private void OnFire()
    {
        Debug.Log("Shoot");
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit))
        {
            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * hit.distance, Color.cyan);
            // check if the hit object is a balloon
            Balloon balloon = hit.collider.GetComponent<Balloon>();
            if (balloon != null)
            {
                // pop the balloon
                balloon.Pop();
            }
        }
    }

}
