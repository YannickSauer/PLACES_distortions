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
    private TMP_Text instructionText;
    private AdaptationTask adaptationTask;

    void Awake()
    {
        adaptationTask = this.GetComponent<AdaptationTask>();   
    }

    void OnEnable()
    {
        adaptationTask.OnRoundOver += UpdateHighscoreText;
        adaptationTask.OnRoundStart += HideHighscoreText;
        adaptationTask.OnNextTrainingStep += UpdateInstructionText;
    }

    void OnDisable()
    {
        adaptationTask.OnRoundOver -= UpdateHighscoreText;
        adaptationTask.OnRoundStart -= HideHighscoreText;
        adaptationTask.OnNextTrainingStep -= UpdateInstructionText;
    }

    // Start is called before the first frame update
    void Start()
    {
        // Get text objects
        scoreText = GameObject.Find("ScoreText").GetComponent<TMP_Text>();
        timerText = GameObject.Find("TimerText").GetComponent<TMP_Text>();
        highScoreText = GameObject.Find("RoundText").GetComponent<TMP_Text>();
        instructionText = GameObject.Find("InstructionText").GetComponent<TMP_Text>();

        instructionText.text = "";
        highScoreText.text = "";
        UpdateGameUITexts();
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

    public void UpdateInstructionText(string newText)
    {
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
            }
        }

    }
    private void UpdateGameUITexts()
    {
        if (scoreText != null)
        {
            scoreText.text = "Round: " + adaptationTask.roundCounter + "/" + adaptationTask.totalRounds + "\nScore: " + adaptationTask.score;
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
}
