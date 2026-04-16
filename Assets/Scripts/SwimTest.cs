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
    private bool isTestRunning = false;
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
    public List<AudioClip> trainingAudioClips; // list of audio clips to play during the training, should be in the same order as the pathList.
    public float bpm; // frequency of the metronome in bpm
    public float firstBeat;
    public int minGoodTrials = 10; // number of good trials to complete the training

    public AudioClip beepClip;
    public AudioClip metronomeClip;
    public AudioClip instructionReminderClip;
    public AudioClip dotTransitionClip;
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
    private bool hideTrainingWall = false; // flag to indicate whether to hide the training wall during training (Tolga)


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

        //scene = GameObject.Find("Scene"); // original code
        //scene.SetActive(false); // original code

        // Only search if it hasn't been assigned in the Inspector (Tolga)
        if (scene == null)
        {
            scene = GameObject.Find("Scene");
        }

        // Check if it exists before trying to disable it
        if (scene != null)
        {
            scene.SetActive(false);
        }
        else
        {
            Debug.LogWarning("SwimTest: Scene object not found in this scene.");
        }

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

        // making sure no old Metronome music is playing at the start of the experiment (Tolga)
        Metronome oldMetronome = FindObjectOfType<Metronome>();
        if (oldMetronome != null)
        {
            oldMetronome.StopMetronome();
        }

        // in case we have an audiosource on this gameobject (Tolga)
        AudioSource source = GetComponent<AudioSource>();
        if (source != null) source.Stop();


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
       
        if (Input.GetKeyDown(KeyCode.Space) && (isTraining || isTestRunning)) touchpadPressed = true;

        //if (expManager == null) // if expManager is null, then the scene was started outside of the experiment. Allow to run training or test by button press

        
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

            StartCoroutine(ResampleAndReproject());

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
        //exit if scene is not assigned or dotManager or distortions are missing (Tolga)
        if (scene == null) return;

        // when wall should be hidden, then do not adjust the scene scale, to avoid any weird distortions of the training GUI (Tolga)
        if (hideTrainingWall) return;

        //Only update scene scale if dotManager exists (Tolga)
        if (dotManager != null)
        {
            // adjust the scene scale for distortions, i.e. all objects are inversely scaled by the magnification factor. This works only if the camera is (roughtly) at the origin.
            // 1. adjust scene for randomDot magnification
            scene.transform.localScale = initTargetScale / dotManager.distortionParam.x * new Vector3(1f, 1f, 1f);
            // adjust training gameObject for distortion magnification
            if (trainingObj != null && camDistortions != null)
            {
                trainingObj.transform.localScale = camDistortions.magn * new Vector3(1f, 1f, 1f);
                Transform wall = trainingObj.transform.Find("Wall"); //.transform.localScale = 1 / camDistortions.magn * new Vector3(1f, 1f, 1f);
                if (wall != null) //(Tolga)
                {
                    wall.localScale = 1 / camDistortions.magn * new Vector3(1f, 1f, 1f);
                }
            }
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

        // Reposition training objects to correct distance from player (without activating dots)
        if (Camera.main != null)
        {
            Vector3 headPosition = Camera.main.transform.position;
            if (scene != null) scene.transform.position = headPosition;
            trainingObj.transform.position = headPosition;

            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0;
            initHeadForward = headForward.normalized;

            if (scene != null)
            {
                scene.transform.Find("Wall").transform.localPosition = new Vector3(0f, 0f, wallDistance);
                scene.transform.rotation = Quaternion.LookRotation(headForward);
                scene.SetActive(false); // keep scene hidden
            }

            trainingObj.transform.Find("Wall").transform.localPosition = new Vector3(0f, 0f, wallDistance);
            trainingObj.transform.rotation = Quaternion.LookRotation(headForward);
        }

        dotManager.active = false; // make sure dots are still off after repositioning

        // Save original target scales and colors for reset
        Transform trRef = trainingObj.transform.Find("Wall/UI/TargetRight");
        Transform tlRef = trainingObj.transform.Find("Wall/UI/TargetLeft");
        Vector3 savedTargetScale = tlRef != null ? tlRef.localScale : Vector3.one * 0.1f;
        Color savedTargetColor = tlRef != null ? tlRef.GetComponent<Renderer>().material.color : Color.red;

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

            // Hide targets during instruction text display so they don't overlap
            Transform hideTargetL = trainingObj.transform.Find("Wall/UI/TargetLeft");
            Transform hideTargetR = trainingObj.transform.Find("Wall/UI/TargetRight");
            Transform hideFixation = trainingObj.transform.Find("Wall/UI/FixationTarget");
            Transform hideBar = trainingObj.transform.Find("Wall/UI/Bar");
            Transform hideHead = trainingObj.transform.Find("Wall/UI/HeadIndicator");
            if (hideTargetL != null) hideTargetL.gameObject.SetActive(false);
            if (hideTargetR != null) hideTargetR.gameObject.SetActive(false);
            if (hideFixation != null) hideFixation.gameObject.SetActive(false);
            if (hideBar != null) hideBar.gameObject.SetActive(false);
            if (hideHead != null) hideHead.gameObject.SetActive(false);

            // Play training audio if available for this step
            if (trainingAudioClips != null && step < trainingAudioClips.Count && trainingAudioClips[step] != null)
            {
                beepSource.pitch = 1f;
                beepSource.PlayOneShot(trainingAudioClips[step]);
            }

            fireLeftPressed = false;
            fireRightPressed = false;
            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.RightArrow) ||
                fireLeftPressed ||
                fireRightPressed ||
                AnyContinueInput()

            );
            beepSource.Stop(); // stop audio if still playing when participant continues
            
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                step = step - 2; // repeat two steps back (stable)
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || fireLeftPressed || fireRightPressed)
            {
                // repeat the previous step (unstable) - trigger also replays unstable
                step = step - 1;
            }

            // Show targets again before the exercise
            if (hideTargetL != null) hideTargetL.gameObject.SetActive(true);
            if (hideTargetR != null) hideTargetR.gameObject.SetActive(true);
            if (hideFixation != null) hideFixation.gameObject.SetActive(true);
            // Show Bar and HeadIndicator only for case 0 (they get removed in case 1)
            if (step <= 0)
            {
                if (hideBar != null) hideBar.gameObject.SetActive(true);
                if (hideHead != null) hideHead.gameObject.SetActive(true);
            }

            // Reset target scales and colors
            if (trRef != null)
            {
                trRef.localScale = savedTargetScale;
                trRef.GetComponent<Renderer>().material.color = savedTargetColor;
            }
            if (tlRef != null)
            {
                tlRef.localScale = savedTargetScale;
                tlRef.GetComponent<Renderer>().material.color = savedTargetColor;
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
                    camDistortions.active = true;
                    yield return StartCoroutine(TrainingHeadMovement());
                    camDistortions.magn = 1.0f;
                    camDistortions.active = false;

                    // else: space pressed -> continue to next step
                    break;
                case 4: // replay stable or unstable on request
                    // do nothing here, the for-loop handles the input
                    // but we need to handle trigger/touchpad for replay
                    break;
                case 5: // Practice with different distortion trials
                        // for loop over 6 example trials, continue until all are correct
                    exampleMags = new List<float> { 1.3f, 1.0f, 0.9f };//, 1.0f, 1.2f, 1.3f };
                    while (exampleMags.Count > 0)
                    {
                        float mag = exampleMags[0];
                        exampleMags.RemoveAt(0);
                        camDistortions.magn = mag;
                        camDistortions.active = (mag != 1.0f); // only activate distortions if mag is not 1, to avoid any weird visuals during the "stable" trials (Tolga)
                        yield return StartCoroutine(TrainingHeadMovement(4));
                        camDistortions.magn = 1.0f;
                        camDistortions.active = false;

                        instructionText.text = "Trigger = unstable, Trackpad = stable";

                        fireLeftPressed = false;
                        fireRightPressed = false;
                        touchpadPressed = false;
                        int ans = -1;
                        yield return new WaitUntil(() =>
                        {
                            if (Input.GetKeyDown(KeyCode.LeftArrow) || fireLeftPressed || fireRightPressed)
                            {
                                ans = 0;
                                return true;
                            }
                            if (Input.GetKeyDown(KeyCode.RightArrow) || touchpadPressed)
                            {
                                ans = 1;
                                return true;
                            }
                            return false;
                        });
                        if (ans == 0) Flash(colorLeft, flashDuration);
                        else if (ans == 1) Flash(colorRight, flashDuration);

                        // 1. it was a stable trial
                        if (mag == 1.0f)
                        {
                            if (ans == 1)
                            {
                                // correct
                                instructionText.text = "Correct! This was a stable trial.\nPress trackpad to continue.";
                            }
                            else
                            {
                                instructionText.text = "Wrong! This was a stable trial.\nPress trackpad to continue.";
                                // add the current mag to the end of the exampleMags list, so that it will be repeated later
                                exampleMags.Add(mag);
                            }
                        }
                        // 2. it was an unstable trial
                        if (mag != 1.0f)
                        {
                            if (ans == 1) // wrong
                            {
                                instructionText.text = "Wrong! This was an unstable trial.\nPress trackpad to continue.";
                                exampleMags.Add(mag);
                            }
                            else // correct
                            {
                                instructionText.text = "Correct! This was an unstable trial.\nPress trackpad to continue.";
                            }
                        }
                        // Reset input state and wait for explicit trackpad press
                        touchpadPressed = false;
                        fireLeftPressed = false;
                        fireRightPressed = false;
                        yield return new WaitForSeconds(0.3f); // brief debounce
                        yield return new WaitUntil(() => AnyContinueInput());
                    }
                    break;

                case 6: // Practice with random dots
                    hideTrainingWall = true;

                    // Deactivating the plane (brick wall)
                    Transform pTrans = trainingObj.transform.Find("Wall/Plane");
                    if (pTrans != null) pTrans.gameObject.SetActive(false);

                    // Show all UI elements for warm-up (FixationTarget, TargetLeft, TargetRight)
                    Transform fTrans = trainingObj.transform.Find("Wall/UI/FixationTarget");
                    if (fTrans != null) fTrans.gameObject.SetActive(true);
                    if (hideTargetL != null) hideTargetL.gameObject.SetActive(true);
                    if (hideTargetR != null) hideTargetR.gameObject.SetActive(true);

                    // Reset target scales and colors
                    if (trRef != null)
                    {
                        trRef.localScale = savedTargetScale;
                        trRef.GetComponent<Renderer>().material.color = savedTargetColor;
                    }
                    if (tlRef != null)
                    {
                        tlRef.localScale = savedTargetScale;
                        tlRef.GetComponent<Renderer>().material.color = savedTargetColor;
                    }

                    // Show dots but WITHOUT DotManager fixation target during warm-up
                    // (GUI fixation target is already visible)
                    dotManager.showFixationTarget = false;
                    yield return StartCoroutine(ResampleAndReproject());

                    // Warm-up: practice head movements with dots (full GUI visible)
                    yield return StartCoroutine(TrainingHeadMovement(4));

                    // Hide dots during instruction
                    dotManager.active = false;

                    // Instruction: now remove visualization, only fixation target remains
                    instructionText.text = "Good! Now we remove the visualization.\nOnly the fixation target remains.\n\nYou will hear a beep at each head turn.\nAfter 4 turns, return to center and decide:\n\nTrigger = unstable, Trackpad = stable\n\n<b>Press trackpad to continue.<b>";

                    // Play audio for this instruction
                    if (dotTransitionClip != null)
                    {
                        beepSource.pitch = 1f;
                        beepSource.PlayOneShot(dotTransitionClip);
                    }
                    // Hide ALL training UI elements for this instruction and the following trials
                    if (hideTargetL != null) hideTargetL.gameObject.SetActive(false);
                    if (hideTargetR != null) hideTargetR.gameObject.SetActive(false);
                    if (hideFixation != null) hideFixation.gameObject.SetActive(false);
                    if (hideBar != null) hideBar.gameObject.SetActive(false);
                    if (hideHead != null) hideHead.gameObject.SetActive(false);

                    touchpadPressed = false;
                    yield return new WaitUntil(() => AnyContinueInput());
                    beepSource.Stop(); // stop audio if still playing when participant continues

                    // From now on: only dots + DotManager fixation target visible, no training UI
                    dotManager.showFixationTarget = true;

                    // Start example trials with dots
                    exampleMags = new List<float> { 1.0f, 1.2f, 1.0f };
                    while (exampleMags.Count > 0)
                    {
                        float mag = exampleMags[0];
                        exampleMags.RemoveAt(0);
                        dotManager.distortionParam.x = mag;
                        instructionText.text = ""; // clear instruction text before showing dots
                        yield return StartCoroutine(ResampleAndReproject());
                        yield return StartCoroutine(HeadMovement());
                        dotManager.distortionParam.x = 1.0f;

                        // remove dots while answering
                        dotManager.active = false;

                        instructionText.text = "Trigger = unstable, Trackpad = stable";

                        fireLeftPressed = false;
                        fireRightPressed = false;
                        touchpadPressed = false;
                        int ans = -1;
                        yield return new WaitUntil(() =>
                        {
                            if (Input.GetKeyDown(KeyCode.LeftArrow) || fireLeftPressed || fireRightPressed)
                            {
                                ans = 0;
                                return true;
                            }
                            if (Input.GetKeyDown(KeyCode.RightArrow) || touchpadPressed)
                            {
                                ans = 1;
                                return true;
                            }
                            return false;
                        });
                        if (ans == 0) Flash(colorLeft, flashDuration);
                        else if (ans == 1) Flash(colorRight, flashDuration);

                        // 1. it was a stable trial
                        if (mag == 1.0f)
                        {
                            if (ans == 1)
                            {
                                instructionText.text = "Correct! This was a stable trial.\nPress trackpad to continue.";
                            }
                            else
                            {
                                instructionText.text = "Wrong! This was a stable trial.\nPress trackpad to continue.";
                                exampleMags.Add(mag);
                            }
                        }
                        // 2. it was an unstable trial
                        if (mag != 1.0f)
                        {
                            if (ans == 1) // wrong
                            {
                                instructionText.text = "Wrong! This was an unstable trial.\nPress trackpad to continue.";
                                exampleMags.Add(mag);
                            }
                            else // correct
                            {
                                instructionText.text = "Correct! This was an unstable trial.\nPress trackpad to continue.";
                            }
                        }
                        // Reset input state and wait for explicit trackpad press
                        touchpadPressed = false;
                        fireLeftPressed = false;
                        fireRightPressed = false;
                        yield return new WaitForSeconds(0.3f); // brief debounce
                        yield return new WaitUntil(() => AnyContinueInput());
                    }
                    break;

            }
            touchpadPressed = false;
        }

        //  training is completed 
        instructionText.text = "Great job! You have completed the head movement training.\nPress trackpad to start the test.";
        yield return new WaitUntil(() => AnyContinueInput());
        isTraining = false; // set training flag to false

        // turning off the scene and the training GUI after training is completed, 
        // to avoid any weird visuals during the transition to the test phase (Tolga)
        if (scene != null) scene.SetActive(false);
        if (dotManager != null) dotManager.active = false;       

        Debug.Log("Headmovement training completed.");
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
        // Feedback-Beep turning off when metronome is active (Tolga)
        OnTurnHead -= PlayBeep;

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

        while (goodTrial < minGoodTrials) // continue until enough good trials or the experimenter wants to skip (Tolga)
        {
            float moveStartTime = Time.time; // save the time when the head movement was detected, to check the timing of the next movement (Tolga)

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
            if (Time.time - moveStartTime > 1.5f) // if the participant took too long to move their head, then consider it as a wrong trial and reset the good trial counter (Tolga)
            {
                goodTrial = 0;
            }
            else if ((Time.time - metronome.GetLastBeatTime() < timingThreshold) || (metronome.GetNextBeatTime() - Time.time < timingThreshold))
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

        // Feedback-Beep turning on again after metronome is stopped (Tolga)
        OnTurnHead += PlayBeep;
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




        while (goodTrial < minGoodTrials) // continue until enough good trials or the experimenter wants to skip (Tolga)
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
        isTestRunning = true;
        // brickwall is not needed during the test, 
        // so we can disable it to avoid any weird visuals with the random dots (Tolga)
        if (trainingObj != null)
        {
            Transform wall = trainingObj.transform.Find("Wall/Plane");
            if (wall != null) wall.gameObject.SetActive(false);

            // Hide all training GUI elements during the actual test
            Transform ui = trainingObj.transform.Find("Wall/UI");
            if (ui != null) ui.gameObject.SetActive(false);
        }
        // Hide the TrainingText during the actual test
        GameObject trainingTextObj = GameObject.Find("TrainingText");
        if (trainingTextObj != null)
        {
            trainingTextObj.GetComponent<TMPro.TMP_Text>().text = "";
        }

        // Position scene and training objects correctly, but don't activate scene yet (Tolga)
        // (ResampleAndReproject will activate the scene when needed)
        
        // Make sure fixation target dot is enabled
        dotManager.showFixationTarget = true;

        if (Camera.main != null && scene != null)
        {
            Vector3 headPosition = Camera.main.transform.position;
            scene.transform.position = headPosition;
            trainingObj.transform.position = headPosition;

            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height
            initHeadForward = headForward.normalized;

            scene.transform.Find("Wall").transform.localPosition = new Vector3(0f, 0f, wallDistance);
            scene.transform.rotation = Quaternion.LookRotation(headForward);

            trainingObj.transform.Find("Wall").transform.localPosition = new Vector3(0f, 0f, wallDistance);
            trainingObj.transform.rotation = Quaternion.LookRotation(headForward);
        }
        
        // Instruction text instead of sample text (Tolga)
        DisplayInformation.UpdateInstructionText("Phase 1: Assess Motion Stability");

        if (nTrialsBlock == -1)
        {
            nTrialsBlock = aftereffectData.nTrials;
        }

        Debug.Log("Starting aftereffect test.");
        // wait for startWaitTime
        yield return new WaitForSeconds(2f);

        // beep to indicate the start of the test (different pitch than the feedback beep during training, to avoid confusion (Tolga))
        PlayBeep(0.6f);
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

            // play audio instruction reminder after the first trial of each block
            if (trial == 0 && instructionReminderClip != null)
            {
                beepSource.pitch = 1f; // reset pitch to normal for voice instruction
                beepSource.PlayOneShot(instructionReminderClip);
            }

            fireLeftPressed = false;
            fireRightPressed = false;
            touchpadPressed = false;
            // wait for participant answer: trigger for unstable, touchpad for stable
            // (participant can answer while audio is still playing)
            yield return new WaitUntil(() =>
                Input.GetKeyDown(KeyCode.LeftArrow) ||
                Input.GetKeyDown(KeyCode.RightArrow) ||
                fireLeftPressed ||
                fireRightPressed ||
                touchpadPressed);

            // stop reminder audio if still playing
            beepSource.Stop();

            // you can check which key was pressed or OnFire was called
            if (Input.GetKeyDown(KeyCode.LeftArrow) || fireLeftPressed || fireRightPressed)
                    {
                        ans = 0; // unstable (trigger)
                        Flash(colorLeft, flashDuration);
                    }
                    else if (Input.GetKeyDown(KeyCode.RightArrow) || touchpadPressed)
                    {
                        ans = 1; // stable (touchpad)
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

        // brief pause before automatic scene switch
        yield return new WaitForSeconds(1f);

        Debug.Log("Aftereffect test completed.");
        isTestRunning = false;
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
        PlayBeep(1f);
    }
    void PlayBeep(float pitch)
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
    // newly added for consistent control (Tolga)
    private bool AnyContinueInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) || touchpadPressed)
        {
            touchpadPressed = false; // reset the touchpad pressed state
            return true;
        }
        return false;
    }
}