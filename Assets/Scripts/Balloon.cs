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

    private float growSpeed; // individual grow speed
    private bool isActive = true;

    void Start()
    {
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
    }

    public void Pop(Vector3 direction)
    {
        if (!isActive) return;

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
        // Instatiate a explode prefab here
        // GameObject popEffect = Instantiate(explodePrefab, transform.position, Quaternion.identity);
        // Destroy(explodeEffect, 1f); // destroy the pop effect after 1 second
        // set color of pop effect to the color of the balloon
        // var explodeRenderer = explodeEffect.GetComponent<Renderer>();
        // if (explodeRenderer != null)
        // {
        //     explodeRenderer.material.color = GetComponent<Renderer>().material.color;
        // }
        // Invoke the OnPopped event so that the AdaptationTask can handle it (e.g. update score)
        isActive = false;
        OnExplode?.Invoke(this);
        
        Destroy(gameObject);
    }
}
