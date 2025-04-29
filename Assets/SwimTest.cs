using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SwimTest : MonoBehaviour
{
    public float startWaitTime = 0.1f; // inter-stimulus interval
    public string filePath = "swimTest.csv";
    public string eyeTrackerFileName = "swimTest_gaze.csv";
    public float targetDistance = 10f;
    public bool invisibleTarget = false;
    public int nTrials = 5; // number of trials
    public float[] magnificationStimulusLevels = { 0.8f, 0.9f, 1f, 1.1f, 1.2f }; // visual angle of the target
    public float[] radialStimulisLevels = { 0.0f }; // visual angle of the target
    int stimulusRepetitions = 6; // number of repetitions for each stimulus level
    public float headRotationThreshold = 10f;
    public float timingThreshold = 0.2f; // timing offset allowed for the participant
    public float centerThreshold = 2f;
    public float metronomeFrequency = 1f; // frequency of the metronome in Hz
    
    private float[] magnificationTrial;
    private float[] radialTrial;
    private float[] targetHeightPerTrial;
    private int currentTrial = 0;
    private Quaternion initialRotation; // save initial rotation of the camera for each trial
    private Vector3 initialPosition;
    private EyeTrackingManager eyeTracker;
    private ExperimentManager expManager;
    private DotManager dotManager;
    private float initTargetScale;
    public GameObject scene;
    private float lastBeepTime;
    private float nextBeepTime;
    
    void Start()
    {
        eyeTracker = EyeTrackingManager.instance;
        expManager = ExperimentManager.instance;
        
        dotManager = Camera.main.GetComponent<DotManager>();
        if (dotManager == null)
        {
            Debug.LogError("Dot manager not found. Please add the DotManager component to the camera.");
            return;
        }

        scene = GameObject.Find("Scene");

        // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is at the origin.
        initTargetScale = scene.transform.localScale.x; // assuming all scale components are the same
        AdjustForMagnification();

        // fill the trial variables
        magnificationTrial = ExperimentPreparation.FillWithSamples(magnificationStimulusLevels, stimulusRepetitions, 1);
        radialTrial = ExperimentPreparation.FillWithSamples(radialStimulisLevels, stimulusRepetitions, 1);
        // randomly permute the trials
        ExperimentPreparation.RandPermute(magnificationTrial);
        ExperimentPreparation.RandPermute(radialTrial);

        nTrials = magnificationTrial.Length;

        // Create file and write header if it does not exist
        if (!File.Exists(filePath))
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine("trial,timestamp,magnification,radial,response");
            }
        }

        // if experiment is not found, then start the experiment
        if (eyeTracker == null)
        {
            Debug.LogError("Eye tracking manager not found. Please add the EyeTrackingManager component to the scene.");
        }
        if (expManager == null)
        {
            Debug.Log("Experiment manager not found. Starting the experiment by itself.");
            
            StartCoroutine(RunTraining());
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

    public IEnumerator Metronome()
    {
        // play a metronome sound at a given frequency
        AudioSource beep = GetComponent<AudioSource>();
        float waitTime = 1/metronomeFrequency;
        while (true)
        {
            PlayBeep(1.25f); // high pitch metronom sound
            // save time to compare participant movement with the metronome
            lastBeepTime = Time.time;
            nextBeepTime = lastBeepTime + waitTime;
            yield return new WaitForSeconds(waitTime);

            PlayBeep(1f); // low pitch metronom sound
            // save time to compare participant movement with the metronome
            lastBeepTime = Time.time;
            nextBeepTime = lastBeepTime + waitTime;
            yield return new WaitForSeconds(waitTime);
        }
    }

    public IEnumerator RunTraining()
    {
        Debug.Log("Starting headmovement training.");
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
        StartCoroutine(Metronome());
        int goodTrial = 0;
        scene.SetActive(true);
        dotManager.active = false;
       
            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StartTrainingTrial" + currentTrial);
            }
            // save initial rotation of the camera
            
            // wait for full head rotation left or right
            yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
            PlayBeep(0.7f); // low pitch metronom sound
            if ((Time.time - lastBeepTime < timingThreshold) || (Time.time - nextBeepTime < timingThreshold))
            {
                // if the participant moved in the right time, then increase the good trial counter
                goodTrial++;
            }
        while (goodTrial < 10)
        {
            if (GetYawRotation() > 0f)
            {
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            }
            else
            {
                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            }
            PlayBeep(0.7f); // low pitch metronom sound
            if ((Time.time - lastBeepTime < timingThreshold) || (Time.time - nextBeepTime < timingThreshold))
            {
                // if the participant moved in the right time, then increase the good trial counter
                goodTrial++;
            }
            else
            {
                // if the participant moved in the wrong time, then reset the good trial counter
                goodTrial = 0;
            }
        }
        Debug.Log("Headmovement training completed.");
        eyeTracker.StopRecording();
    }

    public IEnumerator RunTest()
    {
        Debug.Log("Starting aftereffect test.");
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
            dotManager.active = true;
            scene.SetActive(true);
            AdjustForMagnification();
            dotManager.Resample();
            dotManager.Reproject();
            scene.SetActive(false);

            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StartTestTrial" + currentTrial);
            }
            // save initial rotation of the camera
            
            // wait for full head rotation left
            yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            PlayBeep();

            // Wait for head to rotate right
            yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            PlayBeep();

            // wait for full head rotation left
            yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            PlayBeep();

            // Wait for head to rotate right
            yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            PlayBeep();

            // Wait for head to return to center
            yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) < centerThreshold);
            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StopTestTrial" + currentTrial);
            }
            PlayBeep();

            // remove the random dots
            dotManager.active = false;

            // wait for participant answer
            yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow));
            // you can check which key was pressed
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SaveTrial("L");
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SaveTrial("R");
            }

            //GetComponent<Renderer>().material.color = Color.green;
            yield return new WaitForSeconds(startWaitTime);
            currentTrial++;
        }
        Debug.Log("Aftereffect test completed.");
        eyeTracker.StopRecording();
    }

    void SaveTrial(string answer)
    {
        // save the trial data
        string trialData = currentTrial + "," + Time.time + "," + magnificationTrial[currentTrial] + "," + radialTrial[currentTrial] + "," + answer;
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine(trialData);
        }

    }


    void PlayBeep(float pitch = 1f)
    {
        AudioSource beep = GetComponent<AudioSource>();
        beep.pitch = pitch;
        beep.Play();
    }

    // return horizontal rotation of the camera relative to the starting position
    private float GetYawRotation()
    {
        float deltaAngle = Mathf.DeltaAngle(initialRotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.y);
        return deltaAngle;
    }
}