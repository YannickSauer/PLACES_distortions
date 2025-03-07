using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System;
using System.Timers;
public class DummyEyeTracker : IEyeTracker
{
    private GazeData currentGazeData;
    private GameObject cam;
    private Stopwatch stopwatch;
    private float simulatedIpd = 0.064f; // default is 64mm
    private float simulatedNoise = 0.0f; // default is 0.0
    private bool backgroundSampling = false;
    private Timer timer;


    // Initialize the dummy eye tracker and set simulated gaze properties
    public void Initialize(float ipd,float noise)
    {
        // Find camera object
        cam = GameObject.Find("Main Camera");

        // Initialize and start the stopwatch
        stopwatch = new Stopwatch();
        stopwatch.Start();

        currentGazeData = new GazeData();

        // Set the simulated IPD and noise
        simulatedIpd = ipd;
        simulatedNoise = noise;
        UnityEngine.Debug.Log("Dummy eye tracker initialized with IPD: " + simulatedIpd + " and noise: " + simulatedNoise);
    }

    private void OnTimedEvent(object sender, ElapsedEventArgs e)
    {
        UnityEngine.Debug.Log("Event triggered at: " + e.SignalTime);

        UnityEngine.Debug.Log("On our way");
        // Get the current gaze data and raise the event
        currentGazeData = SimulatedGazeData();
        UnityEngine.Debug.Log("Made it our way");

        if (backgroundSampling)
        {
            UnityEngine.Debug.Log("Calling TriggerEvent");
            EyeTrackingEvent.TriggerEvent(currentGazeData);
        }
    }    

    public void StartListening()
    {
        backgroundSampling = true;
        UnityEngine.Debug.Log("Dummy eye tracker started listening");
        timer = new Timer(500); // 500 ms interval (0.5 sec)
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = true; // Repeat
        timer.Start();
    }

    public void StopListening()
    {
        backgroundSampling = false;
        timer.Stop();    // Stop the timer
        timer.Dispose(); // Release resources
        timer = null;    // Prevent further use
    }
    // Initialize the dummy eye tracker with default noise
    public void Initialize(float ipd)
    {
        // Find camera object
        cam = GameObject.Find("Main Camera");

        // Initialize and start the stopwatch
        stopwatch = new Stopwatch();
        stopwatch.Start();

        currentGazeData = new GazeData();

        // Set the simulated IPD
        simulatedIpd = ipd;
        UnityEngine.Debug.Log("Dummy eye tracker initialized with IPD: " + simulatedIpd + " and noise: " + simulatedNoise);

    }
    
    // initialize the default eye tracker with default IPD and noise
    public void Initialize()
    {
        // Find camera object
        cam = GameObject.Find("Main Camera");

        // Initialize and start the stopwatch
        stopwatch = new Stopwatch();
        stopwatch.Start();

        currentGazeData = new GazeData();
        UnityEngine.Debug.Log("Dummy eye tracker initialized with IPD: " + simulatedIpd + " and noise: " + simulatedNoise);

    }

    // Calibrate eye tracking
    public void Calibrate()
    {
        // so far we dont simulate a calibratio
        return;
    }

    // Get the current gaze point
    public GazeData GetGazeData()
    {
        return currentGazeData;
    }   

    private GazeData SimulatedGazeData()
    {
        if (cam == null)
        {
            cam = GameObject.Find("Main Camera");
            if (cam == null)
            {
                UnityEngine.Debug.LogError("Camera not found.");
                return new GazeData();
            }
        }
        //Vector3 origin = cam.transform.position;
        //Vector3 direction = cam.transform.rotation * Vector3.forward;
        //Ray mainCamView = new Ray(origin, direction);
        //Ray leftEyeRay = new Ray(origin - cam.transform.right * simulatedIpd / 2, direction);
        //Ray rightEyeRay = new Ray(origin + cam.transform.right * simulatedIpd / 2, direction);

        Ray leftGazeRay = new Ray(new Vector3(- simulatedIpd / 2,0,0), Vector3.forward);
        Ray rightGazeRay = new Ray(new Vector3(simulatedIpd / 2,0,0), Vector3.forward);
        Ray combinedGazeRay = new Ray(new Vector3(0,0,0), Vector3.forward);

        GazeData simulatedGazeData = new GazeData(); 
        simulatedGazeData.deviceTimestamp = stopwatch.ElapsedMilliseconds;
        simulatedGazeData.UnityTimestamp = Time.time;
        simulatedGazeData.leftGazeRay = leftGazeRay;
        simulatedGazeData.rightGazeRay = rightGazeRay;
        simulatedGazeData.combinedGazeRay = combinedGazeRay;
        simulatedGazeData.gazeDistance = 1000.0f;
        simulatedGazeData.leftPupilDiameter = 4.0f;
        simulatedGazeData.rightPupilDiameter = 4.0f;
        simulatedGazeData.leftEyeOpenness = 1.0f;
        simulatedGazeData.rightEyeOpenness = 1.0f;
        simulatedGazeData.leftValidity = 1;
        simulatedGazeData.rightValidity = 1;

        // add gaussian noise to the gaze data
        //TODO: implement noise
        UnityEngine.Debug.Log("On our way3");
        return new GazeData();//simulatedGazeData;
    }

    // Start queueing Eye samples in background
    //public void StartBackgroundSampling(Queue<GazeData> gazeSamples)
    //{
    //    return;
    //}
    //public void StopBackgroundSampling()
    //{
    //    return;
    //}
 }