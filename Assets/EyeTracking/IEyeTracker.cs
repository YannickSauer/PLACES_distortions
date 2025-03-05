using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Gaze data struct
public struct GazeData
{
    public long deviceTimestamp;
    public float UnityTimestamp;
    
    public Ray leftGazeRay;
    public Ray rightGazeRay;
    public Ray combinedGazeRay;
    public float gazeDistance;

    public float leftPupilDiameter;
    public float rightPupilDiameter;
    public float leftEyeOpenness;
    public float rightEyeOpenness;


    public int leftValidity;
    public int rightValidity;
}

public interface IEyeTracker
{    
    // Initialize the eye tracker
    public void Initialize();

    // Calibrate eye tracking
    public void Calibrate();

    // Get the current gaze point
    public GazeData GetGazeData();

    public void StartListening();
    public void StopListening();
}