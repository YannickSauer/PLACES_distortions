using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using Random = UnityEngine.Random;
using System.IO;


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

    [Header("Game Settings")]
    public float roundTime = 120f; // in seconds; duration of one round
    public int roundCounter = 0; // counter for the current round
    public int totalRounds = 0;
    [HideInInspector] public List<float> highScores = new List<float>();
    [HideInInspector] public float roundTimer = 0f; // timer for the current round
    [HideInInspector] public bool inRound = false; // flag to check if we are in a round

    [Header("Spawn Settings")]
    public float spawnDelay = 2f; // delay between destruction of balloon and spawning a new one
    private float timer = 0f; // timer for the current run
    public Vector3 spawnAreaLowerBounds;
    public Vector3 spawnAreaUpperBounds;
    public float playerDistance; // minimum distnace to player
    public float prevSpawnDistance; // minimum distance to previous spawn location

    [Header("Trainings Settings")]
    public List<string> pathList;

    [Header("Debug Data")]
    public float duration = 120f; // in seconds; duration of the test
    private List<Balloon> balloons = new List<Balloon>();
    public bool HasBalloons => balloons.Count > 0;
    public bool HasNextRound => (totalRounds > roundCounter) && canPlayAgain;
    public bool canPlayAgain;
    [HideInInspector] public int score = 0;
    private bool buttonPressed;
    private bool isTraining = false;
    public event Action OnRoundStart;
    public event Action OnRoundOver;
    public event Action<string> OnNextTrainingStep;



    void Start()
    {
        // check if enough colors are provided
        if (balloonColors.Count < 2)
        {
            Debug.LogError("At least two colors are necessary.");
            return;
        }

        // start the timer
        timer = Time.time;

        // set the min and max grow speed for all balloons
        Balloon.minGrowSpeed = minGrowSpeed;
        Balloon.maxGrowSpeed = maxGrowSpeed;
        Balloon.maxSize = explodeSize;
    }

    public IEnumerator RunTraining()
    {
        isTraining = true;
        Vector3 spawnPos = Vector3.zero;

        for (int step = 0; step < pathList.Count; step++)
        {
            string pathToText = Path.Combine(Application.dataPath, pathList[step]);
            Debug.Log(pathToText);

            // TODO Put readtext to utils ?
            string nextInstructionText = DisplayInformation.ReadText(pathToText);
            Debug.Log(nextInstructionText);
            OnNextTrainingStep?.Invoke(nextInstructionText);

            buttonPressed = false;
            switch (step)
            {
                case 0: // Show Welcome Text
                    yield return new WaitUntil(() => buttonPressed);
                    break;

                case 1: // Show LookAround Text
                    yield return new WaitUntil(() => buttonPressed);
                    break;

                case 2: // Destroy a green balloon
                    yield return new WaitForSeconds(6);
                    // Determine position and spawn the balloon
                    spawnPos = Camera.main.transform.position + new Vector3(-1.0f, 0, 1.5f);
                    SpawnForTutorial(0, spawnPos);
                    yield return null;
                    // wait until balloon is destroyed
                    yield return new WaitUntil(() => !HasBalloons);
                    break;

                case 3: // Destroy a blue balloon
                    yield return new WaitForSeconds(3);
                    // Determine position and spawn the balloon
                    spawnPos = Camera.main.transform.position + new Vector3(-1.0f, 0, 1.5f);
                    SpawnForTutorial(1, spawnPos);
                    yield return null;
                    // wait until balloon is destroyed
                    yield return new WaitUntil(() => !HasBalloons);
                    break;

                case 4: // Play one test round
                    yield return new WaitUntil(() => buttonPressed);
                    // hide instruction and background
                    OnNextTrainingStep?.Invoke("hide");
                    yield return StartCoroutine(StartRound());
                    break;

                case 5: // Recenter head
                    yield return new WaitForSeconds(7);
                    yield return this.GetComponent<RecenterHead>().RunRecenter();
                    break;
            }
        }
        isTraining = false;
    }


    private IEnumerator StartRound()
    {
        // reset score and timer
        score = 0;
        roundTimer = Time.time;
        inRound = true;
        OnRoundStart?.Invoke();
        roundCounter++;
        // reset the balloons
        foreach (Balloon balloon in balloons)
        {
            if (balloon != null)
            {
                Destroy(balloon.gameObject);
            }
        }
        balloons.Clear();
        SpawnAllBalloons();
        // wait for the round to finish
        yield return new WaitForSeconds(roundTime);
        // end the round
        // destroy all balloons
        foreach (Balloon balloon in balloons)
        {
            if (balloon != null)
            {
                Destroy(balloon.gameObject);
            }
        }
        // save the score
        highScores.Add(score);
        // sort the high scores
        highScores.Sort((a, b) => b.CompareTo(a)); // sort in descending order
        // keep only the top 5 scores
        if (highScores.Count > 5)
        {
            highScores.RemoveRange(5, highScores.Count - 5);
        }
        inRound = false;
        OnRoundOver?.Invoke();
    }

    private void SpawnAllBalloons()
    {
        // spawn target balloons
        for (int balloonId = 0; balloonId < nTargetBalloons; balloonId++)
        {
            // Start coroutine to spawn balloons with a delay
            float delay = Random.Range(0f, 1f); // random delay for each balloon
            Vector3 newPos = FindNewPosition(Camera.main.transform.position);
            StartCoroutine(SpawnBalloon(delay, 0, balloonId, newPos));
        }

        // spawn distractor balloons
        for (int balloonId = 0; balloonId < nDistractorBalloons; balloonId++)
        {
            // Start coroutine to spawn balloons with a delay
            float delay = Random.Range(0f, 1f); // random delay for each balloon
            Vector3 newPos = FindNewPosition(Camera.main.transform.position);
            StartCoroutine(SpawnBalloon(delay, 1, balloonId, newPos));
        }
    }

    private IEnumerator SpawnBalloon(float delay, int groupId, int balloonId, Vector3 pos)
    {
        // wait for the delay before spawning the balloon
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
        
        // if (!inRound)
        // {
        //     yield break; // if not in round, do not spawn the balloon
        // }
        // spawn the balloon at the group position with a random offset
        GameObject balloonObj = Instantiate(balloonPrefab, pos, Quaternion.identity);

        Balloon balloon = balloonObj.GetComponent<Balloon>();
        balloon.Initialize(groupId, balloonId, balloonColors[groupId]);
        balloon.transform.localScale = new Vector3(initBalloonSize, initBalloonSize, initBalloonSize); // set initial size
        balloon.OnExplode += HandleBalloonExploded;
        balloon.OnPopped += HandleBalloonPopped;

        balloons.Add(balloon);

        // remove null balloons from the list
        balloons.RemoveAll(b => b == null);
    }

    private Vector3 FindNewPosition(Vector3 prevLocation)
    {
        Vector3 pos = Camera.main.transform.position;
        while ((Vector3.Distance(pos, Camera.main.transform.position) < playerDistance) || (Vector3.Distance(pos, prevLocation) < prevSpawnDistance))
        {
            pos = RandomPointInBounds(spawnAreaLowerBounds, spawnAreaUpperBounds);
            Debug.Log(pos);
        }
        return pos;
    }

    public Vector3 RandomPointInBounds(Vector3 lowerBounds, Vector3 upperBounds)
    {
        return new Vector3(
            Random.Range(lowerBounds.x, upperBounds.x),
            Random.Range(lowerBounds.y, upperBounds.y),
            Random.Range(lowerBounds.z, upperBounds.z)
        );
    }

    void HandleBalloonExploded(Balloon b)
    {
        if (b.groupId == 0) // this was a target balloon, we get negative points
        {
            score -= Mathf.Abs(explodeScore); // take neg abs,then it doesn't matter how the explodeScore was defined (pos or neg)
            SpawnPoints(b.transform.position, -Mathf.Abs(explodeScore));
            GameObject explosion = Instantiate(explosionPrefab, b.transform.position, Quaternion.identity);
            // play the explosion particle system
            ParticleSystem explosionPS = explosion.GetComponent<ParticleSystem>();
            explosionPS.Play();
            // destroy the explosion after 1 second
            Destroy(explosion, 1f);
        }

        //Instantiate explosion effect at the balloon's position
        // set the color of the explosion to the color of the balloon
        //ParticleSystem.MainModule main = explosion.GetComponent<ParticleSystem>().main;
        // play the particle system
        //main.startColor = b.GetComponent<Renderer>().material.color;
        //explosion.GetComponent<ParticleSystem>().Play();

        Debug.Log("Balloon exploded! Score: " + score);
        // start score animation

        // spawn a new balloon in the same group
        if (inRound)
        {
            float delay = Random.Range(0f, 1f) + spawnDelay; // random delay for each balloon
            Vector3 newPos = FindNewPosition(b.transform.position);
            StartCoroutine(SpawnBalloon(delay, b.groupId, b.balloonId, newPos));
        }
        else
        {
            StartCoroutine(SpawnBalloon(0f, b.groupId, b.balloonId, b.transform.position));
        }
        // remove null balloons from the list
            balloons.Remove(b);
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
            GameObject explosion = Instantiate(explosionPrefab, b.transform.position, Quaternion.identity);
            // play the explosion particle system
            ParticleSystem explosionPS = explosion.GetComponent<ParticleSystem>();
            //ParticleSystem.MainModule ma = explosionPS.main;
            //ma.startColor = b.GetComponent<Renderer>().material.color;

            explosionPS.Play();
            // destroy the explosion after 1 second
            Destroy(explosion, 1f);
        }
        score += points;
        SpawnPoints(b.transform.position, points);
        Debug.Log("Balloon popped! Score: " + score);
        // spawn a new balloon in the same group
        if (inRound)
        {
            float delay = Random.Range(0f, 1f) + spawnDelay; // random delay for each balloon
            Vector3 newPos = FindNewPosition(b.transform.position);
            StartCoroutine(SpawnBalloon(delay, b.groupId, b.balloonId, newPos));
        }
        // remove null balloons from the list
        balloons.Remove(b);
    }

    void SpawnPoints(Vector3 position, int points)
    {
        GameObject pointsObj = Instantiate(pointsPrefab, position + 0.2f * Vector3.up, Quaternion.identity);
        pointsObj.transform.LookAt(Camera.main.transform);
        pointsObj.transform.Rotate(0, 180f, 0); // Rotate to face the camera properly
        // get child object with text component
        TMP_Text pointsText = pointsObj.transform.GetChild(0).GetComponent<TMP_Text>();
        if (points > 0)
        {
            pointsText.text = "+" + points.ToString();
        }
        else
        {
            pointsText.text = points.ToString();
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

    private void OnTouchpad() // called by the touchpad of the controller
    {
        buttonPressed = true;
        if (!isTraining && !inRound)
        {
            StartCoroutine(StartRound());
        }
    }

    public void StartGame()
    {
        StartCoroutine(StartRound());
    }

    public void SpawnForTutorial(int groupId, Vector3 pos)
    {
        Debug.Log("Spawn for tutorial.");
        StartCoroutine(SpawnBalloon(0.0f, groupId, 0, pos));
    }


    // For testing: destroy balloon with mouse click (or raycast in VR)
    void Update()
    {

        // if not in round, check if trigger is pressed
        if (!inRound && Input.GetKeyDown(KeyCode.B))
        {
            StartCoroutine(StartRound());
        }

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log(Input.mousePosition);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, 10f * ray.direction, Color.cyan);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log(hit.transform.gameObject.name);
                Balloon balloon = hit.collider.GetComponent<Balloon>();
                if (balloon != null)
                {
                    Debug.Log("Balloon Hit");
                    balloon.Pop(ray.direction);
                }
            }
        }



        //UpdateTexts();
        // check if the time is up
        // if (Time.time - timer >= duration)
        // {
        //     Debug.Log("Time's up! Final score: " + score);
        //     // handle end of the game, e.g. show results or go to next scene
        //     // SceneManager.LoadScene("NextScene");
        // }
        // extenstions:
        // 1. Add a timer to the game
        // 2. Increase the speed of the balloons over time
        // 3. Add a score multiplier for consecutive pops
        // 4. Add a sound effect for popping balloons
        // 5. Add a visual effect for popping balloons
        // 6. Add a countdown timer for the game
    }
    

}
