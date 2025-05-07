using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager Instance { get; private set; }
    public int subjectID = 0;
    public float adaptationDuration = 120f; // in seconds; duration for each trial
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
    }
    
    [Header("Experiment Settings")]
    
    public AdaptationPhaseSettings adaptationPhaseSettings;
    public AftereffectSettings aftereffectSettings;
    
    [Header("Live Data (Debugging)")]

    public AdaptationPhaseData adaptationPhaseData;

    public AftereffectData aftereffectData ; // stores trial-by-trial data for the aftereffect phase

    private string resultsPath;
    private bool isRunning = false;
    private Distortions distortions;

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
        resultsPath = Path.Combine(Application.dataPath, "measurements", subjectID.ToString(), "results.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(resultsPath));

        // fill the trial variables
        aftereffectData = GetAftereffectData();

        // set random dots to inactive
        Camera.main.GetComponent<DotManager>().active = false;
        
    }

    private AftereffectData GetAftereffectData()
    {
        aftereffectData = new AftereffectData();
        aftereffectData.currentTrial = 0;
        float[] magnificationTrial = ExperimentPreparation.FillWithSamples(aftereffectSettings.magnificationStimulusLevels,
                                                                           aftereffectSettings.samplingFrequency * aftereffectSettings.radialStimulisLevels.Length, 1);
        float[] radialTrial = ExperimentPreparation.FillWithSamples(aftereffectSettings.radialStimulisLevels, aftereffectSettings.samplingFrequency * aftereffectSettings.magnificationStimulusLevels.Length, 1);
        // randomly permute the trials
        ExperimentPreparation.RandPermute(magnificationTrial);
        ExperimentPreparation.RandPermute(radialTrial);
        aftereffectData.magnificationTrial = magnificationTrial;
        aftereffectData.radialTrial = radialTrial;
        aftereffectData.nTrials = magnificationTrial.Length;
        aftereffectData.answerTrial = new int[aftereffectData.nTrials];
        return aftereffectData;
    }

    private void Update()
    {
        if(!isRunning && Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(RunExperiment());
        }
    }

    private IEnumerator RunExperiment()
    {
        Debug.Log("Starting experiment...");
        isRunning = true;
        ////////////////////
        // Baseline phase///
        ////////////////////
        yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.preBaselineDuration)); // adaptation phase without distortions
        //yield return StartCoroutine(VORTestPhase(baselineTrials,false)); // baseline trials with target
        //yield return StartCoroutine(VORTestPhase(aftereffectTestTrials,true)); // baseline trials without target (VOR in the dark)
        
        // //  SWIM EFFECT SCENE // //
        // switch to sway scene and run topupFrequency trials
        while (aftereffectData.currentTrial < aftereffectData.nTrials) // repeat until all trials are done
        {
            yield return StartCoroutine(SwimTestPhase(aftereffectSettings.topupFrequency)); // do a few trials in the sway scene
            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break; // check if we are done with the trials
            yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.topUpDuration)); // adaptation phase for topUpDuration seconds
        }
        
        //////////////////////
        // Adaptation phase //
        //////////////////////
        
        // turn distortions on
        adaptationPhaseData.distorted = true;
        distortions.active = true;
        yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.adaptationDuration)); // adaptation phase with distortions
        distortions.active = false;

        ///////////////////////
        // Aftereffect phase //
        ///////////////////////
        
        // create new trial parameters
        aftereffectData = GetAftereffectData();


        while (aftereffectData.currentTrial < aftereffectData.nTrials) // repeat until all trials are done
        {   
            yield return StartCoroutine(SwimTestPhase(aftereffectSettings.topupFrequency)); // do a few trials in the sway scene

            if (aftereffectData.currentTrial >= aftereffectData.nTrials) break; // check if we are done with the trials
            // return to adaptation scene for top-up with distortions
            distortions.active = true;
            yield return StartCoroutine(AdaptationPhase(adaptationPhaseSettings.topUpDuration)); // adaptation phase for topUpDuration seconds
            distortions.active = false;
        }
        Debug.Log("Experiment completed.");
        distortions.active = false;
        isRunning = false;
    }

    private IEnumerator AdaptationPhase(float adaptationDuration)
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
            Debug.LogError("VORTest script not found on the object!");
            
        }
        else
        {
            testManager.duration = adaptationDuration;
        }

        // Start the test coroutine and wait for it to finish
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

        // Start the test coroutine and wait for it to finish
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

    private void SaveResults(int trialNumber)
    {
        string data = "Trial " + trialNumber;
        File.AppendAllText(resultsPath, data);
        Debug.Log("Results saved for trial " + trialNumber);
    }
}