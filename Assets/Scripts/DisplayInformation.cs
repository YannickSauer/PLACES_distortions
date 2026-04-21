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
                // if at least one score in the list, show last round and best score
                if (adaptationTask.highScores.Count > 0)
                {
                    float lastScore = adaptationTask.highScores[adaptationTask.highScores.Count - 1];
                    float bestScore = Mathf.Max(adaptationTask.highScores.ToArray());

                    highScoreText.text = "Last Round: " + lastScore + "\nBest Score: " + bestScore + "\n\nPress Trackpad to continue.";
                }
                else
                {
                    highScoreText.text = "Destroy all green balloons!\nPress Trackpad to start.";
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


    //if needed we cen get back to this method with 5 best highscores
    private string GetHighScoreText()
    {
        int total = adaptationTask.highScores.Count;

        // If 5 or fewer rounds: simple single column (as before)
        if (total <= 5)
        {
            string text = "";
            for (int i = 0; i < total; i++)
            {
                text += (i + 1) + ": " + adaptationTask.highScores[i] + "\n";
            }
            return text;
        }

        // More than 5 rounds: split into two columns
        // Left column holds the first half, right column the second half
        int rowsPerColumn = Mathf.CeilToInt(total / 2f);
        string combined = "";

        for (int row = 0; row < rowsPerColumn; row++)
        {
            // Left column entry
            string leftEntry = (row + 1) + ": " + adaptationTask.highScores[row];

            // Pad the left entry to a fixed width so the right column lines up
            leftEntry = leftEntry.PadRight(12);

            // Right column entry (if it exists)
            int rightIndex = row + rowsPerColumn;
            string rightEntry = "";
            if (rightIndex < total)
            {
                rightEntry = (rightIndex + 1) + ": " + adaptationTask.highScores[rightIndex];
            }

            combined += leftEntry + rightEntry + "\n";
        }

        return combined;
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

                // Update size and position to match current text (same logic as ShowTrainingBackground)
                Bounds textBounds = instructionText.textBounds;
                float padding = 1f;
                bgObj.transform.localPosition = new Vector3(textBounds.center.x, textBounds.center.y, 0.01f);
                bgObj.transform.localScale = new Vector3(textBounds.size.x + padding, textBounds.size.y + padding, 1f);
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
