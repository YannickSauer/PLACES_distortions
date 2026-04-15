using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeadIndicator : MonoBehaviour
{
    private SwimTest testManager;
    // Start is called before the first frame update
    void Start()
    {
        testManager = GameObject.Find("SceneTestManager").GetComponent<SwimTest>();
    }

    // Update is called once per frame
    void Update()
    {
         // Update the head indicator position to match the camera's position
        if (Camera.main != null)
        {
            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height
            // get the horizontal angle between initHeadForward and headForward
            float angle = Vector3.SignedAngle(testManager.GetInitHeadForward(), headForward, Vector3.up);
            // position the headIndicator based on the angle
            this.transform.localPosition = new Vector3(testManager.wallDistance * Mathf.Tan(angle * Mathf.Deg2Rad), 0, 0);
        }
    }
}
