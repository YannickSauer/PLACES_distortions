using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VORTest : MonoBehaviour
{
    public float startWaitTime = 2f; // inter-stimulus interval
    public float targetDuration = 1f; // duration of target presentation
    public float[] targetHeightVisualAngle = {10f , -20f}; // target location as height visual angle
    public string fileName = "test.csv";
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
    private EyeTrackingToolbox eyeTracker;
    private ExperimentManager expManager;
    private float initTargetScale;
    private GameObject room;

    
    void Start()
    {
        eyeTracker = EyeTrackingToolbox.Instance;
        expManager = ExperimentManager.Instance;
        // initialize targetHeightPerTrial
        targetHeightPerTrial = GetTargetHeightPerTrial();

        // adjust the target size for distortions, i.e. reduce the size if the image is magnified. It should appear in the same size always
        initTargetScale = transform.localScale.x; // assuming all scale components are the same
        transform.localScale = initTargetScale / Camera.main.GetComponent<Distortions>().magn * new Vector3(1f, 1f, 1f);
        
        // "hide" target in the beginning
        transform.localPosition = new Vector3(0, 0, -2000f);


        // find the room object, which is invisible during trials
        room = GameObject.Find("Room");
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

    public IEnumerator RunTest(int nTrials, bool invisibleTarget)
    {
        eyeTracker.StartRecording(fileName);
        // wait for the participant to rotate towards the test direction
        while (Vector3.Angle(Camera.main.transform.forward, Vector3.forward) > 10f)
        {
            yield return null;
        }
         
        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);

        room.SetActive(false);

        // loop trough all target positions
        initialRotation = Camera.main.transform.rotation;
        initialPosition = Camera.main.transform.position;

        for (int i = 0; i < targetHeightVisualAngle.Length; i++)
        {
            for (int j = 0; j < nTrials; j++)
            {
                // set target position for current trial
                transform.position = initialPosition + initialRotation * new Vector3(0, 0, targetDistance);
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
                currentTrial++;
            }   
        }
        Debug.Log("Aftereffect test completed.");
        eyeTracker.StopRecording();
    }

    void PlayBeep()
    {
        GetComponent<AudioSource>().Play();
    }

    // return horizontal rotation of the camera relative to the starting position
    private float GetYawRotation()
    {
        float deltaAngle = Mathf.DeltaAngle(initialRotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.y);
        return deltaAngle;
    }
}