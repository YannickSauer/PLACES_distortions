using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using System;

public class DisplayInformation : MonoBehaviour
{
    private TMP_Text scoreText;
    private TMP_Text timerText;
    private TMP_Text highScoreText;
    private static TMP_Text instructionText;
    private AdaptationTask adaptationTask;
    private SwimTest swimTest;

    void Awake()
    {
        adaptationTask = this.GetComponent<AdaptationTask>();
        swimTest = this.GetComponent<SwimTest>();
    }

    void OnEnable()
    {
        if (adaptationTask != null)
        {
            adaptationTask.OnRoundOver += UpdateHighscoreText;
            adaptationTask.OnRoundStart += HideHighscoreText;
            adaptationTask.OnNextTrainingStep += UpdateInstructionText;
        }
        if (swimTest != null)
        {
            swimTest.OnNextTrainingStep += UpdateInstructionText;
        }
    }

    void OnDisable()
    {
        if (adaptationTask != null)
        {
            adaptationTask.OnRoundOver -= UpdateHighscoreText;
            adaptationTask.OnRoundStart -= HideHighscoreText;
            adaptationTask.OnNextTrainingStep -= UpdateInstructionText;
        }
        if (swimTest != null)
        {
            swimTest.OnNextTrainingStep -= UpdateInstructionText;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        // Get text objects
        scoreText = GameObject.Find("ScoreText")?.GetComponent<TMP_Text>();
        timerText = GameObject.Find("TimerText")?.GetComponent<TMP_Text>();
        highScoreText = GameObject.Find("RoundText")?.GetComponent<TMP_Text>();
        instructionText = GameObject.Find("InstructionText")?.GetComponent<TMP_Text>();

        // Add a dark background behind instruction text
        if (instructionText != null)
        {
            // Remove old background if it exists
            Transform existingBg = instructionText.transform.Find("Background");
            if (existingBg != null)
            {
                Destroy(existingBg.gameObject);
            }
        }

        if (highScoreText != null) highScoreText.text = "";
        UpdateGameUITexts();
        UpdateInstructionText("hide");
    }

    // Update is called once per frame
    void Update()
    {
        UpdateGameUITexts();
    }

    public void UpdateHighscoreText()
    {

        if (highScoreText != null)
        {
            if (adaptationTask.HasNextRound)
            {
                // if at least one score in the list, show the high score
                if (adaptationTask.highScores.Count > 0)
                {
                    highScoreText.text = "High Score:\n" + GetHighScoreText() + "\nPress Trackpad to start next round.";
                }
                else
                {
                    highScoreText.text = "Destory all green balloons!\nPress Trackpad to start.";
                }
            }
            else
            {
                highScoreText.text = "";
            }
        }
        else
        {
            Debug.Log("No highScoreText found.");
        }
    }

    private void HideHighscoreText()
    {
        if (highScoreText != null)
        {
            highScoreText.text = "";
        }
    }

    private string GetHighScoreText()
    {
        string text = "";
        for (int i = 0; i < adaptationTask.highScores.Count; i++)
        {
            text += (i + 1) + ": " + adaptationTask.highScores[i] + "\n";
        }
        return text;
    }

    public static void UpdateInstructionText(string newText)
    {
        Debug.Log(newText);
        if (instructionText != null)
        {
            if (newText == "hide")
            {
                instructionText.gameObject.SetActive(false);
            }
            else
            {
                instructionText.gameObject.SetActive(true);
                instructionText.text = newText;

                // Force mesh update so we can get correct bounds
                instructionText.ForceMeshUpdate();

                // Create or update background
                Transform bgTransform = instructionText.transform.Find("Background");
                GameObject bgObj;
                if (bgTransform == null)
                {
                    bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    bgObj.name = "Background";
                    bgObj.transform.SetParent(instructionText.transform, false);
                    bgObj.transform.localRotation = Quaternion.identity;
                    Destroy(bgObj.GetComponent<Collider>());
                    Renderer bgRenderer = bgObj.GetComponent<Renderer>();
                    Material bgMat = new Material(Shader.Find("Sprites/Default"));
                    bgMat.color = new Color(0, 0, 0, 0.75f);
                    bgRenderer.material = bgMat;
                }
                else
                {
                    bgObj = bgTransform.gameObject;
                }

                // Update size and position to match current text
                Vector2 textSize = instructionText.GetRenderedValues(false);
                float padding = 1f;
                bgObj.transform.localPosition = new Vector3(-0.3f, -0.8f, 0.01f);
                bgObj.transform.localScale = new Vector3(textSize.x + padding, textSize.y + padding, 1f);
                bgObj.SetActive(true);
            }
        }
    }
    private void UpdateGameUITexts()
    {
        if (adaptationTask == null) return;

        if (scoreText != null)
        {
            if (adaptationTask.inRound)
            {
                scoreText.text = "Round: " + adaptationTask.roundCounter + "/" + adaptationTask.totalRounds + "\nScore: " + adaptationTask.score;
            }
            else
            {
                scoreText.text = "";
            }
        }

        if (timerText != null)
        {
            if (adaptationTask.inRound)
            {
                timerText.text = "Time left: " + Mathf.RoundToInt(adaptationTask.roundTime - (Time.time - adaptationTask.roundTimer)) + "s";
            }
            else
            {
                timerText.text = "";
            }
        }

    }

    // Show a dark transparent background behind the TrainingText in the SwimTest scene.
    // Call this whenever an instruction text is shown. Call HideTrainingBackground() when entering trials.
    public static void ShowTrainingBackground()
    {
        GameObject trainingTextObj = GameObject.Find("TrainingText");
        if (trainingTextObj == null) return;
        TMP_Text trainingText = trainingTextObj.GetComponent<TMP_Text>();
        if (trainingText == null) return;

        // Force mesh update so we can get correct bounds
        trainingText.ForceMeshUpdate();

        // Create or reuse the Background quad
        Transform bgTransform = trainingText.transform.Find("Background");
        GameObject bgObj;
        if (bgTransform == null)
        {
            bgObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgObj.name = "Background";
            bgObj.transform.SetParent(trainingText.transform, false);
            bgObj.transform.localRotation = Quaternion.identity;
            Destroy(bgObj.GetComponent<Collider>());
            Renderer bgRenderer = bgObj.GetComponent<Renderer>();
            Material bgMat = new Material(Shader.Find("Sprites/Default"));
            bgMat.color = new Color(0, 0, 0, 0.75f);
            bgRenderer.material = bgMat;
        }
        else
        {
            bgObj = bgTransform.gameObject;
        }

        // Size and position the background to cover the text (centered on actual text bounds)
        Bounds textBounds = trainingText.textBounds;
        float padding = 1f;
        bgObj.transform.localPosition = new Vector3(textBounds.center.x, textBounds.center.y, 0.01f);
        bgObj.transform.localScale = new Vector3(textBounds.size.x + padding, textBounds.size.y + padding, 1f);
        bgObj.SetActive(true);
    }

    // Hide the TrainingText background (e.g. when trials start, so dots/targets are not obscured).
    public static void HideTrainingBackground()
    {
        GameObject trainingTextObj = GameObject.Find("TrainingText");
        if (trainingTextObj == null) return;
        Transform bgTransform = trainingTextObj.transform.Find("Background");
        if (bgTransform != null)
        {
            bgTransform.gameObject.SetActive(false);
        }
    }

    public static string ReadText(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("No Textfile found.");
            return null;
        }
        string readText = File.ReadAllText(path);
        return readText;
    }
}
