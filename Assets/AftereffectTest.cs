using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AftereffectTest : MonoBehaviour
{
    public float startWaitTime = 2f; // inter-stimulus interval
    public float targetDuration = 1f; // duration of target presentation
    public float[] targetHeightVisualAngle = {10f , -20f}; // target location as height visual angle
    public float targetDistance = 10f;
    public bool invisibleTarget = false;
    public int nTrials = 5; // trials per targetLocation
    public float leftRotationThreshold = -10f;
    public float rightRotationThreshold = 10f;
    public float centerThreshold = 2f;
    private float[] targetHeightPerTrial;
    private int currentTrial = 0;
    private Quaternion initialRotation; // save initial rotation of the camera for each trial
    private Vector3 initialPosition;
    private EyeTrackingManager eyeTracker;
    
    void Start()
    {
        eyeTracker = EyeTrackingManager.instance;
        // initialize targetHeightPerTrial
        targetHeightPerTrial = GetTargetHeightPerTrial();
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
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Starting aftereffect test.");
            StartCoroutine(RunTest());
        }
    }

    private IEnumerator RunTest()
    {
        eyeTracker.StartRecording("test1.csv");
        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);
        // loop trough all target positions
        initialRotation = Camera.main.transform.rotation;
        initialPosition = Camera.main.transform.position;

        for (int i = 0; i < targetHeightVisualAngle.Length; i++)
        {
            for (int j = 0; j < nTrials; j++)
            {
                // set target position for current trial
                transform.position = initialPosition + initialRotation * Quaternion.Euler(targetHeightPerTrial[currentTrial], 0, 0) * new Vector3(0, 0, targetDistance);
                transform.position = new Vector3(transform.position.x, initialPosition.y + Random.Range(-0.5f,0.5f), transform.position.z);
                GetComponent<Renderer>().material.color = Color.red;


                // make target invisible
                if (invisibleTarget)
                {
                    yield return new WaitForSeconds(0.3f);
                    transform.localPosition = Camera.main.transform.TransformPoint(new Vector3(0, 0, -10)); // TODO that's a horrible shortcut
                }
                eyeTracker.WriteMessage("StartTestTrial" + currentTrial);
                // save initial rotation of the camera
                
                // wait for full head rotation left
                yield return new WaitUntil(() => GetYawRotation() < leftRotationThreshold);
                PlayBeep();

                // Wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > rightRotationThreshold);
                PlayBeep();

                // wait for full head rotation left
                yield return new WaitUntil(() => GetYawRotation() < leftRotationThreshold);
                PlayBeep();

                // Wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > rightRotationThreshold);
                PlayBeep();

                // Wait for head to return to center
                yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) < centerThreshold);
                eyeTracker.WriteMessage("StopTestTrial" + currentTrial);
                PlayBeep();
                GetComponent<Renderer>().material.color = Color.green;
                yield return new WaitForSeconds(startWaitTime);
            }   
        }
        Debug.Log("Aftereffect test completed.");
    }

    void PlayBeep()
    {
        GetComponent<AudioSource>().Play();
    }

    // return horizontal rotation of the camera relative to the starting position
    private float GetYawRotation()
    {
        float deltaAngle = Mathf.DeltaAngle(initialRotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.y);
        Debug.Log(deltaAngle);
        return deltaAngle;
    }
}