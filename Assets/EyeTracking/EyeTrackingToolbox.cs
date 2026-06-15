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

public class EyeTrackingToolbox : MonoBehaviour
{
    public static EyeTrackingToolbox Instance { get; private set; }
    private static float unityTimestamp; // Each thread has access to static variables, so that the timestamp Time.time can be written to the variable unityTimestamp and we can use this as a timestamp even if the function is called in another thread.
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
    private Transform mainCamTransform; // cached Camera.main.transform
    public KeyCode calibrateKey = KeyCode.C;

    private GazeData currentGazeData; // for gaze sample of the current frame
    public Queue<GazeData> gazeTrackingQueue { get; private set; } // TODO: Can it be used publicly? Or should it be private?

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
    public string OutputFolder   { get; private set; } // folder for the recording output files

    [Header("Object Tracking Settings")]
    // List to hold the variables with dropdown options and associated GameObjects
    [SerializeField] private List<TrackedObjectOptions> trackedObjectList = new List<TrackedObjectOptions>();


    private string objectTrackingFile; // output file for object tracking (bound to framerate)
    private string gazeTrackingFile; // output file for eye tracking data (bound to eye tracking frequency)
    Queue trackingDataQueue = new Queue();
    static string msgBufferHead = "";
    static string msgBufferGaze = "";

    private bool isObjectRecording = true;
    public bool isRecording {get; private set;} = false;
    private Thread savingThread; // background thread for writing to files

    void Awake()
    {
        // set US culture for number formatting in strings
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
        
        if (Instance == null)
        {
            Instance = this;
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
                eyeTracker = gameObject.AddComponent<DummyEyeTracker>(); // add DummyEyeTracker as component so that Update/Coroutine is called
                eyeTracker.Initialize();
                break;
#if USE_VIVE
            case ETProvider.HTCViveSRanipal:
                // start SRanipal // todo, should be part of the interface I guess ???
                //Instance.AddComponent<SRanipal_Eye_Framework>();
       
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
                eyeTracker = gameObject.AddComponent<DummyEyeTracker>(); // add DummyEyeTracker as component so that Update/Coroutine is called
                eyeTracker.Initialize();
                break;
        }

        // event handler for gaze data
        EyeTrackingEvent.OnDataAvailable += HandleData; // subscribe to event

        // set US culture for number formatting in strings
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");

        // set default output folder
        OutputFolder = null;
    }

    public void SetOutputFolder(string folder)
    {
        OutputFolder = folder;
    }

    private void Start()
    {
        mainCamTransform = Camera.main.transform;
        eyeTracker.StartListening(); // start the background event system
    }

    void Update()
    {
        unityTimestamp = Time.time;
        // TODO change to get-function
        //currentGazeData = eyeTracker.GetGazeData();
        if (Input.GetKeyDown(calibrateKey))
        {
            Calibrate();
        }

        if (isRecording && isObjectRecording)
        {
            QueueTrackingData(trackingDataQueue);
        }
    }

