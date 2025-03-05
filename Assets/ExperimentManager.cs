using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExperimentManager : MonoBehaviour
{
    public static ExperimentManager instance;
    public int subjectID = 0;
    public int nTrials = 10;
    public float adaptationDuration = 5f;
    public string adaptationScene = "AdaptationScene";
    public string testScene = "AftereffectScene";
    
    private int currentTrial = 0;
    private string resultsPath;


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
        // output = "./measurements/subjectID/results.csv"
        resultsPath = Path.Combine(Application.dataPath, "measurements", subjectID.ToString(), "results.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(resultsPath));
        StartCoroutine(RunExperiment());
    }

    private IEnumerator RunExperiment()
    {
        Debug.Log("Starting experiment...");
        while (currentTrial < nTrials)
        {
            yield return StartCoroutine(AdaptationPhase());
            yield return StartCoroutine(TestPhase());
            currentTrial++;
        }
        Debug.Log("Experiment completed.");
    }

    private IEnumerator AdaptationPhase()
    {
        Debug.Log("Starting adaptation phase...");
        SceneManager.LoadScene(adaptationScene);
        yield return new WaitForSeconds(adaptationDuration);
    }

    private IEnumerator TestPhase()
    {
        Debug.Log("Switching to test scene...");
        SceneManager.LoadScene(testScene);
        yield return new WaitForSeconds(1f); // Small delay to ensure scene loads
        SaveResults(currentTrial);
    }

    private void SaveResults(int trialNumber)
    {
        string data = "Trial " + trialNumber;
        File.AppendAllText(resultsPath, data);
        Debug.Log("Results saved for trial " + trialNumber);
    }
}
