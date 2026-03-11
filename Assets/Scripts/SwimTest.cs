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

    [Header("Test settings")]
    public float wallDistance = 4.0f;
    public float startWaitTime = 0.1f; // inter-stimulus interval
    public float headRotationThreshold = 10f;
    public float timingThreshold = 0.3f; // timing offset allowed for the participant
    public float centerThreshold = 2f;
    private ExperimentManager.AftereffectData aftereffectData;
    private float magnification;
    private float radial;
    private Quaternion initialRotation; // save initial rotation of the camera for each trial
    private EyeTrackingToolbox eyeTracker;
    private ExperimentManager expManager;
    private DotManager dotManager;
    private Distortions camDistortions;
    private float initTargetScale;
    public GameObject scene;
    private bool fireLeftPressed = false;
    private bool fireRightPressed = false;
    private bool touchpadPressed = false;
    private string filePath;
    
    [Header("UI settings")]
    public Image flashImage;
    public Color colorRight = new Color(1f, 0f, 1f, 0.2f); // Magenta with 20% opacity
    public Color colorLeft = new Color(0f, 1f, 1f, 0.2f); // Cyan with 20% opacity
    public float flashDuration = 0.2f;
    private Coroutine flashCoroutine;

    [Header("Training settings")]
    public ExperimentManager.AftereffectSettings trainingSettings;
    public List<string> pathList;
    public float bpm; // frequency of the metronome in bpm
    public float firstBeat;
    public int minGoodTrials = 10; // number of good trials to complete the training

    public AudioClip beepClip;
    public AudioClip metronomeClip;
    public AudioClip correctClip;
    public AudioClip wrongClip;
    private float lastBeatNum = 0;
    private GameObject trainingObj;
    private GameObject directionArrow;
    private AudioSource metronomeMusic;
    private AudioSource beepSource;
    private ExperimentManager.AftereffectData trainingData;
    private bool isTraining; // flag to indicate if the training phase is active
    private Vector3 initHeadForward; // initial forward direction of the head
    public event Action<string> OnNextTrainingStep;
    public static event Action OnTurnHead;
    private Coroutine lastRoutine = null;


    public Vector3 GetInitHeadForward()
    {
        return initHeadForward;
    }
    void OnEnable()
    {
        OnTurnHead += PlayBeep;
    }

    void OnDisable()
    {
        OnTurnHead -= PlayBeep;
    }

    void Start()
    {
        eyeTracker = EyeTrackingToolbox.Instance;
        expManager = ExperimentManager.Instance;

        dotManager = Camera.main.GetComponent<DotManager>();
        camDistortions = Camera.main.GetComponent<Distortions>();
        if (dotManager == null)
        {
            Debug.LogError("Dot manager not found. Please add the DotManager component to the camera.");
            return;
        }

        if (camDistortions == null)
        {
            Debug.LogError("Camera distortions not found. Please add the Distortion component to the camera.");
            return;
        }

        beepSource = GetComponent<AudioSource>();

        scene = GameObject.Find("Scene");
        scene.SetActive(false);

        trainingObj = GameObject.Find("Training");
        ScaleGUI();

        directionArrow = GameObject.Find("ArrowSprite");
        directionArrow.SetActive(false);

        // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is at the origin.
        initTargetScale = scene.transform.localScale.x; // assuming all scale components are the same
        AdjustForMagnification();

        if (ExperimentManager.Instance != null)
        {
            aftereffectData = ExperimentManager.Instance.aftereffectData; // get the aftereffect data from the experiment manager
            filePath = Path.Combine(ExperimentManager.Instance.outputDirectory, "answers.csv");    
        
            // Create file and write header if it does not exist
            if (!File.Exists(filePath))
            {
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine("trial,timestamp,magnification,radial,response");
                }
            }
        }

        // if experiment is not found, then start the experiment
        if (eyeTracker == null)
        {
            Debug.LogError("Eye tracking manager not found. Please add the EyeTrackingManager component to the scene.");
        }
        if (expManager == null)
        {
            //Debug.Log("Experiment manager not found. Starting the experiment by itself.");
            //aftereffectData = new ExperimentManager.AftereffectData(); // start with default values    
            //StartCoroutine(RunTest());
        }
        

    }

    void ScaleGUI()
    {
        // position targetLeft and targetRight to indicate the max amplitude for the head rotation depending on current headRotationThreshold   
        GameObject targetLeft = trainingObj.transform.Find("Wall/UI/TargetLeft").gameObject;
        GameObject targetRight = trainingObj.transform.Find("Wall/UI/TargetRight").gameObject;

        float headTargetPos = wallDistance * Mathf.Tan(headRotationThreshold * Mathf.Deg2Rad);

        targetLeft.transform.localPosition = new Vector3(-headTargetPos, 0, 0);
        targetRight.transform.localPosition = new Vector3(headTargetPos, 0, 0);
        // scale the bar to reach from targetLeft to targetRight
        GameObject bar = trainingObj.transform.Find("Wall/UI/Bar").gameObject;
        bar.transform.localScale = new Vector3(2 * headTargetPos, bar.transform.localScale.y, bar.transform.localScale.z);

    }

    void Update()
    {
        if (expManager == null) // if expManager is null, then the scene was started outside of the experiment. Allow to run training or test by button press
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                RepositionScene();
            }
            if (Input.GetKeyDown(KeyCode.L) && !isTraining)
            {
                    Debug.Log("Manually started training.");
                    lastRoutine = StartCoroutine(RunTraining());
            }

            if (Input.GetKeyDown(KeyCode.T) && !isTraining)
            {
                StartCoroutine(RunTest());
            }
        }
        AdjustForMagnification();
    }

    void RepositionScene()
    {
        if (Camera.main != null)
        {
            // adjust origin of random dot scene and the trainingObj
            Vector3 headPosition = Camera.main.transform.position;
            scene.transform.position = headPosition;
            trainingObj.transform.position = headPosition;

            // get current head direction and adjust the position of scene and traingObj childrean
            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height
            initHeadForward = headForward.normalized;

            // adjust scene (used for random dots simulation during actual swim test)
            // Wall (child of scene) is always at wallDistance in front of the head
            scene.transform.Find("Wall").transform.localPosition = new Vector3(0f, 0f, wallDistance);
            scene.transform.rotation = Quaternion.LookRotation(headForward);

            ResampleAndReproject();

            // adjust trainigsObj (used for training phase)
            trainingObj.transform.Find("Wall").transform.localPosition = new Vector3(0f, 0f, wallDistance);
            trainingObj.transform.rotation = Quaternion.LookRotation(headForward);

        }
        else
        {
            Debug.LogError("Main Camera not found. Please ensure there is a camera tagged as 'MainCamera'.");
        }
    }

    void AdjustForMagnification()
    {
        // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is (roughtly) at the origin.
        // 1. adjust scene for randomDot magnification
        scene.transform.localScale = initTargetScale / dotManager.distortionParam.x * new Vector3(1f, 1f, 1f);
        // adjust training gameObject for distortion magnification
        if (trainingObj != null)
        {
            trainingObj.transform.localScale = camDistortions.magn * new Vector3(1f, 1f, 1f);
            trainingObj.transform.Find("Wall").transform.localScale = 1 / camDistortions.magn * new Vector3(1f, 1f, 1f);
        }
    }

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
    // GUI visualizes the headmovement
    // if the time between metronome and the head passing the head rotation threshold is less than the timing threshold, then the trial is considered good
    // if the participant moves their head in the wrong time, then the trial is considered bad and the good trial counter is reset
    // the training has multiple phases:
    // 1. practice head movements with metronome
    // 2. practice trial with metronome
    // individual trials with beats
    // transition distorted: this is "normal" vs this is "distorted"
    // transition to rand dots
    // answering "stability"
    public IEnumerator RunTraining()
    {
        List<float> exampleMags = new List<float> { 1.0f, 1.2f, 1.0f, 0.9f, 1.0f, 1.3f };
        isTraining = true; // set training flag to true
        dotManager.active = false; // make sure that the random dots are not visible during training

        // Prepare Training
        trainingData = new ExperimentManager.AftereffectData(trainingSettings);

        TMP_Text instructionText = GameObject.Find("TrainingText").GetComponent<TMP_Text>();


        for (int step = 0; step < pathList.Count; step++)
        {
            string pathToText = Path.Combine(Application.dataPath, pathList[step]);
            Debug.Log(pathToText);

            // TODO Put readtext to utils ?
            string nextInstructionText = DisplayInformation.ReadText(pathToText);
            Debug.Log(nextInstructionText);
            instructionText.text = nextInstructionText;

            yield return new WaitUntil(() =>
                        Input.GetKeyDown(KeyCode.LeftArrow) ||
                        Input.GetKeyDown(KeyCode.RightArrow) ||
                        Input.GetKeyDown(KeyCode.Space) ||
                        touchpadPressed);
                    if (Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        step = step - 2; // repeat the previous two steps
                    }
                    else if (Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        // repeat the previous step
                        step = step - 1;
                    }

            switch (step)
            {
                case 0: // Practice head movement with metronome
                    yield return StartCoroutine(MetronomeHeadMovement());
                    break;

                case 1: // Practice head movement with metronome but without headIndicator
                        // remove GUI elements
                    trainingObj.transform.Find("Wall/UI/Bar").gameObject.SetActive(false);
                    trainingObj.transform.Find("Wall/UI/HeadIndicator").gameObject.SetActive(false);
                    yield return StartCoroutine(MetronomeHeadMovement());
                    break;
                case 2: // Practice without metronome
                    yield return StartCoroutine(TrainingHeadMovement());
                    break;
                case 3: // Practice with distortions
                        // set distortions to training values
                    camDistortions.magn = 1.2f;
                    yield return StartCoroutine(TrainingHeadMovement());
                    camDistortions.magn = 1.0f;

                    // else: space pressed -> continue to next step
                    break;
                case 4: // Practice without metronome
                        
                        // do nothing here
                    break;
                case 5: // Practice with different distortion trials
                        // for loop over 6 example trials, continue until all are correct
                    exampleMags = new List<float> { 1.3f, 1.0f, 0.9f };//, 1.0f, 1.2f, 1.3f };
                    while (exampleMags.Count > 0)
                    {
                        float mag = exampleMags[0];
                        exampleMags.RemoveAt(0);
                        camDistortions.magn = mag;
                        yield return StartCoroutine(TrainingHeadMovement(4));
                        camDistortions.magn = 1.0f;

                        instructionText.text = "Press left if it felt unstable, right if it felt stable.";

                        yield return new WaitUntil(() =>
                            Input.GetKeyDown(KeyCode.LeftArrow) ||
                            Input.GetKeyDown(KeyCode.RightArrow));
                        int ans = -1;
                        if (Input.GetKeyDown(KeyCode.LeftArrow))
                        {
                            ans = 0; // unstable
                            Flash(colorLeft, flashDuration);
                        }
                        else if (Input.GetKeyDown(KeyCode.RightArrow))
                        {
                            ans = 1; // stable
                            Flash(colorRight, flashDuration);
                        }
                        // 1. it was a stable trial
                        if (mag == 1.0f)
                        {
                            if (ans == 1)
                            {
                                // correct
                                instructionText.text = "Correct! This was a stable trial.\nPress space key to continue.";
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);

                            }
                            else
                            {
                                instructionText.text = "Wrong! This was a stable trial.\nPress space key to continue.";
                                // add the current mag to the end of the exampleMags list, so that it will be repeated later
                                exampleMags.Add(mag);
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);
                            }
                        }
                        // 2. it was an unstable trial
                        if (mag != 1.0f)
                        {
                            if (ans == 1) // wrong
                            {
                                instructionText.text = "Wrong! This was an unstable trial.\nPress space key to continue.";
                                exampleMags.Add(mag);
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);

                            }
                            else // correct
                            {
                                instructionText.text = "Correct! This was an unstable trial.\nPress space key to continue.";
                                // add the current mag to the end of the exampleMags list, so that it will be repeated later
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);
                            }
                        }
                    }    
                    break;

                case 6: // Practice with random dots
                    exampleMags = new List<float> { 1.0f, 1.2f, 1.0f };//, 0.9f, 1.0f, 1.3f };
                    while (exampleMags.Count > 0)
                    {
                        float mag = exampleMags[0];
                        exampleMags.RemoveAt(0);
                        camDistortions.magn = mag;
                        yield return StartCoroutine(TrainingHeadMovement(4));
                        camDistortions.magn = 1.0f;

                        instructionText.text = "Press left if it felt unstable, right if it felt stable.";

                        yield return new WaitUntil(() =>
                            Input.GetKeyDown(KeyCode.LeftArrow) ||
                            Input.GetKeyDown(KeyCode.RightArrow));
                        int ans = -1;
                        if (Input.GetKeyDown(KeyCode.LeftArrow))
                        {
                            ans = 0; // unstable
                            Flash(colorLeft, flashDuration);
                        }
                        else if (Input.GetKeyDown(KeyCode.RightArrow))
                        {
                            ans = 1; // stable
                            Flash(colorRight, flashDuration);
                        }
                        // 1. it was a stable trial
                        if (mag == 1.0f)
                        {
                            if (ans == 1)
                            {
                                // correct
                                instructionText.text = "Correct! This was a stable trial.\nPress space key to continue.";
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);

                            }
                            else
                            {
                                instructionText.text = "Wrong! This was a stable trial.\nPress space key to continue.";
                                // add the current mag to the end of the exampleMags list, so that it will be repeated later
                                exampleMags.Add(mag);
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);
                            }
                        }
                        // 2. it was an unstable trial
                        if (mag != 1.0f)
                        {
                            if (ans == 1) // wrong
                            {
                                instructionText.text = "Wrong! This was a unstable trial.\nPress space key to continue.";
                                exampleMags.Add(mag);
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);

                            }
                            else // correct
                            {
                                instructionText.text = "Correct! This was a stable trial.\nPress space key to continue.";
                                // add the current mag to the end of the exampleMags list, so that it will be repeated later
                                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || touchpadPressed);
                            }
                        }
                    }    
                    break;
                case 7: // Practice without metronome
                    yield return StartCoroutine(TrainingHeadMovement());
                    break;
                default:
                    Debug.LogWarning("No training step defined for step " + step);
                    break;

            }
            touchpadPressed = false;
        }
        Debug.Log("Headmovement training completed.");

        if (eyeTracker != null)
        {
            eyeTracker.StopRecording();
        }
        isTraining = false; // set training flag to false
    }