    public GazeData GetGazeData()
    {
        currentGazeData.leftRayWorld = new Ray(mainCamTransform.TransformPoint(currentGazeData.leftRayLocal.origin), mainCamTransform.TransformDirection(currentGazeData.leftRayLocal.direction));
        currentGazeData.rightRayWorld = new Ray(mainCamTransform.TransformPoint(currentGazeData.rightRayLocal.origin), mainCamTransform.TransformDirection(currentGazeData.rightRayLocal.direction));
        currentGazeData.combinedRayWorld = new Ray(mainCamTransform.TransformPoint(currentGazeData.combinedRayLocal.origin), mainCamTransform.TransformDirection(currentGazeData.combinedRayLocal.direction));
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
            trackingDataQueue.Clear();
            if (OutputFolder == null)
            {
                Debug.LogError("Output folder not set. Please set the output folder with SetOutputFolder(string folder) before starting the recording.");
            }
            if (Path.HasExtension(outputFileName))
            {
                objectTrackingFile = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(outputFileName) + "_head.csv");
                gazeTrackingFile = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(outputFileName) + "_gaze.csv");
            }
            else
            {
                objectTrackingFile = Path.Combine(OutputFolder, outputFileName + "_head.csv");
                gazeTrackingFile = Path.Combine(OutputFolder, outputFileName + "_gaze.csv");
            }
            Debug.Log("Object tracking file " + objectTrackingFile);
            Debug.Log("Gaze tracking file " + gazeTrackingFile);
            
            isRecording = true;

            Debug.Log("Start recording: " + eyeTracker);
            
            // check if csv file already exists and change filename if necessary (e.g. _01...)
            int counter = 0;
            while (File.Exists(objectTrackingFile) || File.Exists(gazeTrackingFile))
            {
                counter++;
                objectTrackingFile = Path.Combine(OutputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_" + counter.ToString("D2") + "_head.csv");
                Debug.Log("Object tracking file already exists. Changing filename to " + objectTrackingFile);
                gazeTrackingFile = Path.Combine(OutputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_" + counter.ToString("D2") + "_gaze.csv");
            }
            WriteHeader();
            StartBackgroundWriter();
        }
    }
    
    // handle data of the event invoked by the eye tracker
    private void HandleData(GazeData gazeData)
    {
        // gazeDate.unityTimestamp = Time.time; // set Unity timestamp for the current frame
        gazeData.unityTimestamp = unityTimestamp; // If the static event is called from another thread, it appears that this function is called from this thread and does not have access to Time.time, so we use the static variable here.
        currentGazeData = gazeData;
        
        if (isRecording)
        {
            gazeTrackingQueue.Enqueue(gazeData);
        }
    }

    // stop the background saving of the tracking data
    public void StopRecording()
    {
        isRecording = false;

        // Signal the background writer to flush & exit
        writerThreadRunning = false;
        writerWakeUp.Set();

        if (savingThread != null && savingThread.IsAlive)
        {
            savingThread.Join();
        }

        Debug.Log("Stopped Recording");
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        // Stop the background writer if still running
        if (writerThreadRunning)
        {
            writerThreadRunning = false;
            writerWakeUp.Set();
            if (savingThread != null && savingThread.IsAlive)
            {
                savingThread.Join();
            }
        }

        if (eyeTracker != null)
        {
            eyeTracker.StopListening();
        }
        if (EyeTrackingEvent.HasSubscribers())
        {
            EyeTrackingEvent.OnDataAvailable -= HandleData;
        }
    }

    // Re-cache Camera.main after a scene change (old camera is destroyed).
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (Camera.main != null) mainCamTransform = Camera.main.transform;
    }
    
    // Write header for tracking files
    private void WriteHeader()
    {
        // check if output folder exists
        if (!Directory.Exists(OutputFolder))
        {
            Directory.CreateDirectory(OutputFolder);
        }
        StreamWriter sw = new StreamWriter(objectTrackingFile);

        // header for object tracking file
        string header = "unity_timestamp,";
        header += "eye_timestamp,";

        foreach (TrackedObjectOptions trackedObject in trackedObjectList)
        {
            if(trackedObject.gameObject == null)
            {
                header += ",,,,,,,"; // add 7 empty cells, object seems to be missing
            }
            else
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
        header = "unity_timestamp,";
        header += "eye_timestamp,";
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
        header += "messages,";

        sw.WriteLine(header);
        sw.Close();
    }

    public void WriteMessage(string msg)
    {
        msgBufferHead = msg;
        msgBufferGaze = msg;
    }

    // Reusable StringBuilder for gaze data, allocated once (reduces GC pressure at 120Hz)
    private StringBuilder gazeStringBuilder = new StringBuilder(512);

    private string GazeDataString(GazeData gazeDataSample)
    {
        gazeStringBuilder.Clear();
        var ci = invariantCulture;

        gazeStringBuilder.Append(gazeDataSample.unityTimestamp.ToString("F10", ci)).Append(',');
        gazeStringBuilder.Append(gazeDataSample.deviceTimestamp).Append(',');

        // left eye
        gazeStringBuilder.Append(gazeDataSample.leftValidity).Append(',');
        gazeStringBuilder.Append(gazeDataSample.leftEyeOpenness.ToString("F10", ci)).Append(',');
        gazeStringBuilder.Append(gazeDataSample.leftPupilDiameter.ToString("F10", ci)).Append(',');
        AppendVector3(gazeStringBuilder, gazeDataSample.leftRayLocal.origin, ci);
        AppendVector3(gazeStringBuilder, gazeDataSample.leftRayLocal.direction, ci);

        // right eye
        gazeStringBuilder.Append(gazeDataSample.rightValidity).Append(',');
        gazeStringBuilder.Append(gazeDataSample.rightEyeOpenness.ToString("F10", ci)).Append(','); // BUGFIX: was writing leftEyeOpenness here
        gazeStringBuilder.Append(gazeDataSample.rightPupilDiameter.ToString("F10", ci)).Append(',');
        AppendVector3(gazeStringBuilder, gazeDataSample.rightRayLocal.origin, ci);
        AppendVector3(gazeStringBuilder, gazeDataSample.rightRayLocal.direction, ci);

        // combined
        AppendVector3(gazeStringBuilder, gazeDataSample.combinedRayLocal.origin, ci);
        AppendVector3(gazeStringBuilder, gazeDataSample.combinedRayLocal.direction, ci);
        gazeStringBuilder.Append(gazeDataSample.gazeDistance.ToString("F10", ci)).Append(',');

        // Append buffered message (or empty cell) - mirrors the head-tracking logic
        // so both CSVs have a 'messages' column with consistent field counts.
        if (!String.IsNullOrEmpty(msgBufferGaze))
        {
            gazeStringBuilder.Append(msgBufferGaze).Append(',');
            msgBufferGaze = "";
        }
        else
        {
            gazeStringBuilder.Append(','); // empty message cell
        }

        return gazeStringBuilder.ToString();
    }

    private static void AppendVector3(StringBuilder sb, Vector3 v, System.Globalization.CultureInfo ci)
    {
        sb.Append(v.x.ToString("F10", ci)).Append(',');
        sb.Append(v.y.ToString("F10", ci)).Append(',');
        sb.Append(v.z.ToString("F10", ci)).Append(',');
    }

    // Reusable StringBuilder, allocated once instead of every frame (reduces garbage collection pauses).
    private StringBuilder reusableStringBuilder = new StringBuilder(700);
    // Explicit culture for number formatting, prevents any regional formatting issues (e.g. German commas vs. English dots).
    private static readonly System.Globalization.CultureInfo invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

    private void QueueTrackingData(Queue queue)
    {
        // Reuse the same StringBuilder across frames to reduce pressure.
        reusableStringBuilder.Clear();

        // timestamp: use time at beginning of frame
        reusableStringBuilder.Append(Time.time.ToString("F10", invariantCulture)).Append(',');

        // eye tracking timestamp
        reusableStringBuilder.Append(currentGazeData.deviceTimestamp).Append(',');

        foreach (TrackedObjectOptions trackedObject in trackedObjectList)
        {
            if (trackedObject.gameObject == null)
            {
                reusableStringBuilder.Append(",,,,,,,"); // add 7 empty cells, object seems to be missing
            }
            else
            {
                // Cache transform reference, accessing .transform repeatedly is slower than caching it.
                Transform t = trackedObject.gameObject.transform;
                Vector3 pos;
                Quaternion rot;

                if (trackedObject.trackingOptions == TrackingOptions.localTransform)
                {
                    pos = t.localPosition;
                    rot = t.localRotation;
                }
                else // globalTransform
                {
                    pos = t.position;
                    rot = t.rotation;
                }

                reusableStringBuilder.Append(pos.x.ToString("F10", invariantCulture)).Append(',');
                reusableStringBuilder.Append(pos.y.ToString("F10", invariantCulture)).Append(',');
                reusableStringBuilder.Append(pos.z.ToString("F10", invariantCulture)).Append(',');
                reusableStringBuilder.Append(rot.x.ToString("F10", invariantCulture)).Append(',');
                reusableStringBuilder.Append(rot.y.ToString("F10", invariantCulture)).Append(',');
                reusableStringBuilder.Append(rot.z.ToString("F10", invariantCulture)).Append(',');
                reusableStringBuilder.Append(rot.w.ToString("F10", invariantCulture)).Append(',');
            }
        }

        if (saveRaycastHitpoint)
        {
            reusableStringBuilder.Append(GazeRaycast());
        }

        // Buffered message, always append the column (empty or with message)
        // so that every row has the same number of fields (fixes CSV inconsistency).
        if (!String.IsNullOrEmpty(msgBufferHead))
        {
            reusableStringBuilder.Append(msgBufferHead).Append(',');
            msgBufferHead = "";
        }
        else
        {
            reusableStringBuilder.Append(',');
        }
        queue.Enqueue(reusableStringBuilder.ToString());
    }

    public string GazeRaycast()
    {
        RaycastHit hit;
        Vector3 rayOrigin = mainCamTransform.position + mainCamTransform.rotation * currentGazeData.combinedRayLocal.origin;
        Vector3 rayDirection = mainCamTransform.rotation * currentGazeData.combinedRayLocal.direction;

        if (Physics.Raycast(rayOrigin, rayDirection, out hit))
        {

            return hit.transform.name + "," + hit.point.x.ToString("F10") + "," + hit.point.y.ToString("F10") + "," + hit.point.z.ToString("F10") + ",";
        }
        else
        {
            return "NA,,,,";    
        }
    }

    private volatile bool writerThreadRunning = false;
    private System.Threading.ManualResetEventSlim writerWakeUp = new System.Threading.ManualResetEventSlim(false);

    // Start a single long-lived background writer thread instead of creating one per second.
    // Thread creation is expensive (1-10ms); this avoids periodic main-thread stalls.
    private void StartBackgroundWriter()
    {
        if (writerThreadRunning) return;
        writerThreadRunning = true;
        savingThread = new Thread(BackgroundWriterLoop) { IsBackground = true, Name = "EyeTracker-Writer" };
        savingThread.Start();
    }

    // Runs on the background thread. Sleeps 1s between checks, drains the queues when woken.
    private void BackgroundWriterLoop()
    {
        while (writerThreadRunning)
        {
            WriteTrackingData();
            // Sleep for up to 1 second, but wake early if asked to stop
            writerWakeUp.Wait(1000);
            writerWakeUp.Reset();
        }
        // Final flush on shutdown
        WriteTrackingData();
    }

    private void WriteTrackingData()
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