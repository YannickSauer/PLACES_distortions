using System;
using System.Collections;
using System.Collections.Generic;
//using System.Numerics;
using UnityEngine;

public class InterTrialInterval : MonoBehaviour
{

    GameObject cam;
    public GameObject movingTargetPrefab;
    public GameObject fixedTargetPrefab;
    GameObject movingTarget;
    GameObject fixedTarget;
    public bool isActive;
    public float duration  = 0.3f;
    float startTime;
    public float rangePos = 0.3f;
    public float rangeAngle = 3f;
    public Vector3 initPos = new Vector3(0,0,0);
    public Quaternion initRot = new Quaternion(1,0,0,0);
    public bool isPoseReset;
    public bool isRotationReset;

    public float fixedTargetDistance = 4;
    public float movingTargetDistance = 2;

    public float dist;
    public float angle;

    public float diffNear;
    public float diffFar;

    public bool isRunning = false;

    Vector3 currPos;
    Quaternion currRot;

    Renderer renderNear;
    Renderer renderFar;

    // ExperimentManager expManager;

    // Start is called before the first frame update
    void Start()
    {
        cam = GameObject.Find("Main Camera");
        initPos = cam.transform.position;
        initRot = Quaternion.identity;

        movingTarget = Instantiate(movingTargetPrefab, cam.transform);
        movingTarget.transform.localPosition = new Vector3(0, 0, movingTargetDistance);

        Vector3 fixedTargetPosition = cam.transform.position + new Vector3(0, 0, fixedTargetDistance);
        fixedTarget = Instantiate(fixedTargetPrefab);
        fixedTarget.transform.position = initRot * (initPos + Vector3.forward * fixedTargetDistance);

        isActive = false;
        
        movingTarget.SetActive(isActive);
        fixedTarget.SetActive(isActive);

        // expManager = GetComponent<ExperimentManager>();
        renderNear = movingTarget.GetComponent<Renderer>();
        renderFar = fixedTarget.GetComponent<Renderer>();   
    }

    void Update(){
        //  // ! Uncomment to test ITI manager individually !
        // if (isRunning){
        //     isActive = true;
        //     StartCoroutine(InterTripletInterval());
        //     isRunning = false;
        // }
    }

    public IEnumerator InterTripletInterval()
    {
        isActive = true;    
        // Show ISI scene and start timer
        if (!movingTarget.activeSelf && !fixedTarget.activeSelf)
        {
            Debug.Log("Targets not active.");
            movingTarget.SetActive(isActive);
            fixedTarget.SetActive(isActive);

            fixedTarget.transform.position = initRot * (initPos + Vector3.forward * fixedTargetDistance);
            // movingTarget.transform.position = cam.transform.position + cam.transform.rotation * Vector3.forward * shortDistance;

            currPos = cam.transform.position;
            currRot = cam.transform.rotation;

            startTime = Time.time;

            yield return null;
        }

        // Check if ISI has passed and if pose and rotation are restored
        while (Time.time <= startTime + duration || !isRotationReset || !isPoseReset)
        {
            Debug.Log("Still waiting to reset.");
            currPos = cam.transform.position;
            currRot = cam.transform.rotation;

            // Calculate pose and rotation difference of curr intial pos & rot
            angle = Quaternion.Angle(currRot, initRot);
            dist = Vector3.Distance(currPos, initPos);
            isPoseReset = dist <= rangePos;
            isRotationReset = angle <= rangeAngle;

            diffNear = dist - rangePos;
            diffFar = angle - rangeAngle;
            float maxDiffNear = 0.5f*rangePos;
            float maxDiffFar = 2f*rangeAngle;

            // Map the difference to a color gradient from red to green
            Color lerpedColorNear = Color.Lerp(Color.green, Color.white, Mathf.Clamp01(diffNear / maxDiffNear));
            Color lerpedColorFar = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(diffFar / maxDiffFar));

            renderNear.material.color = lerpedColorNear;
            renderFar.material.color = lerpedColorFar;
            
            yield return null; 
        }
        // Reset everything
        renderNear.material.color = Color.white;
        renderFar.material.color = Color.red;
        startTime = float.NaN;
        isActive = false;
        isPoseReset = false;
        isRotationReset = false;
        movingTarget.SetActive(isActive);
        fixedTarget.SetActive(isActive);
    }
}
