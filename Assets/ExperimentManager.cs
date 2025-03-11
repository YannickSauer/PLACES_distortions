using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager instance;
    public int subjectID = 0;
    public int baselineTrials = 4;
    public int aftereffectTestTrials = 4;
    public int adaptationTrials = 4;
    public float preBaseLineDuration = 5f;
    public float adaptationDuration = 120f; // in seconds; duration for each trial
    public string adaptationScene = "AdaptationScene";
    public string testScene = "AftereffectScene";
    
    private int currentTrial = 0;
    private string resultsPath;
    private bool isRunning = false;

    private Distortions distortions;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
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
        distortions.active = false;
        // output = "./measurements/subjectID/results.csv"
        resultsPath = Path.Combine(Application.dataPath, "measurements", subjectID.ToString(), "results.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(resultsPath));
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

        yield return StartCoroutine(AdaptationPhase(preBaseLineDuration)); // adaptation phase without distortions
        yield return StartCoroutine(TestPhase(baselineTrials,false)); // baseline trials with target
        yield return StartCoroutine(TestPhase(aftereffectTestTrials,true)); // baseline trials without target (VOR in the dark)
        // turn distortions on
        distortions.active = true;
        // repeated adaptation phase + test phase
        while (currentTrial < adaptationTrials)
        {        
            yield return StartCoroutine(AdaptationPhase(adaptationDuration));
            yield return StartCoroutine(TestPhase(aftereffectTestTrials,true)); 
            currentTrial++;
        }
        Debug.Log("Experiment completed.");
        distortions.active = false;
        isRunning = false;
    }

    private IEnumerator AdaptationPhase(float adaptationDuration)
    {
        yield return StartCoroutine(SwitchScene(adaptationScene));

        // TODO: do same stuff as in TestPhase() for scene loading and task starting
        yield return new WaitForSeconds(adaptationDuration);
    }

    private IEnumerator TestPhase(int nTrials, bool invisibleTarget)
    {
        yield return StartCoroutine(SwitchScene(testScene));
       
        // Find the GameObject that contains the script responsible for the coroutine
        GameObject testManagerObject = GameObject.FindWithTag("SceneTestManager");

        if (testManagerObject == null)
        {
            Debug.LogError("SceneTestManager tagged object not found!");
            yield break; // Stop execution if the object isn't found
        }

        AftereffectTest testManager = testManagerObject.GetComponent<AftereffectTest>();

        if (testManager == null)
        {
            Debug.LogError("AftereffectTest script not found on the object!");
            yield break;
        }

        // Start the test coroutine and wait for it to finish
        yield return StartCoroutine(testManager.RunTest(nTrials, invisibleTarget));

        // Continue with the experiment
        SaveResults(currentTrial);
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