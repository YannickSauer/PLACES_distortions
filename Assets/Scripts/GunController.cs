using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;


public class GunController : MonoBehaviour
{
    public Transform muzzle;          // Assign in inspector - the transform from which the laser is cast
    public GameObject hitDotPrefab;   // Assign in inspector

    [Tooltip("Name of the scene where the gun should be active (the balloon/adaptation scene).")]
    public string adaptationSceneName = "Adaptation";

    private GameObject hitDotInstance;
    private AdaptationTask adaptationTask;
    private bool gunActive = true;

    void Start()
    {
        // Create the dot at runtime, parented to the gun so it survives scene reloads
        if (hitDotPrefab != null && hitDotInstance == null)
            hitDotInstance = Instantiate(hitDotPrefab, transform);

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

        // Apply visibility for the scene we're currently in
        UpdateGunVisibility(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        UpdateGunVisibility(scene.name);
    }

    // Show the gun (and its hit dot) only in the adaptation scene; hide everywhere else (e.g. SwimTest).
    private void UpdateGunVisibility(string sceneName)
    {
        bool shouldBeVisible = (sceneName == adaptationSceneName);

        // Toggle all renderers on the gun and its children (dot included).
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = shouldBeVisible;
        }

        // Also toggle all colliders, so the SwimTest dot projection doesn't hit the
        // (invisible) gun collider and project dots onto it.
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            c.enabled = shouldBeVisible;
        }

        gunActive = shouldBeVisible;
    }

    // Update is called once per frame
    void Update()
    {
        if (!gunActive) return; // gun is hidden (e.g. in SwimTest scene) - do nothing

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
        if (!gunActive) return; // ignore shooting when gun is hidden

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