private void AnimateRightTarget()
    {
        // animate the targetRight object to scale up and down
        GameObject targetRight = trainingObj.transform.Find("Wall/UI/TargetRight").gameObject;
        StartCoroutine(ScaleUpDown(targetRight));
    }

    private void AnimateLeftTarget()
    {
        // animate the targetLeft object to scale up and down
        GameObject targetLeft = trainingObj.transform.Find("Wall/UI/TargetLeft").gameObject;
        StartCoroutine(ScaleUpDown(targetLeft));
    }

    // scaleUpDown coroutine
    private IEnumerator ScaleUpDown(GameObject target)
    {
        Color oldColor = target.GetComponent<Renderer>().material.color;
        Vector3 originalScale = target.transform.localScale;
        Vector3 targetScale = originalScale * 3.0f;
        float duration = 0.1f;
        float elapsed = 0f;


        // scale up
        while (elapsed < duration)
        {
            target.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            // lerp color between oldColor and green
            target.GetComponent<Renderer>().material.color = Color.Lerp(oldColor, Color.green, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.transform.localScale = targetScale;

        // scale down
        elapsed = 0f;
        while (elapsed < duration)
        {
            target.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            target.GetComponent<Renderer>().material.color = Color.Lerp(Color.green, oldColor, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.transform.localScale = originalScale;
        target.GetComponent<Renderer>().material.color = oldColor;
    }

    private IEnumerator HeadMovement()
    {
        // save initial rotation and position of the camera
        initialRotation = Camera.main.transform.rotation;

        // wait for full head rotation left or right
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
        OnTurnHead?.Invoke();

        if (GetYawRotation() > 0f) // initial rotation is to the right
        {
            // wait for head to rotate left
            yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            OnTurnHead?.Invoke();
            // wait for head to rotate right
            yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            OnTurnHead?.Invoke();
            // wait for head to rotate left
            yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            OnTurnHead?.Invoke();

        }
        else // if the head is rotated to the left
        {
            // wait for head to rotate right
            yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            OnTurnHead?.Invoke();
            // wait for head to rotate left
            yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
            OnTurnHead?.Invoke();
            // wait for head to rotate right
            yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
            OnTurnHead?.Invoke();
        }

        // Wait for head to return to center
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) < centerThreshold);
        // Play a beep sound to indicate the end of the head movement
        PlayBeep(0.8f);
    }

    private void MetronomeStart()
    {
        Metronome metronome = trainingObj.GetComponent<Metronome>();
        if (metronome != null)
        {
            metronome.StartMetronome();
        }
        else
        {
            Debug.LogError("Metronome component not found on the GameObject.");
        }
    }

    private void MetronomeStop()
    {
        Metronome metronome = trainingObj.GetComponent<Metronome>();
        if (metronome != null)
        {
            metronome.StopMetronome();
        }
        else
        {
            Debug.LogError("Metronome component not found on the GameObject.");
        }
    }


    private IEnumerator MetronomeHeadMovement(int minGoodTrials = 10)
    {

        Metronome metronome = trainingObj.GetComponent<Metronome>();
        MetronomeStart();
        int goodTrial = 0;
        TMP_Text instructionText = GameObject.Find("TrainingText").GetComponent<TMP_Text>();
        instructionText.text = "Correct head movements: " + goodTrial.ToString();

        //if (eyeTracker != null)
        //{
        //            eyeTracker.WriteMessage("StartTrainingTrial" + aftereffectData.currentTrial);
        //}

        // save initial rotation of the camera

        // wait for full head rotation left or right
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
        // show head target animation
        if (GetYawRotation() > 0f) // if the head is rotated to the right
        {
            AnimateRightTarget();
        }
        else
        {
            AnimateLeftTarget();
        }


        if ((Time.time - metronome.GetLastBeatTime() < timingThreshold) || (metronome.GetNextBeatTime() - Time.time < timingThreshold))
        {
            // if the participant moved in the right time, then increase the good trial counter
            goodTrial++;
            instructionText.text = "Correct head movements: " + goodTrial.ToString();

        }
        else
        {
            goodTrial = 0;
            instructionText.text = "Correct head movements: " + goodTrial.ToString();
        }

        while (goodTrial < minGoodTrials)
        {
            if (GetYawRotation() > 0f) // if the head is rotated to the right
            {
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
                AnimateLeftTarget();
            }
            else // if the head is rotated to the left
            {

                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
                AnimateRightTarget();
            }
            if ((Time.time - metronome.GetLastBeatTime() < timingThreshold) || (metronome.GetNextBeatTime() - Time.time < timingThreshold))
            {
                // if the participant moved in the right time, then increase the good trial counter
                goodTrial++;
                instructionText.text = "Correct head movements: " + goodTrial.ToString();

            }
            else
            {
                // if the participant moved in the wrong time, then reset the good trial counter
                goodTrial = 0;
                instructionText.text = "Correct head movements: " + goodTrial.ToString();
            }
        }
        MetronomeStop();
    }

    private IEnumerator TrainingHeadMovement(int minGoodTrials = 10)
    {

        int goodTrial = 0;
        TMP_Text instructionText = GameObject.Find("TrainingText").GetComponent<TMP_Text>();
        instructionText.text = "Correct head movements: " + goodTrial.ToString();

        //if (eyeTracker != null)
        //{
        //            eyeTracker.WriteMessage("StartTrainingTrial" + aftereffectData.currentTrial);
        //}

        // save initial rotation of the camera

        // wait for full head rotation left or right
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > headRotationThreshold);
        // show head target animation
        if (GetYawRotation() > 0f) // if the head is rotated to the right
        {
            AnimateRightTarget();
            PlayBeep(1.0f);
        }
        else
        {
            AnimateLeftTarget();
            PlayBeep(1.25f);
        }


        
        // if the participant moved in the right time, then increase the good trial counter
        goodTrial++;
        instructionText.text = "Correct head movements: " + goodTrial.ToString();

        
        

        while (goodTrial < minGoodTrials)
        {
            if (GetYawRotation() > 0f) // if the head is rotated to the right
            {
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -headRotationThreshold);
                AnimateLeftTarget();
                PlayBeep(1.25f);

            }
            else // if the head is rotated to the left
            {

                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > headRotationThreshold);
                AnimateRightTarget();
                PlayBeep(1.0f);

            }
            
            // if the participant moved in the right time, then increase the good trial counter
            goodTrial++;
            instructionText.text = "Correct head movements: " + goodTrial.ToString();

            
        }
    }


    public IEnumerator ResampleAndReproject()
    {
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
    }

    public IEnumerator RunTest(int nTrialsBlock = -1) // run a block of nTrialsBlock Trials. If 
    {
        if (nTrialsBlock == -1)
        {
            nTrialsBlock = aftereffectData.nTrials;
        }

        Debug.Log("Starting aftereffect test.");
        // wait for startWaitTime
        yield return new WaitForSeconds(2f);

        // beep to indicate the start of the test
        PlayBeep();
        // wait for ISI before starting the test
        yield return new WaitForSeconds(startWaitTime);

        int ans = 0;
        // loop trough all target positions
        for (int trial = 0; trial < nTrialsBlock; trial++)
        {
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break; // eary stop if less trials left than given by nTrialsBlock

            // set the trial distortion
            magnification = aftereffectData.magnificationTrial[aftereffectData.currentTrial];
            radial = aftereffectData.radialTrial[aftereffectData.currentTrial];
            dotManager.distortionParam.x = magnification;
            dotManager.distortionParam.y = radial;

            // set scene and random dots for the current distortion
            yield return StartCoroutine(ResampleAndReproject());

            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StartTestTrial" + aftereffectData.currentTrial);
            }
            // save initial rotation of the camera

            // wait for 5 head rotations
            yield return StartCoroutine(HeadMovement());

            if (eyeTracker != null)
            {
                eyeTracker.WriteMessage("StopTestTrial" + aftereffectData.currentTrial);
            }

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
                ans = 0; // unnatural perception
                Flash(colorLeft, flashDuration);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || fireRightPressed)
            {
                ans = 1; // natural perception
                Flash(colorRight, flashDuration);
            }
            else
            {
                ans = 2; // no answer given
            }
            SaveTrial(ans);

            // give feedback to the participant
            if (isTraining)
            {
                yield return StartCoroutine(PlayAudioFeedback(ans, magnification));
            }
            else
            {
                PlayBeep();
                yield return new WaitForSeconds(startWaitTime);
            }

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


    void PlayBeep()
    {
        beepSource.pitch = 1f;
        beepSource.PlayOneShot(beepClip);
    }

    void PlayBeep(float pitch = 1f)
    {
        beepSource.pitch = pitch;
        beepSource.PlayOneShot(beepClip);
    }

    public void Flash(Color color, float duration = 0.2f)
    {
        // Adjust orientation of flash canvas
        GameObject flashObj = flashImage.transform.parent.gameObject;
        flashObj.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1f;
        flashObj.transform.LookAt(Camera.main.transform.position, Camera.main.transform.up);
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

    IEnumerator PlayAudioFeedback(int ans, float magnification)
    {
        AudioClip clip = null;
        if (ans == 1 && magnification == 1f || ans == 0 && magnification != 1f)
            clip = correctClip;
        else
            clip = wrongClip;

        if (clip != null)
        {
            beepSource.PlayOneShot(clip);
            yield return new WaitWhile(() => beepSource.isPlaying);
        }
        else
        {
            yield return null;
        }
    }

    private float GetYawRotation()
    {
        Vector3 headForward = Camera.main.transform.forward;
        headForward.y = 0; // Keep the GUI at head height
        // get the horizontal angle between initHeadForward and headForward
        float deltaAngle = Vector3.SignedAngle(initHeadForward, headForward, Vector3.up);
        return deltaAngle;
    }

    // return horizontal rotation of the camera relative to the starting position
    //private float GetYawRotation()
    //{
//        float deltaAngle = Mathf.DeltaAngle(initialRotation.eulerAngles.y, Camera.main.transform.rotation.eulerAngles.y);
    //return deltaAngle;
    //}

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