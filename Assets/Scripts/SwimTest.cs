using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using TMPro;

public class SwimTest : MonoBehaviour
{
    public float startWaitTime = 0.1f; // inter-stimulus interval
    public string filePath = "swimTest.csv";
    public string eyeTrackerFileName = "swimTest_gaze.csv";
    public float targetDistance = 10f;
    public bool invisibleTarget = false;
    public float headRotationThreshold = 10f;
    public float timingThreshold = 0.2f; // timing offset allowed for the participant
    public float centerThreshold = 2f;
    public float metronomeFrequency = 1f; // frequency of the metronome in Hz

    private ExperimentManager.AftereffectData aftereffectData;
    public float[] magnificationTrial;
    public float[] radialTrial;
    public float magnification;
    public float radial;
    private float[] targetHeightPerTrial;
    private Quaternion initialRotation; // save initial rotation of the camera for each trial
    private Vector3 initialPosition;
    private EyeTrackingToolbox eyeTracker;
    private ExperimentManager expManager;
    private DotManager dotManager;
    private float initTargetScale;
    public GameObject scene;
    private float lastBeepTime;
    private float nextBeepTime;

    
    void Start()
    {
        eyeTracker = EyeTrackingToolbox.Instance;
        expManager = ExperimentManager.Instance;
        
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

        filePath = "swimTest_" + ExperimentManager.Instance.subjectID + ".csv";

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
            aftereffectData = new ExperimentManager.AftereffectData(); // start with default values    
            StartCoroutine(RunTest());
        }
        aftereffectData = ExperimentManager.Instance.aftereffectData; // get the aftereffect data from the experiment manager
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

    // Training phase for participants to learn the timing of the head movements
    // a metronome sound is played at a given frequency and the participant has to move their head in time with the sound
    // if the time between metronome and the head passing the head rotation threshold is less than the timing threshold, then the trial is considered good
    // if the participant moves their head in the wrong time, then the trial is considered bad and the good trial counter is reset
    // the training is completed when the participant has 10 good trials in a row
    public IEnumerator RunTraining()
    {
        yield return null;
        yield return null;
        Debug.Log("Starting headmovement training.");
        //eyeTracker.StartRecording(fileName);
        //TODO: fill the trial variables        
        // wait for the participant to rotate towards the test direction
        while (Vector3.Angle(Camera.main.transform.forward, Vector3.forward) > 10f)
        {
            yield return null;
        }

        var trainingsDisp = GameObject.Find("Training");
        Debug.Log(new Vector3(trainingsDisp.transform.position.x, Camera.main.transform.position.y, trainingsDisp.transform.position.z));
        trainingsDisp.transform.position = new Vector3(trainingsDisp.transform.position.x, Camera.main.transform.position.y, trainingsDisp.transform.position.z);

        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);

        // loop trough all target positions
        initialRotation = Camera.main.transform.rotation;
        initialPosition = Camera.main.transform.position;
        IEnumerator metronomeCoroutine = Metronome();
        StartCoroutine(metronomeCoroutine); // start background metronome sound as indicator for the participant to move their head in the given rythm
        int goodTrial = 0;
        TMP_Text goodTrialText = GameObject.Find("Text").GetComponent<TMP_Text>();
        goodTrialText.text = goodTrial.ToString();
        scene.SetActive(true);
        dotManager.active = false;
       
        if (eyeTracker != null)
        {
            eyeTracker.WriteMessage("StartTrainingTrial" + aftereffectData.currentTrial);
        }
        // save initial rotation of the camera
            
        // wait for full head rotation left or right
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
        PlayBeep(0.7f); // low pitch metronom sound
        if ((Time.time - lastBeepTime < timingThreshold) || (nextBeepTime - Time.time < timingThreshold))
        {
            // if the participant moved in the right time, then increase the good trial counter
            goodTrial++;
            goodTrialText.text = goodTrial.ToString();

        }
        else
        {
            goodTrial = 0;
            goodTrialText.text = goodTrial.ToString();
        }

        while (goodTrial < 10)
        {
            if (GetYawRotation() > 0f) // if the head is rotated to the right
            {
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            }
            else // if the head is rotated to the left
            {
               
                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            }
            PlayBeep(0.7f); // low pitch metronom sound
            if ((Time.time - lastBeepTime < timingThreshold) || (nextBeepTime - Time.time < timingThreshold))
            {
                // if the participant moved in the right time, then increase the good trial counter
                goodTrial++;
                goodTrialText.text = goodTrial.ToString();

            }
            else
            {
                // if the participant moved in the wrong time, then reset the good trial counter
                goodTrial = 0;
                goodTrialText.text = goodTrial.ToString();
            }
        }
        Debug.Log("Headmovement training completed.");
        StopCoroutine(metronomeCoroutine);
        if(eyeTracker != null)
        {
            eyeTracker.StopRecording();
        }
        
    }

    public IEnumerator RunTest(int nTrialsBlock = -1) // run a block of nTrialsBlock Trials. If 
    {
        if (nTrialsBlock == -1)
        {
            nTrialsBlock = aftereffectData.nTrials;
        }
        // set training GameObject to inactive
        var trainingGameObj = GameObject.Find("Training");
        trainingGameObj.SetActive(false);

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

        for (int trial = 0; trial < nTrialsBlock; trial++)
        {    
            // set the trial distortion
            magnification = aftereffectData.magnificationTrial[aftereffectData.currentTrial];
            radial = aftereffectData.radialTrial[aftereffectData.currentTrial];
            dotManager.distortionParam.x = magnification;
            dotManager.distortionParam.y = radial;
            
            // set scene and random dots for the current distortion
            dotManager.active = true;
            scene.SetActive(true);
            AdjustForMagnification();
            dotManager.Resample();
            dotManager.Reproject();
            scene.SetActive(false);

            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StartTestTrial" + aftereffectData.currentTrial);
            }
            // save initial rotation of the camera
            // wait for full head rotation left or right
            yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
            PlayBeep();

            if (GetYawRotation() > 0f) // initial rotation is to the right
            {
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
                PlayBeep();
                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
                PlayBeep();
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
                PlayBeep();

            }
            else // if the head is rotated to the left
            {
               
                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
                PlayBeep();
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
                PlayBeep();
                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
                PlayBeep();
            }


            // Wait for head to return to center
            yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) < centerThreshold);
            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StopTestTrial" + aftereffectData.currentTrial);
            }
            PlayBeep();

            // remove the random dots
            dotManager.active = false;

            // wait for participant answer
            yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow));
            // you can check which key was pressed
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SaveTrial(1);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SaveTrial(2);
            }

            //GetComponent<Renderer>().material.color = Color.green;
            yield return new WaitForSeconds(startWaitTime);
            aftereffectData.currentTrial++;
        }
        Debug.Log("Aftereffect test completed.");
        if (eyeTracker != null)
        {
            eyeTracker.StopRecording();
        }
    }

    void SaveTrial(int answer)
    {
        // save the trial data
        string trialData = aftereffectData.currentTrial + "," + Time.time + "," + aftereffectData.magnificationTrial[aftereffectData.currentTrial] + "," + aftereffectData.radialTrial[aftereffectData.currentTrial] + "," + answer;
        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine(trialData);
        }
        aftereffectData.answerTrial[aftereffectData.currentTrial] = answer; 
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