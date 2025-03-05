using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// This class is used to trigger an event when eye tracking data is available
// This is used to decouple the subsciption to the event from the actual eye tracking implementation
// The individual eye tracking implementations will trigger this event when new data is available
public class EyeTrackingEvent : MonoBehaviour
{
    public static event Action<GazeData> OnDataAvailable;

    public static void TriggerEvent(GazeData data)
    {
        Debug.Log("Triggering event");
        OnDataAvailable?.Invoke(data);
    }
}