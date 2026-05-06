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
        public float[] magnificationTrial; 
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

    [Header("Instruction Audio")]
    public AudioClip startInstructionAudio;
    public AudioClip adaptationInstructionAudio;
    public AudioClip completedInstructionAudio;
    private AudioSource instructionAudioSource;
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
        adaptationPhaseData.distorted = false;
        adaptationPhaseData.inAdaptationPhase = false;
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

        // Setup audio source for instruction voiceover
        instructionAudioSource = gameObject.AddComponent<AudioSource>();
        instructionAudioSource.playOnAwake = false;

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

        // after training, start the real experiment (Tolga)
        Debug.Log("Training completed. Starting experiment...");
        yield return StartCoroutine(RunExperiment());
    }

    private IEnumerator BalloonGameTraining()
    {
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName));
        AdaptationTask testManager = GetSceneTestManager<AdaptationTask>();
        if (testManager == null) yield break;
        yield return StartCoroutine(testManager.RunTraining());
    }

    private IEnumerator HeadMovementTraining()
    {
        yield return StartCoroutine(SwitchScene(aftereffectSettings.sceneName));
        SwimTest testManager = GetSceneTestManager<SwimTest>();
        if (testManager == null) yield break;
        yield return StartCoroutine(testManager.RunTraining());
    }

    private IEnumerator RunExperiment()
    {
        Debug.Log("Starting experiment...");
        isRunning = true;

        // Reset button state to prevent training input from carrying over
        buttonPressed = false;
        yield return new WaitForSeconds(0.5f);
        buttonPressed = false;

        // Switch to adaptation scene so InstructionText is available
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName));

        string startExpTextFullPath = Path.Combine(Application.dataPath, startExpTextPath);
        string startExperimentText = DisplayInformation.ReadText(startExpTextFullPath);
        DisplayInformation.UpdateInstructionText(startExperimentText);
        if (startInstructionAudio != null)
        {
            instructionAudioSource.PlayOneShot(startInstructionAudio);
        }
        buttonPressed = false;
        yield return new WaitUntil(() => buttonPressed);
        instructionAudioSource.Stop();
        DisplayInformation.UpdateInstructionText("hide");

        ////////////////////
        // Baseline phase///
        ////////////////////
        // start eye tracking measurement for the baseline phase
        eyeTracker?.StartRecording("preBaseline");
        yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.preBaselineDuration, 0)); // adaptation phase without distortions
        eyeTracker?.StopRecording();

        // //  SWIM EFFECT SCENE // //
        // switch to swim scene and run topupFrequency trials without distortions as baseline measurement for the swim effect
        eyeTracker?.StartRecording("baseline");
        int roundCounter = 0;
        while (aftereffectData.currentTrial < aftereffectData.nTrials) // repeat until all trials are done
        {
            RecenterHead recenter = GetSceneTestManager<RecenterHead>();
            if (recenter == null) yield break;
            yield return recenter.RunRecenter();
            eyeTracker?.WriteMessage("StartSwimBlock_baseline_round" + roundCounter);
            yield return StartCoroutine(SwimTestPhase(aftereffectSettings.topupFrequency));
            eyeTracker?.WriteMessage("StopSwimBlock_baseline_round" + roundCounter);
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break;
            eyeTracker?.WriteMessage("StartTopUp_baseline_round" + roundCounter);
            yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.topUpDuration, 0));
            eyeTracker?.WriteMessage("StopTopUp_baseline_round" + roundCounter);
            roundCounter++;
        }
        eyeTracker?.StopRecording();

        //////////////////////
        // Adaptation phase //
        //////////////////////

        // Switch to adaptation scene first, so InstructionText is available
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName));

        string adaptTextFullPath = Path.Combine(Application.dataPath, adaptTextPath);
        string adaptationText = DisplayInformation.ReadText(adaptTextFullPath);
        DisplayInformation.UpdateInstructionText(adaptationText);
        if (adaptationInstructionAudio != null)
        {
            instructionAudioSource.PlayOneShot(adaptationInstructionAudio);
        }
        buttonPressed = false;
        yield return new WaitUntil(() => buttonPressed);
        instructionAudioSource.Stop();
        DisplayInformation.UpdateInstructionText("hide");

        // turn distortions on
        adaptationPhaseData.inAdaptationPhase = true;
        adaptationPhaseData.distorted = true;
        distortions.magn = adaptationMagnification;
        distortions.radial = adaptationRadial;
        distortions.active = true;
        eyeTracker?.StartRecording("adaptation");
        yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.adaptationDuration, 0)); // adaptation phase with distortions
        eyeTracker?.StopRecording();

        ///////////////////////
        // Aftereffect phase //
        ///////////////////////

        // create new trial parameters
        aftereffectData = new AftereffectData(aftereffectSettings);

        eyeTracker?.StartRecording("aftereffect");
        roundCounter = 0;
        while (aftereffectData.currentTrial < aftereffectData.nTrials) // repeat until all trials are done
        {
            RecenterHead recenter = GetSceneTestManager<RecenterHead>();
            if (recenter == null) yield break;
            yield return recenter.RunRecenter();
            adaptationPhaseData.inAdaptationPhase = false;
            distortions.active = false;
            eyeTracker?.WriteMessage("StartSwimBlock_aftereffect_round" + roundCounter);
            yield return StartCoroutine(SwimTestPhase(aftereffectSettings.topupFrequency));
            eyeTracker?.WriteMessage("StopSwimBlock_aftereffect_round" + roundCounter);
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break;
            // return to adaptation scene for top-up with distortions
            distortions.active = true;
            eyeTracker?.WriteMessage("StartTopUp_aftereffect_round" + roundCounter);
            yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.topUpDuration, 0));
            eyeTracker?.WriteMessage("StopTopUp_aftereffect_round" + roundCounter);
            roundCounter++;
        }
        eyeTracker?.StopRecording();
        Debug.Log("Experiment completed.");
        distortions.active = false;
        isRunning = false;

        // Switch to adaptation scene to show completion text
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName));
        DisplayInformation.UpdateInstructionText("Experiment completed!\nThank you for participating.\n\n<b>Press trackpad to exit.</b>");
        if (completedInstructionAudio != null)
        {
            instructionAudioSource.PlayOneShot(completedInstructionAudio);
        }
        buttonPressed = false;
        yield return new WaitUntil(() => buttonPressed);
        instructionAudioSource.Stop();
        DisplayInformation.UpdateInstructionText("hide");

        yield return new WaitForSeconds(1);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // stop play mode in the editor
