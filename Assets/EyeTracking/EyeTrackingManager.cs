using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System;
using System.Text;
using System.Threading;
using System.IO;

#if USE_VIVE
using ViveSR.anipal.Eye;
#endif

public class EyeTrackingManager : MonoBehaviour
{
    public static EyeTrackingManager instance;

    public enum ETProvider
    {
        Dummy,
        HTCViveSRanipal,
        Varjo,
        PupilNeon,
        MetaQuest,
        XTAL
    }
    [Header("Eye Tracking Settings")]

    public ETProvider etprovider = ETProvider.HTCViveSRanipal;
    private IEyeTracker eyeTracker;
    public KeyCode calibrateKey = KeyCode.C;

    public GazeData currentGazeData { get; private set; } // for gaze sample of the current frame
    public GazeData currentWorldGazeData { get; private set; } // for gaze sample of the current frame in world coordinates
    public Queue<GazeData> gazeTrackingQueue { get; private set; }

    // Enum to define different options for GameObject Tracking
    public enum TrackingOptions
    {
        localTransform,
        globalTransform,
    }

    // Define a class to hold the dropdown option and associated GameObject
    [Serializable]
    public class TrackedObjectOptions
    {
        public TrackingOptions trackingOptions;
        public GameObject gameObject;
    }

    public bool saveRaycastHitpoint = false; // check for raycast intersection with objects during runtime
    public string outputFolder = "measurements"; // folder for tracking data

    [Header("Object Tracking Settings")]
    // List to hold the variables with dropdown options and associated GameObjects
    [SerializeField] private List<TrackedObjectOptions> trackedObjectList = new List<TrackedObjectOptions>();


    private string objectTrackingFile; // output file for object tracking (bound to framerate)
    private string gazeTrackingFile; // output file for eye tracking data (bound to eye tracking frequency)
    Queue trackingDataQueue = new Queue();
    static string msgBuffer = "";

    public bool isObjectRecording = false;
    private bool isRecording = false;
    private Thread savingThread; // background thread for writing to files

    void Awake()
    {
        // set US culture for number formatting in strings
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
        
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.Log("Singleton instance already existed.");
            Destroy(gameObject);
            return;
        }

        gazeTrackingQueue = new Queue<GazeData>();

        switch (etprovider)
        {
             case ETProvider.Dummy:
                eyeTracker = new DummyEyeTracker();
                eyeTracker.Initialize();
                Debug.Log("Dummy eye tracker initialized.");
                break;
#if USE_VIVE
            case ETProvider.HTCViveSRanipal:
                // start SRanipal // todo, should be part of the interface I guess ???
                //instance.AddComponent<SRanipal_Eye_Framework>();
       
                eyeTracker = new ViveEyeTracker();
                eyeTracker.Initialize();
                Debug.Log("HTC Vive SRanipal initialized.");
                break;
#endif
#if USE_VARJO
            case ETProvider.Varjo:
                eyeTracker = new VarjoEyeTracker();
                eyeTracker.Initialize();
                break;
#endif
#if USE_NEON
            case ETProvider.PupilNeon:
                eyeTracker = new NeonEyeTracker();
                break;
#endif
#if USE_QUEST
            case ETProvider.MetaQuest:
                eyeTracker = new QuestEyeTracker();
                eyeTracker.Initialize();
                Debug.Log("Meta Quest initialized.");
                break;
#endif
#if USE_XTAL
            case ETProvider.XTAL:
                eyeTracker = new XtalEyeTracker();
                Debug.Log("XTAL eye tracker");
                eyeTracker.Initialize();
                break;
#endif
            default:
                Debug.Log("No Eye Tracker selected. Using dummy eye tracker.");
                eyeTracker = new DummyEyeTracker();
                eyeTracker.Initialize();
                break;
        }

        // event handler for gaze data
        EyeTrackingEvent.OnDataAvailable += HandleData; // subscribe to event

        // test Datetime accuracy
        DateTime t1 = DateTime.Now;
        DateTime t2;
        while ((t2 = DateTime.Now) == t1)
        {
            Debug.Log("DateTime accuracy: " + (t2 - t1).TotalMilliseconds + "ms");
        }

