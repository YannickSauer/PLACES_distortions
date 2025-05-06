using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class AdaptationTask : MonoBehaviour
{
    public GameObject balloonPrefab;
    public GameObject pointsPrefab;
    public GameObject explosionPrefab; 
    public int negativeScore = 5;
    public float highPointsThreshold = 0.2f; // threshold for high points
    public int lowPoints = 1;
    public int highPoints = 3;
    private int numberOfGroups;
    public int balloonsPerGroup = 3;
    public float groupRadius = 1f;
    public float initBalloonSize = 0.001f;
    public float minGrowSpeed = 0.001f;
    public float maxGrowSpeed = 0.003f;
    public float duration = 120f; // in seconds; duration of the test
    private float timer = 0f; // timer for the test
    public List<Vector3> balloonGroupPositions = new List<Vector3>();
    public List<Color> balloonGroupColors = new List<Color>();
    private List<Balloon> balloons = new List<Balloon>();
    private int score = 0;
    private TMP_Text scoreText;
    private TMP_Text timerText;

    void Start()
    {
        numberOfGroups = balloonGroupPositions.Count; // get number of groups from the list of defined group positions
        // check if enough colors are provided
        if (balloonGroupColors.Count < numberOfGroups)
        {
            Debug.LogError("Not enough colors provided for the balloon groups.");
            return;
        }
        SpawnAllBalloons();

        // start the timer
        timer = Time.time;
        // show score on the TextMeshPro object
        scoreText = GameObject.Find("ScoreText").GetComponent<TMP_Text>();
        timerText = GameObject.Find("TimerText").GetComponent<TMP_Text>();
        UpdateTexts();

        // set the min and max grow speed for all balloons
        Balloon.minGrowSpeed = minGrowSpeed;
        Balloon.maxGrowSpeed = maxGrowSpeed;
    }

    private void SpawnAllBalloons()
    {
        for (int groupId = 0; groupId < numberOfGroups; groupId++)
        {
            for (int i = 0; i < balloonsPerGroup; i++)
            {
               SpawnBalloon(groupId, i);
            }
        }
    }

    private void SpawnBalloon(int groupId, int balloonId)
    {
        Vector3 groupCenter = balloonGroupPositions[groupId];
        Vector3 offset = Random.insideUnitSphere * 0.5f;
        offset.y = 1 + Random.Range(-0.5f, 0.5f); // keep the y position within a certain range
        GameObject balloonObj = Instantiate(balloonPrefab, groupCenter + offset, Quaternion.identity);
        
        Balloon balloon = balloonObj.GetComponent<Balloon>();
        balloon.Initialize(groupId,balloonId,balloonGroupColors[groupId]);
        balloon.transform.localScale = new Vector3(initBalloonSize, initBalloonSize, initBalloonSize); // set initial size
        balloon.OnExplode += HandleBalloonExploded;
        balloon.OnPopped += HandleBalloonPopped;

        balloons.Add(balloon);
    }

    private void UpdateTexts()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
        

        if (timerText != null)
        {
            timerText.text = "Time: " + Mathf.RoundToInt(duration - (Time.time-timer)) + "s";
        }
        
    }

    void HandleBalloonExploded(Balloon b)
    { 
        score -= negativeScore;
        // Instantiate explosion effect at the balloon's position
        GameObject explosion = Instantiate(explosionPrefab, b.transform.position, Quaternion.identity);
        // set the color of the explosion to the color of the balloon
        ParticleSystem.MainModule main = explosion.GetComponent<ParticleSystem>().main;
        // play the particle system
        main.startColor = b.GetComponent<Renderer>().material.color;
        explosion.GetComponent<ParticleSystem>().Play();
        SpawnPoints(b.transform.position, -negativeScore);
        Debug.Log("Balloon exploded! Score: " + score);
        // start score animation

        // spawn a new balloon in the same group
        SpawnBalloon(b.groupId, b.balloonId);
    }

    void HandleBalloonPopped(Balloon b)
    {
        int points;
        if (b.transform.localScale.x < highPointsThreshold)
        {
            points = highPoints;
        }
        else 
        {
            points = lowPoints;
        }
        score += points;
        SpawnPoints(b.transform.position, points);
        Debug.Log("Balloon popped! Score: " + score);
        // spawn a new balloon in the same group
        SpawnBalloon(b.groupId, b.balloonId);

    }

    void SpawnPoints(Vector3 position, int points)
    {
        GameObject pointsObj = Instantiate(pointsPrefab, position + 0.2f * Vector3.up, Quaternion.identity);
        // get child object with text component
        TMP_Text pointsText = pointsObj.transform.GetChild(0).GetComponent<TMP_Text>();
        pointsText.text = "+" + points.ToString();
        // set text color depending on the points
        if (points > 0)
        {
            pointsText.color = Color.green;
        }
        else
        {
            pointsText.color = Color.red;
        }
    }

    // For testing: destroy balloon with mouse click (or raycast in VR)
    void Update()
    {
        if (Input.GetMouseButtonDown(0)) {
            Debug.Log(Input.mousePosition);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, 10f * ray.direction, Color.cyan);
            if (Physics.Raycast(ray, out RaycastHit hit)) {
                Debug.Log(hit.transform.gameObject.name);
                Balloon balloon = hit.collider.GetComponent<Balloon>();
                if (balloon != null) {
                    Debug.Log("Balloon Hit");
                    balloon.Pop(ray.direction);
                }
            }
        }

        UpdateTexts();
        // check if the time is up
        if (Time.time - timer >= duration)
        {
            Debug.Log("Time's up! Final score: " + score);
            // handle end of the game, e.g. show results or go to next scene
            // SceneManager.LoadScene("NextScene");
        }
        // extenstions:
        // 1. Add a timer to the game
        // 2. Increase the speed of the balloons over time
        // 3. Add a score multiplier for consecutive pops
        // 4. Add a sound effect for popping balloons
        // 5. Add a visual effect for popping balloons
        // 6. Add a countdown timer for the game
    }
}
