using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;
using UnityEditor.IMGUI.Controls;
using System;

public class SwimTest : MonoBehaviour
{
    public float startWaitTime = 0.1f; // inter-stimulus interval
    public string outputFolder; // should be set by ExperimentManager
    public float targetDistance = 10f;
    public bool invisibleTarget = false;
    public float headRotationThreshold = 10f;
    public float timingThreshold = 0.3f; // timing offset allowed for the participant
    public float centerThreshold = 2f;

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
    private bool fireLeftPressed = false;
    private bool fireRightPressed = false;
    private bool touchpadPressed = false;
    private string filePath;
    

    public Image flashImage;
    public Color colorRight = new Color(1f, 0f, 1f, 0.2f); // Magenta with 20% opacity
    public Color colorLeft = new Color(0f, 1f, 1f, 0.2f); // Cyan with 20% opacity
    public float flashDuration = 0.2f;
    private Coroutine flashCoroutine;


    [Header("Training settings")]
    public List<string> pathList;
    public float bpm; // frequency of the metronome in bpm
    public float firstBeat;
    public AudioClip beepClip;
    public AudioClip metronomeClip;
    private float lastBeatNum = 0;
    private GameObject trainingsObj;
    private AudioSource metronomeMusic;
    private AudioSource beepSource;

    public event Action<string> OnNextTrainingStep;

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

        beepSource = GetComponent<AudioSource>();

        scene = GameObject.Find("Scene");
        scene.SetActive(false);

        // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is at the origin.
        initTargetScale = scene.transform.localScale.x; // assuming all scale components are the same
        AdjustForMagnification();

        filePath = Path.Combine(ExperimentManager.Instance.outputDirectory, "answers.csv");

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

    // public IEnumerator Metronome()
    // {
    //     // set waitTime according to metronome frequency
    //     float beatInterval = 60f/bpm;
    //     Debug.Log("Wait time: " + beatInterval);
    //     // set metronome audio to start at first beat
    //     metronomeSong.time = firstBeat;
    //     Debug.Log("Start song: "+metronomeSong.time+ " "+ firstBeat);
    //     lastBeatTime = firstBeat;
    //     nextBeatTime = lastBeatTime + beatInterval;
    //     metronomeSong.Play();

    //     while (true)
    //     {
    //         // predict next beat
    //         nextBeatTime = lastBeatTime + beatInterval;
    //         Debug.Log("Last Beat: " + lastBeatTime + ", Next Beat: " + nextBeatTime);
    //         // wait for next beat
    //         Debug.Log("Time before wait: " + Time.time);
    //         yield return new WaitForSecondsRealtime(beatInterval);
    //         Debug.Log("Time after wait: " + Time.time);
    //         // record time of beat
    //         lastBeatTime = metronomeSong.time;

    //     }
    // }

    public bool CheckOnBeat()
    {
        Debug.Log("Check On Beat: " + AudioSettings.dspTime);
        float beatInterval = 60f / bpm;
        float currSongTime = metronomeMusic.time - firstBeat;

        if (currSongTime < 0f) return false;

        Debug.Log("Position in song: " + metronomeMusic.time);
        float beatNum = Mathf.Round(currSongTime / beatInterval);

        Debug.Log("Last beat: " + lastBeatNum + " vs beat Num: " + beatNum);
        if (beatNum - lastBeatNum > 1)
        {
            lastBeatNum = beatNum;
            return false;
        }
        else { lastBeatNum = beatNum; }

        float nearestBeatTime = beatNum * beatInterval;
        float error = Mathf.Abs(currSongTime - nearestBeatTime);
        Debug.Log("Error: " + error);

        return error < timingThreshold;
    }

    // Training phase for participants to learn the timing of the head movements
    // a metronome sound is played at a given frequency and the participant has to move their head in time with the sound
    // if the time between metronome and the head passing the head rotation threshold is less than the timing threshold, then the trial is considered good
    // if the participant moves their head in the wrong time, then the trial is considered bad and the good trial counter is reset
    // the training is completed when the participant has 10 good trials in a row
    public IEnumerator RunTraining()
    {
        // Prepare Training
        trainingsObj = GameObject.Find("Training");

        for (int step = 0; step < pathList.Count; step++)
        {
            string pathToText = Path.Combine(Application.dataPath, pathList[step]);
            Debug.Log(pathToText);

            // TODO Put readtext to utils ?
            string nextInstructionText = ReadText(pathToText);
            Debug.Log(nextInstructionText);
            OnNextTrainingStep?.Invoke(nextInstructionText);

            switch (step)
            {
                case 0: // Practice head movement
                    yield return new WaitUntil(() => touchpadPressed);
                    OnNextTrainingStep?.Invoke("hide");
                    yield return StartCoroutine(HeadMovement());
                    break;
                case 1: // Practice head movement with metronome
                    yield return new WaitUntil(() => touchpadPressed);
                    OnNextTrainingStep?.Invoke("hide");
                    yield return StartCoroutine(RhythmicHeadMovement());
                    break;
                case 2: // Practice full swim test 
                    yield return new WaitUntil(() => touchpadPressed);
                    OnNextTrainingStep?.Invoke("hide");
                    yield return StartCoroutine(RunTest(3)); // run 3 trials of the swim test
                    break;

            }
            touchpadPressed = false;
        }
        Debug.Log("Headmovement training completed.");

        // StopCoroutine(metronomeCoroutine);
        if (eyeTracker != null)
        {
            eyeTracker.StopRecording();
        }
    }

    private IEnumerator HeadMovement()
    {
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
        PlayBeep(0.8f);
    }

