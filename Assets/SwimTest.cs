using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SwimTest : MonoBehaviour
{
    public float startWaitTime = 2f; // inter-stimulus interval
    public float targetDuration = 1f; // duration of target presentation
    public string filePath = "swimTest.csv";
    public string eyeTrackerFileName = "swimTest_gaze.csv";
    public float targetDistance = 10f;
    public bool invisibleTarget = false;
    public int nTrials = 5; // number of trials
    public float[] magnificationTrial = { 0.8f, 0.9f, 1f, 1.1f, 1.2f }; // visual angle of the target
    public float[] radialTrial = { 0.0f, 0.0f, 0f, 0.0f, 0.0f }; // visual angle of the target
    public float leftRotationThreshold = -10f;
    public float rightRotationThreshold = 10f;
    public float centerThreshold = 2f;
    private float[] targetHeightPerTrial;
    private int currentTrial = 0;
    private Quaternion initialRotation; // save initial rotation of the camera for each trial
    private Vector3 initialPosition;
    private EyeTrackingManager eyeTracker;
    private ExperimentManager expManager;
    private DotManager dotManager;
    private float initTargetScale;
    public GameObject scene;

    
    void Start()
    {
        eyeTracker = EyeTrackingManager.instance;
        expManager = ExperimentManager.instance;
        
        dotManager = GetComponent<DotManager>();

        scene = GameObject.Find("Scene");

        // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is at the origin.
        initTargetScale = scene.transform.localScale.x; // assuming all scale components are the same
        AdjustForMagnification();

        // fill the trial variables
        for (int i = 0; i < nTrials; i++)
        {
            magnificationTrial[i] = Random.Range(0.8f, 1.2f);
            radialTrial[i] = 0.0f;
        }
        
        // Create file and write header if it does not exist
        if (!File.Exists(filePath))
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine("trial,timestamp,magnification,radial,response");
            }
        }
    }

    void AdjustForMagnification()
    {
        // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is at the origin.
        scene.transform.localScale = initTargetScale / dotManager.distortionParam.x * new Vector3(1f, 1f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
     
    }

    public IEnumerator RunTest(int nTrials)
    {
        //eyeTracker.StartRecording(fileName);
        //TODO: fill the trial variables        
        // wait for the participant to rotate towards the test direction
        while (Vector3.Angle(Camera.main.transform.forward, Vector3.forward) > 10f)
        {
            yield return null;
        }
         
        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);

        // loop trough all target positions
        initialRotation = Camera.main.transform.rotation;
        initialPosition = Camera.main.transform.position;

        for (int trial = 0; trial < nTrials; trial++)
        {
            // set the trial distortion
            dotManager.distortionParam.x = magnificationTrial[trial];
            dotManager.distortionParam.y = radialTrial[trial];
            
            // set scene and random dots for the current distortion
            scene.SetActive(true);
            AdjustForMagnification();
            dotManager.Resample();
            dotManager.Reproject();
            scene.SetActive(false);

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

            // wait for participant answer
            yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow));
             // You can check which key was pressed
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SaveTrial("L");
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SaveTrial("R");
            }

            GetComponent<Renderer>().material.color = Color.green;
            yield return new WaitForSeconds(startWaitTime);
            currentTrial++;
        }
        Debug.Log("Aftereffect test completed.");
        eyeTracker.StopRecording();
    }

    void SaveTrial(string answer)
    {
        // save the trial data
        string trialData = currentTrial + "," + magnificationTrial[currentTrial] + "," + radialTrial[currentTrial] + "," + answer;
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine(trialData);
        }

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