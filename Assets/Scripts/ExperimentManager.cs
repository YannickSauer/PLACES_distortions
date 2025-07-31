using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance { get; private set; }
    public int subjectID = 0;
    public float adaptationMagnification = 1.0f;
    public float adaptationRadial = 0.0f;

    [System.Serializable]
    public class AdaptationPhaseSettings
    {
        public float preBaselineDuration; // which levels of magnification to use for the adaptation phase
        public float adaptationDuration; // in seconds; duration for each trial
        public float topUpDuration; // in seconds; duration for each top-up-trial
        public string sceneName; // AdaptationScene
    }
    [System.Serializable]
    public class AdaptationPhaseData
    {
        public bool inAdaptationPhase;
        public float score; // current score of the adaptation phase
        public bool distorted; // in which experiment phase are we: distorted already or not
    }
    [System.Serializable]
    public class AftereffectSettings // subcategory of settings for the aftereffect phase
    {
        public float[] magnificationStimulusLevels; // which levels of magnification to use for the aftereffect phase
        public float[] radialStimulisLevels; // which levels of radial distortions to use for the aftereffect phase
        public int samplingFrequency; // how many trials for each stimulus level for the aftereffect phase
        public int topupFrequency; // after how many trials to repeat the adaptation phase
        public string sceneName; // RanDot
    }

    public class AftereffectData // stores trial-by-trial data for the aftereffect phase
    {
        public int currentTrial; // current trial number
        public int nTrials; // number of trials for the aftereffect phase
        public float[] magnificationTrial; // 
        public float[] radialTrial;
        public int[] answerTrial;

        // Default constructor
        public AftereffectData()
        {
            currentTrial = 0;
            nTrials = 0;
            magnificationTrial = new float[0];
            radialTrial = new float[0];
            answerTrial = new int[0];
        }

        // Constructor with settings
        public AftereffectData(AftereffectSettings settings)
        {
            currentTrial = 0;
            magnificationTrial = ExperimentPreparation.FillWithSamples(
                settings.magnificationStimulusLevels,
                settings.samplingFrequency * settings.radialStimulisLevels.Length, 1);
            radialTrial = ExperimentPreparation.FillWithSamples(
                settings.radialStimulisLevels,
                settings.samplingFrequency * settings.magnificationStimulusLevels.Length, 1);
            ExperimentPreparation.RandPermute(magnificationTrial);
            ExperimentPreparation.RandPermute(radialTrial);
            nTrials = magnificationTrial.Length;
            answerTrial = new int[nTrials];
        }
    }

    [Header("Experiment Settings")]
    public AdaptationPhaseSettings adaptationPhaseSettings;
    public AftereffectSettings aftereffectSettings;

    [Header("Live Data (Debugging)")]

    public AdaptationPhaseData adaptationPhaseData;

    public AftereffectData aftereffectData; // stores trial-by-trial data for the aftereffect phase

    public string outputDirectory = "./measurements/subjectID/";
    public string startExpTextPath = "./Instructions/StartExperiment.txt";
    public string adaptTextPath = "./Instructions/AdaptationPhase.txt";
    private bool isRunning = false;
    private bool buttonPressed = false; 
    private Distortions distortions;
    private EyeTrackingToolbox eyeTracker;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.Log("Singleton instance already existed.");
            Destroy(gameObject);
            return;
        }
    }
    void Start()
    {
        distortions = Camera.main.gameObject.GetComponent<Distortions>();
        distortions.magn = adaptationMagnification;
        distortions.radial = adaptationRadial;
        distortions.active = false;
        // output = "./measurements/subjectID/results.csv"
        string projectPath = Directory.GetParent(Application.dataPath).FullName;
        outputDirectory = Path.Combine(projectPath, "measurements", subjectID.ToString() + "_" + System.DateTime.Now.ToString("yyMMddHHmm"));

        // create the directory if it does not exist
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // fill the trial variables
        aftereffectData = new AftereffectData(aftereffectSettings);

        // set random dots to inactive
        Camera.main.GetComponent<DotManager>().active = false;

        // set the eye tracker
        eyeTracker = EyeTrackingToolbox.Instance;
        if (eyeTracker == null)
        {
            Debug.LogError("EyeTracker not found in the scene.");
        }
        else
        {
            eyeTracker.SetOutputFolder(outputDirectory);
        }

    }

    private void Update()
    {
        if (!isRunning && Input.GetKeyDown(KeyCode.T))
        {
            StartCoroutine(RunTraining());
        }
        if (!isRunning && Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(RunExperiment());
        }
    }

    private IEnumerator RunTraining()
    {
        Debug.Log("Starting training...");
        isRunning = true;

        // Start balloon game training
        yield return StartCoroutine(BalloonGameTraining());

        // Start head movement training
        yield return StartCoroutine(HeadMovementTraining());

        isRunning = false;
    }

    private IEnumerator BalloonGameTraining()
    {
        // Switch to adaptation scene
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName));

        // Find Test Manager 
        GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");
        if (testManagerObject == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            yield break; // Stop execution if the object isn't found
        }

        AdaptationTask testManager = testManagerObject.GetComponent<AdaptationTask>();

        yield return StartCoroutine(testManager.RunTraining());
    }

    private IEnumerator HeadMovementTraining()
    {
        // Switch to swim test scene
        yield return StartCoroutine(SwitchScene(aftereffectSettings.sceneName));

        // Find Test Manager 
        GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");
        if (testManagerObject == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            yield break; // Stop execution if the object isn't found
        }

        SwimTest testManager = testManagerObject.GetComponent<SwimTest>();

        yield return StartCoroutine(testManager.RunTraining());
    }

    private IEnumerator RunExperiment()
    {
        Debug.Log("Starting experiment...");
        isRunning = true;

        string startExpTextFullPath = Path.Combine(Application.dataPath, startExpTextPath);
        string startExperimentText = DisplayInformation.ReadText(startExpTextFullPath);
        DisplayInformation.UpdateInstructionText(startExperimentText);
        buttonPressed = false;
        yield return new WaitUntil(() => buttonPressed);
        DisplayInformation.UpdateInstructionText("hide");
        ////////////////////
        // Baseline phase///
        ////////////////////
        // start eye tracking measurement for the baseline phase
        eyeTracker?.StartRecording("preBaseline");
        yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.preBaselineDuration, 0)); // adaptation phase without distortions
        eyeTracker?.StopRecording();
        //yield return StartCoroutine(VORTestPhase(baselineTrials,false)); // baseline trials with target
        //yield return StartCoroutine(VORTestPhase(aftereffectTestTrials,true)); // baseline trials without target (VOR in the dark)

        // //  SWIM EFFECT SCENE // //
        // switch to sway scene and run topupFrequency trials
        eyeTracker?.StartRecording("baseline");
        int roundCounter = 0;
        while (aftereffectData.currentTrial < aftereffectData.nTrials) // repeat until all trials are done
        {
            // Find the GameObject that contains the script responsible for the coroutine
            GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");
            if (testManagerObject == null)
            {
                Debug.LogError("SceneTestManager tagged object not found!");
                yield break; // Stop execution if the object isn't found
            }
            yield return testManagerObject.GetComponent<RecenterHead>().RunRecenter();
            yield return StartCoroutine(SwimTestPhase(aftereffectSettings.topupFrequency)); // do a few trials in the sway scene
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break; // check if we are done with the trials
            yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.topUpDuration, roundCounter)); // adaptation phase for topUpDuration seconds
            roundCounter++;
        }
        eyeTracker?.StopRecording();

        //////////////////////
        // Adaptation phase //
        //////////////////////
        
        string adaptTextFullPath = Path.Combine(Application.dataPath, adaptTextPath);
        string adaptationText = DisplayInformation.ReadText(adaptTextFullPath);
        DisplayInformation.UpdateInstructionText(adaptationText);
        buttonPressed = false;
        yield return new WaitUntil(() => buttonPressed);
        DisplayInformation.UpdateInstructionText("hide");

        // turn distortions on
        adaptationPhaseData.inAdaptationPhase = true;
        adaptationPhaseData.distorted = true;
        distortions.active = true;
        eyeTracker?.StartRecording("adaptation");
        yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.adaptationDuration, 0)); // adaptation phase with distortions
        eyeTracker?.StopRecording();
        //distortions.active = false;

        ///////////////////////
        // Aftereffect phase //
        ///////////////////////

        // create new trial parameters
        aftereffectData = new AftereffectData(aftereffectSettings);

        eyeTracker?.StartRecording("aftereffect");
        roundCounter = 0;
        while (aftereffectData.currentTrial < aftereffectData.nTrials) // repeat until all trials are done
        {
            // Find the GameObject that contains the script responsible for the coroutine
            GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");
            if (testManagerObject == null)
            {
                Debug.LogError("SceneTestManager tagged object not found!");
                yield break; // Stop execution if the object isn't found
            }
            yield return testManagerObject.GetComponent<RecenterHead>().RunRecenter();
            adaptationPhaseData.inAdaptationPhase = false;
            distortions.active = false;
            yield return StartCoroutine(SwimTestPhase(aftereffectSettings.topupFrequency)); // do a few trials in the sway scene
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break; // check if we are done with the trials
            // return to adaptation scene for top-up with distortions
            distortions.active = true;
            yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.topUpDuration, roundCounter)); // adaptation phase for topUpDuration seconds
            roundCounter++;
        }
        eyeTracker?.StopRecording();
        Debug.Log("Experiment completed.");
        distortions.active = false;
        isRunning = false;

        yield return new WaitForSeconds(1);
        UnityEditor.EditorApplication.isPlaying = false;
    }

    private IEnumerator AdaptationPhase(float adaptationDuration, int roundCounter)
    {
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName)); // switch to adaptation scene

        // trun distortions on if adaptationPhaseData shows so
        if (adaptationPhaseData.distorted)
        {
            distortions.active = true;
        }
        else
        {
            distortions.active = false;
        }

        // Find the GameObject that contains the script responsible for the coroutine
        GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");

        if (testManagerObject == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            yield break; // Stop execution if the object isn't found
        }

        AdaptationTask testManager = testManagerObject.GetComponent<AdaptationTask>();

        if (testManager == null)
        {
            Debug.LogError("AdaptationTask script not found on the object!");

        }
        else
        {
            testManager.roundCounter = roundCounter;
            // Change round info according adaptation or top up phase
            if (adaptationPhaseData.inAdaptationPhase)
            {
                testManager.canPlayAgain = true;
                testManager.totalRounds = Mathf.FloorToInt(adaptationDuration / testManager.roundTime);
            }
            else
            {
                testManager.canPlayAgain = false;
                testManager.totalRounds = Mathf.FloorToInt(aftereffectData.nTrials / aftereffectSettings.topupFrequency);
                Debug.Log(aftereffectData.nTrials + " /" + aftereffectSettings.topupFrequency + " = " + testManager.totalRounds);
                testManager.roundTime = adaptationDuration;
            }
        }
        testManager.StartGame();
        yield return new WaitForSeconds(adaptationDuration);
    }

    private IEnumerator VORTestPhase(int nTrials, bool invisibleTarget)
    {
        yield return StartCoroutine(SwitchScene(aftereffectSettings.sceneName)); // TODO, this is a new scene

        // TODO: add distortions here, if needed

        // Find the GameObject that contains the script responsible for the coroutine
        GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");

        if (testManagerObject == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            yield break; // Stop execution if the object isn't found
        }

        VORTest testManager = testManagerObject.GetComponent<VORTest>();

        if (testManager == null)
        {
            Debug.LogError("VORTest script not found on the object!");
            yield break;
        }

        // Start the test coroutine and yield return it to wait for it to finish
        yield return StartCoroutine(testManager.RunTest(nTrials, invisibleTarget));
    }

    private IEnumerator SwimTestPhase(int ntrials = -1)
    {
        yield return StartCoroutine(SwitchScene(aftereffectSettings.sceneName));

        // set distortions off, always, because we use the random dot simulation
        distortions.active = false;

        // Find the GameObject that contains the script responsible for the coroutine
        GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");

        if (testManagerObject == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            yield break; // Stop execution if the object isn't found
        }

        SwimTest testManager = testManagerObject.GetComponent<SwimTest>();

        if (testManager == null)
        {
            Debug.LogError("VORTest script not found on the object!");
            yield break;
        }

        // Start the test coroutine and wait for it to finish
        if (ntrials == -1) // if no number of trials is given, call without a number of trials
        {
            yield return StartCoroutine(testManager.RunTest());
        }
        else // if a number of trials is given, run that number of trials
        {
            yield return StartCoroutine(testManager.RunTest(ntrials));
        }
    }

    private IEnumerator SwitchScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
        // Wait until the scene is fully loaded before proceeding
        yield return new WaitUntil(() => SceneManager.GetActiveScene().name == sceneName);
    }
    private void OnTouchpad() // called by the touchpad of the controller
    {
        buttonPressed = true;
    }
}