    private IEnumerator RhythmicHeadMovement()
    {
        metronomeMusic = trainingsObj.GetComponent<AudioSource>();
        metronomeMusic.clip = metronomeClip;

        TMP_Text goodTrialText = GameObject.Find("Text").GetComponent<TMP_Text>();
        int goodTrial = 0;

        Debug.Log("Starting headmovement training.");
        //eyeTracker.StartRecording(fileName);
        //TODO: fill the trial variables   

        // wait for the participant to rotate towards the test direction
        while (Vector3.Angle(Camera.main.transform.forward, Vector3.forward) > 10f)
        {
            yield return null;
        }

        trainingsObj.transform.position = new Vector3(trainingsObj.transform.position.x, Camera.main.transform.position.y, trainingsObj.transform.position.z);

        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);

        // loop trough all target positions
        initialRotation = Camera.main.transform.rotation;
        initialPosition = Camera.main.transform.position;

        goodTrialText.text = goodTrial.ToString();
        scene.SetActive(true);
        dotManager.active = false;

        if (eyeTracker != null)
        {
            eyeTracker.WriteMessage("StartTrainingTrial" + aftereffectData.currentTrial);
        }

        // start background metronome sound as indicator for the participant to move their head in the given rythm
        metronomeMusic.Play();

        // wait for full head rotation left or right
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
        PlayBeep(0.7f); // low pitch metronom sound

        // if the participant moved in the right time, then increase the good trial counter
        goodTrial = CheckOnBeat() ? goodTrial+1 : 0;
        goodTrialText.text = goodTrial.ToString();

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

            goodTrial = CheckOnBeat() ? goodTrial+1 : 0;
            goodTrialText.text = goodTrial.ToString();
        }

        metronomeMusic.Stop();
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
        // wait for startWaitTime
        yield return new WaitForSeconds(2f);

        // beep to indicate the start of the test
        PlayBeep(0.4f);
        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);

        // loop trough all target positions
        for (int trial = 0; trial < nTrialsBlock; trial++)
        {
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break; // eary stop if less trials left than given by nTrialsBlock

            initialRotation = Camera.main.transform.rotation;
            initialPosition = Camera.main.transform.position;


            // set the trial distortion
            magnification = aftereffectData.magnificationTrial[aftereffectData.currentTrial];
            radial = aftereffectData.radialTrial[aftereffectData.currentTrial];
            dotManager.distortionParam.x = magnification;
            dotManager.distortionParam.y = radial;

            // set scene and random dots for the current distortion
            scene.SetActive(true);
            // rotate Scene in horizontal direction of camera
            scene.transform.rotation = Quaternion.Euler(0, Camera.main.transform.rotation.eulerAngles.y, 0);
            // scale scene to keep perceived distance independent of magnification
            AdjustForMagnification();
            yield return null; // wait for one frame, so that the new scene transform applies 
            // now project the dots onto the adjusted scene, before disabling the scene again
            dotManager.active = true;
            dotManager.Resample();
            dotManager.Reproject();
            scene.SetActive(false);

            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StartTestTrial" + aftereffectData.currentTrial);
            }
            // save initial rotation of the camera

            // wait for 5 head rotations
            yield return StartCoroutine(HeadMovement());

            // remove the random dots
            dotManager.active = false;

            fireLeftPressed = false;
            fireRightPressed = false;
            touchpadPressed = false;
            // wait for participant answer
            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.RightArrow) ||
                fireLeftPressed ||
                fireRightPressed ||
                touchpadPressed);
            // you can check which key was pressed or OnFire was called
            if (Input.GetKeyDown(KeyCode.LeftArrow) || fireLeftPressed)
            {
                SaveTrial(1);
                Flash(colorLeft, flashDuration);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || fireRightPressed)
            {
                SaveTrial(0);
                Flash(colorRight, flashDuration);
            }
            else
            {
                SaveTrial(2);
            }
            PlayBeep(0.4f); // feedback beep
            aftereffectData.currentTrial++;
            //GetComponent<Renderer>().material.color = Color.green;
            yield return new WaitForSeconds(startWaitTime);

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
        beepSource.pitch = pitch;
        beepSource.PlayOneShot(beepClip);
        Debug.Log("Beep triggered at DSP time: " + AudioSettings.dspTime);
    }

    public void Flash(Color color, float duration = 0.2f)
    {
        // Adjust orientation of flash canvas
        GameObject flashObj = flashImage.transform.parent.gameObject;
        flashObj.transform.rotation = Quaternion.Euler(0, Camera.main.transform.rotation.eulerAngles.y, 0);

        // Start flashing
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        float maxAlpha = color.a;
        color.a = 0;
        float t = 0;
        float halfDuration = duration / 2;
        
        // FadeIn
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            color.a = maxAlpha * t / halfDuration;
            flashImage.color = color;
            yield return null;
        }
        color.a = maxAlpha;
        flashImage.color = color;
        yield return null;

        // FadeOut
        t = 0;
        while (t < halfDuration)
        {
            t += Time.deltaTime;
            color.a = maxAlpha - maxAlpha * t / halfDuration;
            flashImage.color = color;
            yield return null;
        }
        
        flashImage.color = new Color(color.r, color.g, color.b, 0f); // Transparent
    }

    public string ReadText(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("No Textfile found.");
            return null;
        }
        string readText = File.ReadAllText(path);
        return readText;
    }

    // return horizontal rotation of the camera relative to the starting position
    private float GetYawRotation()
    {
        float deltaAngle = Mathf.DeltaAngle(initialRotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.y);
        return deltaAngle;
    }

    private void OnFireLeft() // called by the fire left button of the controller
    {
        fireLeftPressed = true;
    }

    private void OnFireRight() // called by the fire right button of the controller
    {
        fireRightPressed = true;
    }

    private void OnTouchpad() // called by the touchpad of the controller
    {
        touchpadPressed = true;
    }
}