#else
        Application.Quit(); // quit the application
#endif
    }

    private IEnumerator AdaptationPhase(float adaptationDuration, int roundCounter)
    {
        yield return StartCoroutine(SwitchScene(adaptationPhaseSettings.sceneName)); // switch to adaptation scene

        // making sure that de random Dots are not active during the adaptation phase, 
        // because we use the balloon game for adaptation (Tolga)
        DotManager dotManager = Camera.main.GetComponent<DotManager>();
        if (dotManager != null)
        {
            dotManager.active = false;
        }

        // in case scene object is active (Tolga)
        GameObject dotScene = GameObject.Find("Scene");
        if (dotScene != null)
        {
            dotScene.SetActive(false);
        }

        // turn distortions on if adaptationPhaseData shows so
        distortions.active = adaptationPhaseData.distorted;

        AdaptationTask testManager = GetSceneTestManager<AdaptationTask>();
        if (testManager == null) yield break;

        testManager.roundCounter = roundCounter;
        testManager.canPlayAgain = true;
        // when duration shorter than roundtime, only play one round with the shortened time, otherwise calculate how many rounds fit into the adaptation duration and set that in the test manager
        float originalRoundTime = testManager.roundTime;
        if (adaptationDuration < testManager.roundTime)
        {
            testManager.totalRounds = 1;
            testManager.roundTime = adaptationDuration;
        }
        else
        {
            testManager.totalRounds = Mathf.FloorToInt(adaptationDuration / testManager.roundTime);
        }
        Debug.Log("Balloon phase: " + adaptationDuration + "s / " + testManager.roundTime + "s = " + testManager.totalRounds + " rounds");

        testManager.isDone = false;
        testManager.StartGame();
        yield return new WaitUntil(() => testManager.isDone);

        // original round time back for the next time the adaptation phase is entered 
        testManager.roundTime = originalRoundTime;
    }

    private IEnumerator SwimTestPhase(int ntrials = -1)
    {
        yield return StartCoroutine(SwitchScene(aftereffectSettings.sceneName));

        // set distortions off, always, because we use the random dot simulation
        distortions.active = false;

        SwimTest testManager = GetSceneTestManager<SwimTest>();
        if (testManager == null) yield break;

        if (ntrials == -1)
            yield return StartCoroutine(testManager.RunTest());
        else
            yield return StartCoroutine(testManager.RunTest(ntrials));
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

    // Helper to find and retrieve a component from the SceneTestManager-tagged GameObject.
    // Logs errors if the object or component are missing and returns null.
    private T GetSceneTestManager<T>() where T : Component
    {
        GameObject obj = GameObject.FindWithTag("SceneTestManager");
        if (obj == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            return null;
        }
        T component = obj.GetComponent<T>();
        if (component == null)
        {
            Debug.LogError($"{typeof(T).Name} script not found on the SceneTestManager object!");
        }
        return component;
    }
}