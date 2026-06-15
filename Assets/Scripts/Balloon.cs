using UnityEngine;
using System;
using System.Collections;

public class Balloon : MonoBehaviour
{
    public static float minGrowSpeed = 0.001f; // minimum grow speed for all balloons
    public static float maxGrowSpeed = 0.003f; // maximum grow speed for all balloons
    public static float movingProbability = 0.0f; // probability of moving balloon
    public static float movingSpeed = 1f; // minimum size for all balloons
    public static float maxSize = 0.5f; // threshold for explosion
    public int balloonId;
    public int groupId;
    public event Action<Balloon> OnExplode;
    public event Action<Balloon> OnPopped;
    public AudioClip explosionSound;
    public AudioClip popSound;
    public AudioClip wrongPopSound;
    public AudioClip vanishSound; // neutral puff sound when balloon vanishes without popping or exploding (e.g. when it goes out of bounds)

    //[Header("Initial Size")] // (Tolga)
    //public float initialScaleGreen = 0.2f;
    //public float initialScaleBlue = 0.15f;

    private float growSpeed; // individual grow speed
    private bool isActive = true;
    public float spawnTime { get; private set; } // Time.time when this balloon was spawned

    void Start()
    {
        spawnTime = Time.time; // record the spawn time of the balloon
        growSpeed = UnityEngine.Random.Range(minGrowSpeed, maxGrowSpeed);
        if (UnityEngine.Random.value < movingProbability)
        {
            // make the balloon move in a random direction
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere;
            randomDirection.y = 0; // keep it on the same plane
            // use rigidbody to move the balloon
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // make sure the rigidbody is not kinematic
                rb.velocity = randomDirection * movingSpeed; // set the velocity to a random direction
            }
            else
            {
                Debug.LogWarning("Rigidbody not found on balloon. Moving balloon will not work.");
            }
        }
    }

    void Update()
    {
        if (!isActive) return;

        transform.localScale += Vector3.one * growSpeed * Time.deltaTime;

        

        if (transform.localScale.x >= maxSize)
        {
            Explode();
        }
    }

    public void Initialize(int groupId, int balloonId, Color color)
    {
        this.groupId = groupId;
        this.balloonId = balloonId;
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        // start size
        //float startScale = (groupId == 0) ? initialScaleGreen : initialScaleBlue;
        //transform.localScale = Vector3.one * startScale;
    }

    public void Pop(Vector3 direction)
    {
        if (!isActive) return;
        // play pop sound
        if (groupId == 0)
        {
            AudioSource.PlayClipAtPoint(popSound, transform.position);
        }
        else
        {
            AudioSource.PlayClipAtPoint(wrongPopSound, transform.position);
        }
        isActive = false;
        // Instatiate a pop prefab here
        // GameObject popEffect = Instantiate(popPrefab, transform.position, Quaternion.identity);
        // Destroy(popEffect, 1f); // destroy the pop effect after 1 second
        // set color of pop effect to the color of the balloon
        // var popRenderer = popEffect.GetComponent<Renderer>();
        // if (popRenderer != null)
        // {
        //     popRenderer.material.color = GetComponent<Renderer>().material.color;
        // }
        // Invoke the OnPopped event so that the AdaptationTask can handle it (e.g. update score)
        OnPopped?.Invoke(this);
        StartCoroutine(PopEffect(direction));
    }

    private IEnumerator PopEffect(Vector3 direction)
    {
        
        // scale down the balloon to zero over 0.5 seconds
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;
        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / duration);
            // move the balloon in the direction of the hit normal
            transform.position += direction * 3* Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }
        // destroy the balloon after the effect
        Destroy(gameObject);
    }

    private void Explode()
    {
        if (!isActive) return;
        // play vanish sound for green balloons (neutral puff, no penalty feel)
        if (groupId == 0) // target balloon - just gently vanishes
        {
            if (vanishSound != null)
            {
                AudioSource.PlayClipAtPoint(vanishSound, transform.position);
            }
        }
        // blue balloons vanish silently (unchanged behavior)

        isActive = false;
        OnExplode?.Invoke(this);

        // gentle shrink effect instead of hard destroy
        StartCoroutine(VanishEffect());
    }

    private IEnumerator VanishEffect()
    {
        // smoothly scale down the balloon to zero over 0.3 seconds
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;
        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
    // add onDestroy method to clean up the event listeners
    private void OnDestroy()
    {
        OnExplode = null;
        OnPopped = null;
    }
}
