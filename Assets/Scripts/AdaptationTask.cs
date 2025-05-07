using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class AdaptationTask : MonoBehaviour
{
    public GameObject balloonPrefab;
    public GameObject pointsPrefab;
    public GameObject explosionPrefab;
    [Header("Balloon Settings")]
    public int nTargetBalloons;
    public int nDistractorBalloons;
    public float explodeSize; // at which size should the balloon explode
    public int explodeScore = 5;
    public int wrongBaloonScore = 5;
    public float highPointsThreshold = 0.2f; // threshold for high points
    public int lowPoints = 1;
    public int highPoints = 3;
    
    public float initBalloonSize = 0.001f;
    public float minGrowSpeed = 0.001f;
    public float maxGrowSpeed = 0.003f;

    public List<Color> balloonColors = new List<Color>();



    [Header("Spawn Settings")]
    public float spawnDelay = 2f; // delay between destruction of balloon and spawning a new one
    private float timer = 0f; // timer for the current run
    public Vector3 spawnAreaLowerBounds;
    public Vector3 spawnAreaUpperBounds;
    public float playerDistance; // minimum distnace to player
    public float prevSpawnDistance; // minimum distance to previous spawn location

    [Header("Debug Data")]
    public float duration = 120f; // in seconds; duration of the test
    private List<Balloon> balloons = new List<Balloon>();
    private int score = 0;
    private TMP_Text scoreText;
    private TMP_Text timerText;

    void Start()
    {
        // check if enough colors are provided
        if (balloonColors.Count < 2)
        {
            Debug.LogError("At least two colors are necessary.");
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
        Balloon.maxSize = explodeSize;
    }

    private void SpawnAllBalloons()
    {
        // spawn target balloons
        for (int balloonId = 0; balloonId < nTargetBalloons; balloonId++)
        {
               // Start coroutine to spawn balloons with a delay
                float delay = Random.Range(0f, 1f); // random delay for each balloon
                StartCoroutine(SpawnBalloon(delay, 0, balloonId,Camera.main.transform.position));
        }
        
        // spawn distractor balloons
        for (int balloonId = 0; balloonId < nDistractorBalloons; balloonId++)
        {
            // Start coroutine to spawn balloons with a delay
            float delay = Random.Range(0f, 1f); // random delay for each balloon
            StartCoroutine(SpawnBalloon(delay, 1, balloonId, Camera.main.transform.position));
        }
    }

    private IEnumerator SpawnBalloon(float delay,int groupId, int balloonId, Vector3 prevLocation)
    {
        // wait for the delay before spawning the balloon
        yield return new WaitForSeconds(delay);
        // spawn the balloon at the group position with a random offset
        Vector3 pos = Camera.main.transform.position;
        while ((Vector3.Distance(pos,Camera.main.transform.position) < playerDistance) || (Vector3.Distance(pos, prevLocation) < prevSpawnDistance))
        {
            pos = RandomPointInBounds(spawnAreaLowerBounds, spawnAreaUpperBounds);
        }

        GameObject balloonObj = Instantiate(balloonPrefab, pos, Quaternion.identity);
        
        Balloon balloon = balloonObj.GetComponent<Balloon>();
        balloon.Initialize(groupId,balloonId,balloonColors[groupId]);
        balloon.transform.localScale = new Vector3(initBalloonSize, initBalloonSize, initBalloonSize); // set initial size
        balloon.OnExplode += HandleBalloonExploded;
        balloon.OnPopped += HandleBalloonPopped;

        balloons.Add(balloon);
    }

    public Vector3 RandomPointInBounds(Vector3 lowerBounds, Vector3 upperBounds)
    {
        return new Vector3(
            Random.Range(lowerBounds.x, upperBounds.x),
            Random.Range(lowerBounds.y, upperBounds.y),
            Random.Range(lowerBounds.z, upperBounds.z)
        );
    }

    private void UpdateTexts()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
        

        if (timerText != null)
        {
            timerText.text = "Time left: " + Mathf.RoundToInt(duration - (Time.time-timer)) + "s";
        }
        
    }

    void HandleBalloonExploded(Balloon b)
    { 
        if (b.groupId == 0) // this was a target balloon, we get negative points
        {
            score -= Mathf.Abs(explodeScore); // take neg abs,then it doesn't matter how the explodeScore was defined (pos or neg)
            SpawnPoints(b.transform.position, -Mathf.Abs(explodeScore));
        }
        /*
        // Instantiate explosion effect at the balloon's position
        GameObject explosion = Instantiate(explosionPrefab, b.transform.position, Quaternion.identity);
        // set the color of the explosion to the color of the balloon
        ParticleSystem.MainModule main = explosion.GetComponent<ParticleSystem>().main;
        // play the particle system
        main.startColor = b.GetComponent<Renderer>().material.color;
        explosion.GetComponent<ParticleSystem>().Play();
        */
        Debug.Log("Balloon exploded! Score: " + score);
        // start score animation

        // spawn a new balloon in the same group
        float delay = Random.Range(0f, 1f) + spawnDelay; // random delay for each balloon
        StartCoroutine(SpawnBalloon(delay,b.groupId, b.balloonId, b.transform.position));
    }

    void HandleBalloonPopped(Balloon b)
    {
        int points;
        if (b.groupId == 0)
        {
            if (b.transform.localScale.x < highPointsThreshold)
            {
                points = highPoints;
            }
            else
            {
                points = lowPoints;
            }
        }
        else
        {
            points = -Mathf.Abs(wrongBaloonScore);
        }
        score += points;
        SpawnPoints(b.transform.position, points);
        Debug.Log("Balloon popped! Score: " + score);
        // spawn a new balloon in the same group
        float delay = Random.Range(0f, 1f) + spawnDelay; // random delay for each balloon
        StartCoroutine(SpawnBalloon(delay, b.groupId, b.balloonId, b.transform.position));
    }

    void SpawnPoints(Vector3 position, int points)
    {
        GameObject pointsObj = Instantiate(pointsPrefab, position + 0.2f * Vector3.up, Quaternion.identity);
        // get child object with text component
        TMP_Text pointsText = pointsObj.transform.GetChild(0).GetComponent<TMP_Text>();
        if (points > 0)
        {
            pointsText.text = "+" + points.ToString();
        }
        else
        {
            pointsText.text =  points.ToString();
        }
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
