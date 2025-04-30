using UnityEngine;
using System;

public class Balloon : MonoBehaviour
{
    public static float minGrowSpeed = 0.001f; // minimum grow speed for all balloons
    public static float maxGrowSpeed = 0.003f; // maximum grow speed for all balloons
    public float maxSize = 0.5f; // threshold for explosion
    public int balloonId;
    public int groupId;
    public event Action<Balloon> OnExplode;
    public event Action<Balloon> OnPopped;

    private float growSpeed; // individual grow speed
    private bool isActive = true;

    void Start()
    {
        growSpeed = UnityEngine.Random.Range(minGrowSpeed, maxGrowSpeed);
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

    public void Pop()
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
