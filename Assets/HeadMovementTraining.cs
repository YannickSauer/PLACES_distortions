using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeadMovementTraining : MonoBehaviour
{
    public GameObject GUI; // the capsule that will be moved
    public float GUIDistance = 2.0f; // distance from the camera to the capsule
    
    private GameObject headIndicator; // the head indicator object

    void Start()
    {
        // find HeadIndicator as child of GUI
        headIndicator = GUI.transform.Find("HeadIndicator").gameObject; 
        // set the GUI position to be in front of the camera
        
    }

    void RepositionGUI()
    {
        if (Camera.main != null)
        {
            Vector3 headPosition = Camera.main.transform.position;
            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height

            GUI.transform.position = headPosition + headForward * GUIDistance;
            GUI.transform.rotation = Quaternion.LookRotation(headForward);

        }
        else
        {
            Debug.LogError("Main Camera not found. Please ensure there is a camera tagged as 'MainCamera'.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // if k is pressed
        if (Input.GetKeyDown(KeyCode.K))
        {
            RepositionGUI();
        }
        // Update the head indicator position to match the camera's position
        if (Camera.main != null)
        {
            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height
            Vector3 indicatorPosition = headIndicator.transform.position;
            indicatorPosition.x = headForward.x * GUIDistance;
            headIndicator.transform.position = indicatorPosition;
        }
    }
}
