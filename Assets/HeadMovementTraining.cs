using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class HeadMovementTraining : MonoBehaviour
{

    // number of good trials to complete the training
    public float wallDistance = 2.0f; // distance from the camera to the capsule
    public int minGoodTrials = 10; // number of good trials to complete the training
    public float maxHeadTurnAngle = 10.0f; // maximum head turn angle in degrees
    public float timingThreshold = 0.2f; // time window to consider a head movement as correct
    private GameObject headIndicator; // the head indicator object
    private Vector3 initHeadForward; // initial forward direction of the head
    private GameObject scene;
    private GameObject wall;
    private GameObject GUI; // the capsule that will be moved


    // controller flags
    private bool fireLeftPressed = false;
    private bool fireRightPressed = false;
    private bool touchpadPressed = false;
    void Start()
    {
        scene = GameObject.Find("Scene");
        wall = GameObject.Find("Wall");
        GUI = GameObject.Find("UI");
        // find HeadIndicator as child of GUI
        headIndicator = GUI.transform.Find("HeadIndicator").gameObject;
        // set the GUI position to be in front of the camera
        RepositionScene();
        ScaleGUI();
    }

    void ScaleGUI()
    {
        // position targetLeft and targetRight to indicate the max amplitude for the head rotation depending on current maxHeadTurnAngle
        GameObject targetLeft = GUI.transform.Find("TargetLeft").gameObject;
        GameObject targetRight = GUI.transform.Find("TargetRight").gameObject;

        float headTargetPos = wallDistance * Mathf.Tan(maxHeadTurnAngle * Mathf.Deg2Rad);

        targetLeft.transform.localPosition = new Vector3(-headTargetPos, 0, 0);
        targetRight.transform.localPosition = new Vector3(headTargetPos, 0, 0);
        // scale the bar to reach from targetLeft to targetRight
        GameObject bar = GUI.transform.Find("Bar").gameObject;
        bar.transform.localScale = new Vector3(2 * headTargetPos, bar.transform.localScale.y, bar.transform.localScale.z);

    }

    void RepositionScene()
    {
        if (Camera.main != null)
        {
            Vector3 headPosition = Camera.main.transform.position;
            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height
            initHeadForward = headForward.normalized;
            scene.transform.position = headPosition + headForward * wallDistance;
            scene.transform.rotation = Quaternion.LookRotation(headForward);

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
            RepositionScene();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            StartCoroutine(RunTraining());
        }

        UpdateHeadIndicator();
    }

    void UpdateHeadIndicator()
    {
        // Update the head indicator position to match the camera's position
        if (Camera.main != null)
        {
            Vector3 headForward = Camera.main.transform.forward;
            headForward.y = 0; // Keep the GUI at head height
            // get the horizontal angle between initHeadForward and headForward
            float angle = Vector3.SignedAngle(initHeadForward, headForward, Vector3.up);
            // position the headIndicator based on the angle
            headIndicator.transform.localPosition = new Vector3(wallDistance * Mathf.Tan(angle * Mathf.Deg2Rad), 0, 0);
        }
    }

    public IEnumerator RunTraining()
    {
        // start the metronome
        Metronome metronome = GetComponent<Metronome>();
        if (metronome != null)
        {
            metronome.StartMetronome();
        }
        else
        {
            Debug.LogError("Metronome component not found on the GameObject.");
        }


        int goodTrial = 0;
        TMP_Text instructionText = GameObject.Find("Text").GetComponent<TMP_Text>();
        instructionText.text = "Correct head movements: " + goodTrial.ToString();

        //if (eyeTracker != null)
        //{
        //            eyeTracker.WriteMessage("StartTrainingTrial" + aftereffectData.currentTrial);
        //}

        // save initial rotation of the camera

        // wait for full head rotation left or right
        yield return new WaitUntil(() => Mathf.Abs(GetYawRotation()) > maxHeadTurnAngle);
        // show head target animation
        if (GetYawRotation() > 0f) // if the head is rotated to the right
        {
            AnimateRightTarget();
        }
        else
        {
            AnimateLeftTarget();
        }


        if ((Time.time - metronome.GetLastBeatTime() < timingThreshold) || (metronome.GetNextBeatTime() - Time.time < timingThreshold))
        {
            // if the participant moved in the right time, then increase the good trial counter
            goodTrial++;
            instructionText.text = "Correct head movements: " + goodTrial.ToString();

        }
        else
        {
            goodTrial = 0;
            instructionText.text = "Correct head movements: " + goodTrial.ToString();
        }

        while (goodTrial < minGoodTrials)
        {
            if (GetYawRotation() > 0f) // if the head is rotated to the right
            {
                // wait for head to rotate left
                yield return new WaitUntil(() => GetYawRotation() < -maxHeadTurnAngle);
                AnimateLeftTarget();
            }
            else // if the head is rotated to the left
            {

                // wait for head to rotate right
                yield return new WaitUntil(() => GetYawRotation() > maxHeadTurnAngle);
                AnimateRightTarget();
            }
            if ((Time.time - metronome.GetLastBeatTime() < timingThreshold) || (metronome.GetNextBeatTime() - Time.time < timingThreshold))
            {
                // if the participant moved in the right time, then increase the good trial counter
                goodTrial++;
                instructionText.text = "Correct head movements: " + goodTrial.ToString();

            }
            else
            {
                // if the participant moved in the wrong time, then reset the good trial counter
                goodTrial = 0;
                instructionText.text = "Correct head movements: " + goodTrial.ToString();
            }
        }
        metronome.StopMetronome();





        Debug.Log("Headmovement training completed.");

        //if (eyeTracker != null)
        //{
        //            eyeTracker.StopRecording();
        //      }

    }

    private float GetYawRotation()
    {
        Vector3 headForward = Camera.main.transform.forward;
        headForward.y = 0; // Keep the GUI at head height
        // get the horizontal angle between initHeadForward and headForward
        float deltaAngle = Vector3.SignedAngle(initHeadForward, headForward, Vector3.up);
        return deltaAngle;
    }
    private void AnimateRightTarget()
    {
        // animate the targetRight object to scale up and down
        GameObject targetRight = GUI.transform.Find("TargetRight").gameObject;
        StartCoroutine(ScaleUpDown(targetRight));
    }

    private void AnimateLeftTarget()
    {
        // animate the targetLeft object to scale up and down
        GameObject targetLeft = GUI.transform.Find("TargetLeft").gameObject;
        StartCoroutine(ScaleUpDown(targetLeft));
    }

    // scaleUpDown coroutine
    private IEnumerator ScaleUpDown(GameObject target)
    {
        Color oldColor = target.GetComponent<Renderer>().material.color;
        Vector3 originalScale = target.transform.localScale;
        Vector3 targetScale = originalScale * 3.0f;
        float duration = 0.1f;
        float elapsed = 0f;


        // scale up
        while (elapsed < duration)
        {
            target.transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            // lerp color between oldColor and green
            target.GetComponent<Renderer>().material.color = Color.Lerp(oldColor, Color.green, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.transform.localScale = targetScale;

        // scale down
        elapsed = 0f;
        while (elapsed < duration)
        {
            target.transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            target.GetComponent<Renderer>().material.color = Color.Lerp(Color.green, oldColor, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.transform.localScale = originalScale;
        target.GetComponent<Renderer>().material.color = oldColor;
    }
    
    private void OnFireLeft() // called by the fire left button of the controller
    {
        fireLeftPressed = true;
    }

    private void OnFireRight() // called by the fire right button of the controller
    {
        fireRightPressed = true;
    }

    private void OnTouchpad() // called by the touchpad of the controller
    {
        touchpadPressed = true;
    }

}
