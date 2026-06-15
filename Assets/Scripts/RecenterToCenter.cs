using UnityEngine;


/// Repositions the XR Rig at scene start 
public class RecenterToCenter : MonoBehaviour
{
    [Header("Target Position in the Virtual Room")]
    [Tooltip("Where the participant's head should be after recentering (world coordinates). Y is ignored - physical HMD height is kept.")]
    public Vector3 targetHeadPosition = new Vector3(0f, 0f, 0f);

    [Header("References")]
    [Tooltip("The Main Camera (head). If left empty, Camera.main is used.")]
    public Transform headCamera;

    [Header("Controls")]
    public KeyCode recenterKey = KeyCode.R;
    public bool recenterOnStart = true;

    void Start()
    {
        if (headCamera == null && Camera.main != null)
            headCamera = Camera.main.transform;

        if (recenterOnStart)
        {
            // Delay one frame 
            StartCoroutine(RecenterNextFrame());
        }
    }

    private System.Collections.IEnumerator RecenterNextFrame()
    {
        yield return null; // wait one frame for tracking to settle
        Recenter();
    }

    void Update()
    {
        if (Input.GetKeyDown(recenterKey))
        {
            Recenter();
        }
    }

    public void Recenter()
    {
        if (headCamera == null)
        {
            Debug.LogError("[RecenterToCenter] No head camera assigned and Camera.main is null.");
            return;
        }

        // Move the rig so the head lands on targetHeadPosition.
        // Keep the head's own Y (height) so we don't push the participant into the floor/ceiling.
        // Facing direction is left untouched (position only).
        Vector3 headPos = headCamera.position;
        Vector3 offset = targetHeadPosition - headPos;
        offset.y = 0f; // do not change vertical height; the HMD height is physical
        transform.position += offset;

        Debug.Log($"[RecenterToCenter] Recentered (position only). Head now at {headCamera.position}");
    }
}