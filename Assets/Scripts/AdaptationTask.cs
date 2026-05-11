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
    public float initDistractorSize = 0.001f; // seperate initial size for blue/distractor balloons
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
    private Transform cachedCameraTransform; // cached Camera.main.transform (avoids tag-lookup in hot path)
    public Vector3 spawnAreaLowerBounds;
    public Vector3 spawnAreaUpperBounds;
    public float playerDistance; // minimum distnace to player
    public float prevSpawnDistance; // minimum distance to previous spawn location
    public float maxSpawnAngle = 90f; // max horizontal angle from forward direction (in degrees). 90 = balloons only in front half
    public Transform forwardReference; // assign in inspector: defines what counts as "front" (e.g. window direction). If null, falls back to Camera.main.transform.forward

    [Header("Trainings Settings")]
    public List<string> pathList;
    public List<AudioClip> trainingAudioClips;
    public AudioClip roundEndClip; // sound played when the round timer runs out
    private AudioSource trainingAudioSource;

    [Header("Debug Data")]
    public float duration = 120f; // in seconds; duration of the test
    private List<Balloon> balloons = new List<Balloon>();
    public bool HasBalloons => balloons.Count > 0;
    public bool HasNextRound => (totalRounds > roundCounter) && canPlayAgain;
    public bool canPlayAgain;
    [HideInInspector] public bool isDone = false;
    [HideInInspector] public int score = 0;
    private bool buttonPressed;
    private bool isTraining = false;
    public event Action OnRoundStart;
    public event Action OnRoundOver;
    public event Action<string> OnNextTrainingStep;



    void Start()
    {
        if (Camera.main != null)
            cachedCameraTransform = Camera.main.transform;
        // check if enough colors are provided
        if (balloonColors.Count < 2)
        {
            Debug.LogError("At least two colors are necessary.");
            return;
        }

        // start the timer
        timer = Time.time;

        // Always create a dedicated AudioSource for training voiceover
        // (don't reuse existing one which may be used for balloon sounds)
        trainingAudioSource = gameObject.AddComponent<AudioSource>();
        trainingAudioSource.playOnAwake = false;

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

            string nextInstructionText = DisplayInformation.ReadText(pathToText);
            Debug.Log(nextInstructionText);
            OnNextTrainingStep?.Invoke(nextInstructionText);

            // Play training audio if available for this step
            if (trainingAudioClips != null && step < trainingAudioClips.Count && trainingAudioClips[step] != null)
            {
                trainingAudioSource.Stop();
                trainingAudioSource.pitch = 1f;
                trainingAudioSource.PlayOneShot(trainingAudioClips[step]);
            }

            buttonPressed = false;
            switch (step)
            {
                case 0: // Show Welcome Text
                    yield return new WaitUntil(() => buttonPressed);
                    trainingAudioSource.Stop();
                    break;

                case 1: // Show LookAround Text
                    yield return new WaitUntil(() => buttonPressed);
                    trainingAudioSource.Stop();
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
                    trainingAudioSource.Stop();
                    // hide instruction and background
                    OnNextTrainingStep?.Invoke("hide");
                    yield return StartCoroutine(StartRound());
                    break;

                case 5: // Recenter head
                    yield return new WaitForSeconds(11);
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
        inRound = false; // set this first so no new balloons are spawned while cleanup
        // play round-end sound so the participiant knows the round is over
        if (roundEndClip != null)
        {
            AudioSource.PlayClipAtPoint(roundEndClip, Camera.main.transform.position);
        }
        // destroy all balloons
            foreach (Balloon balloon in balloons)
            {
                if (balloon != null)
                {
                    Destroy(balloon.gameObject);
                }
            }
        // clear the list so no stale references remain
        balloons.Clear(); 
        // save the score in chronological order
        highScores.Add(score);
        
        // inRound was already set to false above

        OnRoundOver?.Invoke();
        if (!HasNextRound)
        {
            isDone = true;
        }
    }

    private void SpawnAllBalloons()
    {
        // Cache camera position once for all 7 spawns (avoids 14 Camera.main tag lookups at round start)
        Vector3 camPos = cachedCameraTransform.position;

        // spawn target balloons
        for (int balloonId = 0; balloonId < nTargetBalloons; balloonId++)
        {
            float delay = Random.Range(0f, 1f); // random delay for each balloon
            Vector3 newPos = FindNewPosition(camPos);
            StartCoroutine(SpawnBalloon(delay, 0, balloonId, newPos));
        }

        // spawn distractor balloons
        for (int balloonId = 0; balloonId < nDistractorBalloons; balloonId++)
        {
            float delay = Random.Range(0f, 1f); // random delay for each balloon
            Vector3 newPos = FindNewPosition(camPos);
            StartCoroutine(SpawnBalloon(delay, 1, balloonId, newPos));
        }
    }

    private IEnumerator SpawnBalloon(float delay, int groupId, int balloonId, Vector3 pos, bool forceSpawn = false)
    {
        // wait for the delay before spawning the balloon
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }
        
        if (!inRound && !forceSpawn)
        {
            yield break; // if not in round, do not spawn the balloon
        }

        // spawn the balloon at the group position with a random offset
        GameObject balloonObj = Instantiate(balloonPrefab, pos, Quaternion.identity);

        Balloon balloon = balloonObj.GetComponent<Balloon>();
        balloon.Initialize(groupId, balloonId, balloonColors[groupId]);
        float size = (groupId == 0) ? initBalloonSize : initDistractorSize;
        balloon.transform.localScale = new Vector3(size, size, size); // set initial size based on group
        balloon.OnExplode += HandleBalloonExploded;
        balloon.OnPopped += HandleBalloonPopped;

        balloons.Add(balloon);

        // remove null balloons from the list
        balloons.RemoveAll(b => b == null);
    }

    private Vector3 FindNewPosition(Vector3 prevLocation)
    {
        // Cache camera position once outside the loop to avoid tag-lookup spam.
        Vector3 camPos = cachedCameraTransform.position;
        Vector3 pos = camPos;
        int maxAttempts = 100;
        int attempts = 0;

        bool valid = false;
        while (!valid)
        {
            pos = RandomPointInBounds(spawnAreaLowerBounds, spawnAreaUpperBounds);
            attempts++;

            // Check 1: distance from player
            bool farEnoughFromPlayer = Vector3.Distance(pos, camPos) >= playerDistance;
            // Check 2: distance from previous spawn
            bool farEnoughFromPrev = Vector3.Distance(pos, prevLocation) >= prevSpawnDistance;
            // Check 3: line of sight (no obstacle between player and balloon position)
            bool hasLineOfSight = !Physics.Linecast(camPos, pos);

            valid = farEnoughFromPlayer && farEnoughFromPrev && hasLineOfSight;

            if (attempts > maxAttempts)
            {
                Debug.LogWarning("Could not find a suitable spawn position after " + maxAttempts + " attempts. Spawning at last tried position.");
                break;
            }
        }
        return pos;
    }

    // checks whether a candidate position lies within maxSpawnAngle horizontal degrees of the defined forward direction
    private bool IsInFrontOfPlayer(Vector3 candidatePos)
    {
        Vector3 playerPos = Camera.main.transform.position;
        // use forwardReference if assigned, else fall back to camera forward
        Vector3 forwardDir = (forwardReference != null) ? forwardReference.forward : Camera.main.transform.forward;
        // project both vectors to the horizontal plane (ignore vertical component)
        forwardDir.y = 0f;
        Vector3 toCandidate = candidatePos - playerPos;
        toCandidate.y = 0f;
        // edge case: candidate is directly above/below player
        if (toCandidate.sqrMagnitude < 0.0001f) return true;
        float angle = Vector3.Angle(forwardDir, toCandidate);
        return angle <= maxSpawnAngle;
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
        // Green balloons that grew too big now vanish silently with no penalty.
        // The participant didn't actively do anything wrong - they just missed it.
        // No score change, no points popup, no explosion particle effect.

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
            //set the color of the explosion to the color of the balloon
            ParticleSystem.MainModule ma = explosionPS.main;
            Color balloonColor = b.GetComponent<Renderer>().material.color;
            balloonColor.a = 1f;
            ma.startColor = balloonColor;
            explosionPS.Play();
            // destroy the explosion after 1 second
            Destroy(explosion, 1f);
        }
        score += points;
        SpawnPoints(b.transform.position, points);
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
        pointsObj.transform.LookAt(cachedCameraTransform);
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
        if (!isTraining && !inRound && HasNextRound)
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
        StartCoroutine(SpawnBalloon(0.0f, groupId, 0, pos, true));
    }


    void Update()
    {
        // Manual start via keyboard (operator shortcut during testing)
        if (!inRound && Input.GetKeyDown(KeyCode.B))
        {
            StartCoroutine(StartRound());
        }
    }
}
