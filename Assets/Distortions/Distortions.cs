using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class Distortions : MonoBehaviour
{
    // settings
    public bool active = false;
    public float multiplier = 1.0f;
    [Range(-50.0f, 50.0f)]
    public float displacementX = 0.0f; // displacelemnt of simulated lens center in degrees of visual field
    [Range(-50.0f, 50.0f)]
    public float displacementY = 0.0f; // displacelemnt of simulated lens center in degrees of visual field
                                       //public Shader ParametricDistortion;
    private Material distortionMaterial;
    private static Camera cam;
    [Range(0.5f, 10.0f)]
    public float magn = 1.0f;
    [Range(-0.5f, 0.5f)]
    public float radial = 0.0f;
    [Range(-0.5f, 0.5f)]
    public float asym = 0.0f;

    void Awake()
    {
        distortionMaterial = new Material(Shader.Find("Hidden/RadialDistortion"));
        // calculate FoV and scaling factors for transformation
        cam = gameObject.GetComponent<Camera>();
        print("Vertical FoV: " + cam.fieldOfView);
        print("Horizontal FoV: " + cam.fieldOfView * cam.aspect);
        float scalingX = multiplier * Mathf.Tan(Mathf.Deg2Rad * (cam.fieldOfView * cam.aspect / 2.0f));
        float scalingY = multiplier * Mathf.Tan(Mathf.Deg2Rad * (cam.fieldOfView / 2.0f));
        distortionMaterial.SetFloat("_XScaling", scalingX);
        distortionMaterial.SetFloat("_YScaling", scalingY);
        distortionMaterial.SetFloat("_XShift", Mathf.Tan(Mathf.Deg2Rad * displacementX));
        distortionMaterial.SetFloat("_YShift", Mathf.Tan(Mathf.Deg2Rad * displacementY));
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (active)
        {
            distortionMaterial.SetFloat("_XShift", Mathf.Tan(Mathf.Deg2Rad * displacementX));
            distortionMaterial.SetFloat("_YShift", Mathf.Tan(Mathf.Deg2Rad * displacementY));
            distortionMaterial.SetFloat("_radial", radial);
            distortionMaterial.SetFloat("_magn", magn);
            distortionMaterial.SetFloat("_asym", asym);

            Graphics.Blit(source, destination, distortionMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }
}