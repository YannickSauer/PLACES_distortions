using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Metronome : MonoBehaviour
{
    public float bpm = 100.0f; // beats per minute
    public AudioClip beatSound; // sound to play on each beat
    private float metronomeFrequency; // frequency in Hz
    private float lastBeatTime;
    private float nextBeatTime;
    private AudioSource beatSource;
    private Coroutine lastRoutine = null;


    void Start()
    {
        metronomeFrequency = bpm / 60.0f; // convert bpm to Hz
        beatSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {

    }
    public void StartMetronome()
    {
        Debug.Log("Starting metronome at " + bpm + " bpm (" + metronomeFrequency + " Hz)");
        lastRoutine = StartCoroutine(RunMetronome());
    }

    public void StopMetronome()
    {
        Debug.Log("Stopping metronome");
        if (lastRoutine != null)
        {
            StopCoroutine(lastRoutine);
            lastRoutine = null;
        }
    }

    public float GetLastBeatTime()
    {
        return lastBeatTime;
    }
    public float GetNextBeatTime()
    {
        return nextBeatTime;
    }

    private IEnumerator RunMetronome()
    {
        // play a metronome sound at a given frequency
        float waitTime = 1 / metronomeFrequency;
        while (true)
        {
            PlayBeat(1.25f); // high pitch metronom sound
            // save time to compare participant movement with the metronome
            lastBeatTime = Time.time;
            nextBeatTime = lastBeatTime + waitTime;
            yield return new WaitForSeconds(waitTime);

            PlayBeat(1f); // low pitch metronom sound
            // save time to compare participant movement with the metronome
            lastBeatTime = Time.time;
            nextBeatTime = lastBeatTime + waitTime;
            yield return new WaitForSeconds(waitTime);
        }
    }
    
    public void PlayBeat()
    {
        PlayBeat(1f);
    }
    public void PlayBeat(float pitch)
    {
        beatSource.pitch = pitch;
        beatSource.PlayOneShot(beatSound);
    }
}