        // set US culture for number formatting in strings
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
    }

    private void Start()
    {
        eyeTracker.StartListening(); // start the background event system
    }

    void Update()
    {
        // TODO change to get-function
        currentGazeData = eyeTracker.GetGazeData();
        if (Input.GetKeyDown(calibrateKey))
        {
            Calibrate();
        }

        if (isObjectRecording)
        {
            QueueTrackingData(trackingDataQueue);
        }

    }

    public GazeData Local2WorldGaze(GazeData localGaze)
    {
        GazeData worldGaze = localGaze;
        worldGaze.leftGazeRay.origin = Camera.main.transform.TransformPoint(localGaze.leftGazeRay.origin);
        worldGaze.leftGazeRay.direction = Camera.main.transform.TransformDirection(localGaze.leftGazeRay.direction);
        worldGaze.rightGazeRay.origin = Camera.main.transform.TransformPoint(localGaze.rightGazeRay.origin);
        worldGaze.rightGazeRay.direction = Camera.main.transform.TransformDirection(localGaze.rightGazeRay.direction);
        worldGaze.combinedGazeRay.origin = Camera.main.transform.TransformPoint(localGaze.combinedGazeRay.origin);
        worldGaze.combinedGazeRay.direction = Camera.main.transform.TransformDirection(localGaze.combinedGazeRay.direction);
        return worldGaze;
    }

    public GazeData GetWorldGazeData()
    {
        return Local2WorldGaze(currentGazeData);
    }

    public GazeData GetLocalGazeData()
    {
        return currentGazeData;
    }

    public void Calibrate()
    {
        Debug.Log("Starting eye tracking calibration");
        eyeTracker.Calibrate();
    }

    // start with queueing tracking data
    public void StartRecording(string outputFileName)
    {
        if (!isRecording)
        {
            gazeTrackingQueue.Clear(); // hier oder in stop tracking
            
            objectTrackingFile = Path.Combine(Application.dataPath, outputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_head.csv");
            Debug.Log("Object tracking file " + objectTrackingFile);
            gazeTrackingFile = Path.Combine(Application.dataPath, outputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_gaze.csv");
            Debug.Log("Gaze tracking file " + gazeTrackingFile);
            
            isRecording = true;

            Debug.Log("Start recording: " + eyeTracker);
            
            // check if csv file already exists and change filename if necessary (e.g. _01...)
            int counter = 0;
            while (File.Exists(objectTrackingFile) || File.Exists(gazeTrackingFile))
            {
                counter++;
                objectTrackingFile = Path.Combine(Application.dataPath, outputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_" + counter.ToString("D2") + "_head.csv");
                Debug.Log("Object tracking file already exists. Changing filename to " + objectTrackingFile);

                gazeTrackingFile = Path.Combine(Application.dataPath, outputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_" + counter.ToString("D2") + "_gaze.csv");
            }
            WriteHeader();
            InvokeRepeating("Save", 0.0f, 1.0f); // save data to file every second
        }
    }
    
    // handle data of the event invoked by the eye tracker
    private void HandleData(GazeData gazeDate)
    {
        currentGazeData = gazeDate;
        if (isRecording)
        {
            gazeTrackingQueue.Enqueue(gazeDate);
        }
    }

    // stop the background saving of the tracking data
    public void StopRecording()
    {
        isRecording = false;
    }

    private void OnDisable()
    {
        WriteTrackingData();
        eyeTracker.StopListening();
        EyeTrackingEvent.OnDataAvailable -= HandleData;
    }
    
    // Write header for tracking files
    void WriteHeader()
    {
        // check if output folder exists
        if (!Directory.Exists(Path.Combine(Application.dataPath, outputFolder)))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, outputFolder));
        }
        StreamWriter sw = new StreamWriter(objectTrackingFile);

        // header for object tracking file
        string header = "timestamp,";
        header += "eye_timestamp,";

        foreach (TrackedObjectOptions trackedObject in trackedObjectList)
        {
            switch (trackedObject.trackingOptions)
            {
                case TrackingOptions.localTransform:
                    header += trackedObject.gameObject.name + "_localPosition.x,";
                    header += trackedObject.gameObject.name + "_localPosition.y,";
                    header += trackedObject.gameObject.name + "_localPosition.z,";
                    header += trackedObject.gameObject.name + "_localRotation.x,";
                    header += trackedObject.gameObject.name + "_localRotation.y,";
                    header += trackedObject.gameObject.name + "_localRotation.z,";
                    header += trackedObject.gameObject.name + "_localRotation.w,";
                    break;
                case TrackingOptions.globalTransform:
                    header += trackedObject.gameObject.name + "_position.x,";
                    header += trackedObject.gameObject.name + "_position.y,";
                    header += trackedObject.gameObject.name + "_position.z,";
                    header += trackedObject.gameObject.name + "_rotation.x,";
                    header += trackedObject.gameObject.name + "_rotation.y,";
                    header += trackedObject.gameObject.name + "_rotation.z,";
                    header += trackedObject.gameObject.name + "_rotation.w,";
                    break;
                default:
                    Debug.LogError("Unknown option selected for " + trackedObject.gameObject.name);
                    break;
            }
        }

        if (saveRaycastHitpoint)
        {
            header += "hit_object,";
            header += "hit_point.x,hit_point.y,hit_point.z,";
        }

        header += "messages,";
        sw.WriteLine(header);
        sw.Close();

        // header for gaze tracking file
        sw = new StreamWriter(gazeTrackingFile);
        header = "eye_timestamp,";
        header += "left_validata,";
        header += "left_eye_openness,";
        header += "left_eye_pupil_diameter,";
        header += "left_eye_origin.x,left_eye_origin.y,left_eye_origin.z,";
        header += "left_eye_gaze.x,left_eye_gaze.y,left_eye_gaze.z,";
        //header += "left_pupil_position.x,left_pupil_position.y,";
        header += "right_validata,";
        header += "right_eye_openness,";
        header += "right_eye_pupil_diameter,";
        header += "right_eye_origin.x,right_eye_origin.y,right_eye_origin.z,";
        header += "right_eye_gaze.x,right_eye_gaze.y,right_eye_gaze.z,";
        //header += "right_pupil_position.x,right_pupil_position.y,";
        header += "combined_eye_origin.x,combined_eye_origin.y,combined_eye_origin.z,";
        header += "combined_eye_gaze.x,combined_eye_gaze.y,combined_eye_gaze.z,";
        header += "gaze_distance,";

        sw.WriteLine(header);
        sw.Close();
    }

    public void WriteMessage(string msg)
    {
        msgBuffer = msg;
    }

    string GazeDataString(GazeData gazeDataSample)
    {
        StringBuilder datasetLine = new StringBuilder(350); // adjust capacity to your needs

        datasetLine.Append(gazeDataSample.deviceTimestamp.ToString() + ",");

        // left eye
        datasetLine.Append(gazeDataSample.leftValidity.ToString() + ",");
        datasetLine.Append(gazeDataSample.leftEyeOpenness.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.leftPupilDiameter.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.leftGazeRay.origin.x.ToString("F10") + "," + gazeDataSample.leftGazeRay.origin.y.ToString("F10") + "," + gazeDataSample.leftGazeRay.origin.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.leftGazeRay.direction.x.ToString("F10") + "," + gazeDataSample.leftGazeRay.direction.y.ToString("F10") + "," + gazeDataSample.leftGazeRay.direction.z.ToString("F10") + ",");
        //datasetLine.Append(gazeDataSample.leftPupilPosition.x.ToString("F10") + "," + gazeDataSample.leftPupilPosition.y.ToString("F10") + ",");

        // right eye
        datasetLine.Append(gazeDataSample.rightValidity.ToString() + ",");
        datasetLine.Append(gazeDataSample.leftEyeOpenness.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.rightPupilDiameter.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.rightGazeRay.origin.x.ToString("F10") + "," + gazeDataSample.rightGazeRay.origin.y.ToString("F10") + "," + gazeDataSample.rightGazeRay.origin.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.rightGazeRay.direction.x.ToString("F10") + "," + gazeDataSample.rightGazeRay.direction.y.ToString("F10") + "," + gazeDataSample.rightGazeRay.direction.z.ToString("F10") + ",");
        //datasetLine.Append(gazeDataSample.rightPupilPosition.x.ToString("F10") + "," + gazeDataSample.rightPupilPosition.y.ToString("F10") + ",");

        // combined eye
        datasetLine.Append(gazeDataSample.combinedGazeRay.origin.x.ToString("F10") + "," + gazeDataSample.combinedGazeRay.origin.y.ToString("F10") + "," + gazeDataSample.combinedGazeRay.origin.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.combinedGazeRay.direction.x.ToString("F10") + "," + gazeDataSample.combinedGazeRay.direction.y.ToString("F10") + "," + gazeDataSample.combinedGazeRay.direction.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.gazeDistance.ToString("F10") + ",");
        return (datasetLine.ToString());
    }

    void QueueTrackingData(Queue queue)
    {
        // StringBuilder should be quite effiction: https://stackoverflow.com/questions/21078/most-efficient-way-to-concatenate-strings
        StringBuilder datasetLine = new StringBuilder(700); // adjust capacity to your needs

        // timestamp: use time at beginning of frame
        datasetLine.Append(Time.time.ToString("F10") + ",");

        // eye tracking timestampe
        datasetLine.Append(currentGazeData.deviceTimestamp.ToString() + ",");

        foreach (TrackedObjectOptions trackedObject in trackedObjectList)
        {
            switch (trackedObject.trackingOptions)
            {
                case TrackingOptions.localTransform:
                    datasetLine.Append(trackedObject.gameObject.transform.localPosition.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localPosition.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localPosition.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.w.ToString("F10") + ",");
                    break;
                case TrackingOptions.globalTransform:
                    datasetLine.Append(trackedObject.gameObject.transform.position.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.position.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.position.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.w.ToString("F10") + ",");
                    break;
                default:
                    Debug.LogError("Unknown option selected for " + trackedObject.gameObject.name);
                    break;
            }
        }

        if (saveRaycastHitpoint)
        {
            datasetLine.Append(GazeRaycast());
        }

        // buffered message
        if (!String.IsNullOrEmpty(msgBuffer))
        {
            datasetLine.Append(msgBuffer + ",");
            msgBuffer = "";
        }
        queue.Enqueue(datasetLine.ToString());
    }

    public string GazeRaycast()
    {
        RaycastHit hit;
        Vector3 rayOrigin = Camera.main.transform.position + Camera.main.transform.rotation * currentGazeData.combinedGazeRay.origin;
        Vector3 rayDirection = Camera.main.transform.rotation * currentGazeData.combinedGazeRay.direction;

        if (Physics.Raycast(rayOrigin, rayDirection, out hit))
        {

            return hit.transform.name + "," + hit.point.x.ToString("F10") + "," + hit.point.y.ToString("F10") + "," + hit.point.z.ToString("F10") + ",";
        }
        else
        {
            return "NA,,,,";    
        }
    }

    void Save()
    {
        if (savingThread != null && savingThread.IsAlive)
        {
            Debug.Log("Previous saving thread is still running");
            return;
        }
        savingThread = new Thread(WriteTrackingData);
        savingThread.Start();
    }

    public void WriteTrackingData()
    {
        int counter = 0;
        StreamWriter sw;
        string datasetLine;

        if (isObjectRecording)
        {
            try
            {
                sw = new StreamWriter(objectTrackingFile, true); //true for append

                // dequeue trackingDataQueue until empty
                while (trackingDataQueue.Count > 0)
                {
                    datasetLine = trackingDataQueue.Dequeue().ToString();
                    counter++;
                    sw.WriteLine(datasetLine); // write to file
                }
                sw.Close(); // close file
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log it, display a message to the user, etc.)
                Console.WriteLine("An error occurred while writing to the file: " + ex.Message);
                // Optionally, you can log more detailed information about the exception:
                Console.WriteLine(ex.ToString());
            }
            //Debug.Log("Writing " + counter.ToString() + " lines of object tracking data");
        }

        if (isRecording)
        {
            try
            {
                sw = new StreamWriter(gazeTrackingFile, true); //true for append
                // dequeue gazeTrackingQueue until empty
                counter = 0;
                while (gazeTrackingQueue.Count > 0)
                {
                    datasetLine = GazeDataString(gazeTrackingQueue.Dequeue());
                    sw.WriteLine(datasetLine); // write to file
                    counter++;
                }
                sw.Close(); // close file
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log it, display a message to the user, etc.)
                Console.WriteLine("An error occurred while writing to the file: " + ex.Message);
                // Optionally, you can log more detailed information about the exception:
                Console.WriteLine(ex.ToString());
            }
            //Debug.Log("Writing " + counter.ToString() + " lines of gaze data");
        }
    }
}