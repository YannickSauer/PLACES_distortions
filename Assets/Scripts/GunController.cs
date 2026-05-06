using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;


public class GunController : MonoBehaviour
{
    public Transform muzzle;          // Assign in inspector - the transform from which the laser is cast
    public GameObject hitDotPrefab;   // Assign in inspector

    private GameObject hitDotInstance;
    private AdaptationTask adaptationTask;

    void Start()
    {
        // Create the dot at runtime
        if (hitDotPrefab != null)
            hitDotInstance = Instantiate(hitDotPrefab);

        adaptationTask = FindObjectOfType<AdaptationTask>();
        if (adaptationTask == null)
        {
            Debug.LogError("AdaptationTask not found in the scene.");
        }

        // Fallback: if no muzzle is assigned, use the GameObject's own transform
        if (muzzle == null)
        {
            Debug.LogWarning("Muzzle transform not assigned in GunController. Falling back to own transform.");
            muzzle = transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // perform raycast in direction of the muzzle and show a point at the hit position
        RaycastHit hit;
        if (Physics.Raycast(muzzle.position, muzzle.forward, out hit))
        {
            Debug.DrawRay(muzzle.position, muzzle.forward * hit.distance, Color.yellow);
            // Move the dot to the hit point
            if (hitDotInstance != null)
            {
                hitDotInstance.SetActive(true);
                hitDotInstance.transform.position = hit.point + hit.normal * 0.01f;
                hitDotInstance.transform.rotation = Quaternion.LookRotation(hit.normal); // Optional: orient to surface
            }
        }
        else
        {
            Debug.DrawRay(muzzle.position, muzzle.forward * 1000, Color.white);
        }
    }

    private void OnFireRight()
    {
        Debug.Log("Shoot");
        RaycastHit hit;
        if (Physics.Raycast(muzzle.position, muzzle.forward, out hit))
        {
            Debug.DrawRay(muzzle.position, muzzle.forward * hit.distance, Color.cyan);
            // check if the hit object is a balloon
            Balloon balloon = hit.collider.GetComponent<Balloon>();
            if (balloon != null)
            {
                // pop the balloon
                balloon.Pop(muzzle.forward);
            }
        }
    }

    // private void OnTouchpad()
    // {
    //     if (adaptationTask != null)
    //     {
    //         adaptationTask.TouchpadPressed();
    //     }
    // }
}