using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AftereffectTest : MonoBehaviour
{
    public float startWaitTime = 2f; // inter-stimulus interval
    public float targetDuration = 1f; // duration of target presentation
    public float[] targetHeightVisualAngle = {10f , -20f}; // target location as height visual angle
    public float targetDistance = 10f;
    public int nTrials = 5; // trials per targetLocation
    public float leftRotationThreshold = -10f;
    public float rightRotationThreshold = 10f;
    public float centerThreshold = 2f;
    private float[] targetHeightPerTrial;
    private int currentTrial = 0;
    private Quaternion initialRotation; // save initial rotation of the camera for each trial
    private EyeTrackingManager eyeTracker;
    
    void Start()
    {
        eyeTracker = EyeTrackingManager.instance;
        // initialize targetHeightPerTrial
        targetHeightPerTrial = GetTargetHeightPerTrial();

        Debug.Log("Starting aftereffect test.");
        StartCoroutine(RunTest());    
    }

    float[] GetTargetHeightPerTrial()
    {
        float[] targetHeightPerTrial = new float[nTrials*targetHeightVisualAngle.Length];
        for (int i = 0; i < targetHeightVisualAngle.Length; i++)
        {
            for (int j = 0; j < nTrials; j++)
            {
                targetHeightPerTrial[i*nTrials + j] = targetHeightVisualAngle[i];
            }
        }
        // shuffle targetHeightPerTrial
        for (int i = 0; i < targetHeightPerTrial.Length; i++)
        {
            float temp = targetHeightPerTrial[i];
            int randomIndex = Random.Range(i, targetHeightPerTrial.Length);
            targetHeightPerTrial[i] = targetHeightPerTrial[randomIndex];
            targetHeightPerTrial[randomIndex] = temp;
        }
        return targetHeightPerTrial;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private IEnumerator RunTest()
    {
        eyeTracker.StartRecording("test1.csv");
        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);
        // loop trough all target positions
        for (int i = 0; i < targetHeightVisualAngle.Length; i++)
        {
            for (int j = 0; j < nTrials; j++)
            {
                // set target position for current trial
                transform.localPosition = Quaternion.Euler(targetHeightPerTrial[currentTrial],0,0) * new Vector3(0, 0, targetDistance);
                // wait for target duration
                yield return new WaitForSeconds(targetDuration);
                // make target invisible
                transform.localPosition = new Vector3(0, 0, -10); // TODO that's a horrible shortcut
                eyeTracker.WriteMessage("StartTestTrial" + currentTrial);
                // save initial rotation of the camera
                initialRotation = Camera.main.transform.rotation;
                
                // wait for full head rotation left
                yield return new WaitUntil(() => GetYawRotation() < leftRotationThreshold);
        
                // Wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > rightRotationThreshold);

                // wait for full head rotation left
                yield return new WaitUntil(() => GetYawRotation() < leftRotationThreshold);
        
                // Wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > rightRotationThreshold);

                // Wait for head to return to center
                yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) < centerThreshold);
                eyeTracker.WriteMessage("StopTestTrial" + currentTrial);

            }   
        }
        Debug.Log("Aftereffect test completed.");
    }


    // return horizontal rotation of the camera relative to the starting position
    private float GetYawRotation()
    {
        return Mathf.DeltaAngle(initialRotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.y);
    }
}