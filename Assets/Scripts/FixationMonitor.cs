using UnityEngine;

/// <summary>
/// Experimenter-only monitor: Shows on the Desktop Game-Window whether the participant
/// is currently fixating the fixation target. NOT visible inside the HMD.
///
/// Attach this to any GameObject in the SwimTest scene (e.g. the SceneTestManager).
/// </summary>
public class FixationMonitor : MonoBehaviour
{
    [Header("Tolerance")]
    [Tooltip("Maximum angle (in degrees) between gaze direction and fixation-target direction to count as 'fixating'.")]
    public float fixationToleranceDeg = 3.0f;

    [Header("Display")]
    [Tooltip("Show the on-screen indicator only when SwimTest scene is active and a trial is running.")]
    public bool onlyDuringTrials = true;

    private EyeTrackingToolbox eyeTracker;
    private bool isFixating = false;
    private float currentAngleDeg = 0f;
    private bool hasValidGaze = false;

    void Start()
    {
        eyeTracker = EyeTrackingToolbox.Instance;
        if (eyeTracker == null)
        {
            Debug.LogError("[FixationMonitor] EyeTrackingToolbox.Instance not found.");
            enabled = false;
        }
    }

    void Update()
    {
        if (eyeTracker == null) return;

        GazeData gaze = eyeTracker.GetGazeData();

        // Skip when gaze data is invalid (eyes closed, no tracking).
        hasValidGaze = gaze.leftValidity && gaze.rightValidity;
        if (!hasValidGaze) return;

        // Fixation target world position is set by DotManager and SwimTest:
        // SwimTest.initHeadPosition + Vector3.forward * wallDistance (4f).
        Vector3 fixationWorldPos = SwimTest.initHeadPosition + Vector3.forward * 4f;

        // Direction from eye origin to fixation target (in world space).
        Vector3 gazeOrigin = gaze.combinedRayWorld.origin;
        Vector3 idealDir = (fixationWorldPos - gazeOrigin).normalized;

        // Current gaze direction (world space).
        Vector3 gazeDir = gaze.combinedRayWorld.direction.normalized;

        // Angle between ideal direction and current gaze direction.
        currentAngleDeg = Vector3.Angle(idealDir, gazeDir);
        isFixating = currentAngleDeg <= fixationToleranceDeg;
    }

    // OnGUI draws on the Editor Game-Window only, NOT into the HMD eye textures.
    // This makes it a perfect experimenter-only monitor.
    void OnGUI()
    {
        if (eyeTracker == null) return;

        // Optional: hide indicator outside trials based on a simple heuristic.
        // (Kept always-on by default; toggle 'onlyDuringTrials' in inspector if you want.)

        const int boxW = 600;
        const int boxH = 60;
        int x = 10;
        int y = 10;

        // Background box
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Box(new Rect(x, y, boxW, boxH), GUIContent.none);
        GUI.color = Color.white;

        // Status label
        string statusText;
        Color statusColor;

        if (!hasValidGaze)
        {
            statusText = "EYES: no tracking";
            statusColor = Color.yellow;
        }
        else if (isFixating)
        {
            statusText = $"FIXATING  ({currentAngleDeg:F1}°)";
            statusColor = Color.green;
        }
        else
        {
            statusText = $"OFF TARGET  ({currentAngleDeg:F1}°)";
            statusColor = Color.red;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = statusColor }
        };

        GUI.Label(new Rect(x, y, boxW, boxH), statusText, style);
    }
}