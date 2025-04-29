//using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
// using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using System.IO;
using System.Globalization;


public class DotManager : MonoBehaviour
{
    [Header("Dot parameters")]
    public const int nDots = 3000;  
    public float size = 1f;  
    public Color dotColor = Color.white;
    [Range (0f, 1f)]
    public float alpha = 0f;
    public Mesh mesh;
    public bool active = false;
    public bool resampleAll = false;
    public bool reprojectAll = false;
    public Vector3 distortionParam = new Vector3(1f,0f,0f);
    public enum DotSpace{
        plane,
        sphere
    }
    public DotSpace dotspace = DotSpace.sphere;

    [Header("Scene parameters")]
    public Color backgroundColor = Color.gray;
    public bool showScene = false;
    public bool showFixationTarget = false;
    public float cutoff = 80f;
    public bool loadFiles = true; 
    Vector3 initCamPos;
    Vector3[] initDots;
    Vector4[] dotPositions;
    GameObject scene;
    /// class variables
    ComputeBuffer positionBuffer;
    ComputeBuffer meshTriangles;
    ComputeBuffer meshPositions;
    Bounds bounds;

    private Material dotMaterial;
    private Distortions distortionManager;

    struct invDistArray {
        public double[,] array;
        public string name;
    }

    void Awake()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(SystemInfo.graphicsShaderLevel);

        // set background color
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = backgroundColor;

        // get scene
        scene = GameObject.Find("Scene");  
        scene.SetActive(true);

        // get distortion manager
        distortionManager = GetComponent<Distortions>(); // assume same GaneObject for distortions

        // create random dots and project them on scene
        // generate random positions of dots
        initDots = RandomDots(nDots);
        Debug.Log("First random dot:");
        Debug.Log(initDots[0]);
        
        Physics.queriesHitBackfaces = true; // for hitting back/inside of object
        // calculate 3d position of dots by projecting them on the scene
        Vector2[] invDots = InverseDistortion(initDots);

        Debug.Log("After inv dist:");
        Debug.Log(invDots[0]);

        dotPositions = ProjectOnScene(invDots);
        Debug.Log("After projection:");
        Debug.Log(dotPositions[0]);
        // set buffer with dots' scene positions 
        positionBuffer = new ComputeBuffer(nDots, sizeof(float) * 4);
        positionBuffer.SetData(dotPositions);

        // set buffer with dots' mesh variables
        int[] triangles = mesh.triangles;
        meshTriangles = new ComputeBuffer(triangles.Length, sizeof(int));
        meshTriangles.SetData(triangles);
        Vector3[] positions = mesh.vertices;
        meshPositions = new ComputeBuffer(positions.Length, sizeof(float) * 3);
        meshPositions.SetData(positions);

        // prepare material and give data to shaders
        dotMaterial = new Material(Shader.Find("Hidden/DotShader"));
        dotMaterial.SetBuffer("SphereLocations", positionBuffer);
        dotMaterial.SetBuffer("Triangles", meshTriangles);
        dotMaterial.SetBuffer("Positions", meshPositions);
        dotMaterial.SetColor("_Color", dotColor);
        dotMaterial.SetFloat("_Size", size);
        dotMaterial.SetFloat("_Alpha", alpha);

        //bounds for frustum culling (20 is a magic number (radius) from the compute shader)
        bounds = new Bounds(Vector3.zero, Vector3.one * 50);
    }

    void OnValidate(){
        // reset cam position and resample all points
        if (resampleAll){
            Resample(nDots);
            resampleAll = false;
        }
        if (reprojectAll){
            Reproject();
            reprojectAll = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (active)
        {
            dotMaterial.SetFloat("_Size", size);
            dotMaterial.SetFloat("_Cutoff", cutoff);
            dotMaterial.SetVector("_DistortionParam", distortionParam);
            dotMaterial.SetFloat("_Alpha", alpha);
            Graphics.DrawProcedural(dotMaterial, bounds, MeshTopology.Triangles, meshTriangles.count, nDots);
            
            scene.SetActive(showScene);
        }
    }

   void OnDestroy()
    {
        // Release the compute buffer
        Debug.Log("Dispose Buffers.");
        positionBuffer.Dispose();
        meshTriangles.Dispose();
        meshPositions.Dispose();
    }

    // returns n dots using a uniform distribution in the defined dotSpace (e.g. plane or sphere surface)
    Vector3[] RandomDots(int n)
    {
        Vector3[] dots = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if(dotspace == DotSpace.sphere)
            {
                // Create points on sphere surface
                // Restrict z (cylinder radius) to limited range
                float z = Mathf.Sin(Mathf.Deg2Rad * 11.31f) + (Random.value * (1 - Mathf.Sin(Mathf.Deg2Rad * 11.31f))); 
                float phi = 2.0f * Mathf.PI * Random.value;
                dots[i] = new Vector3(Mathf.Cos(phi) * Mathf.Sqrt(1-z*z) , Mathf.Sin(phi) * Mathf.Sqrt(1-z*z) , z);
            }
            else
            {
                dots[i] = new Vector3(-3f + 6f * Random.value, -2f + 4f * Random.value,1f); // random point on x-y-plane with distance z=1
            }
        }
        if (showFixationTarget)
        {
            dots[dots.Length - 1] = new Vector3(0f, 0f, 1f); // center point as fixation target
        }
        return dots;
    }

    // interpolate inverse distortion of dot sample
    Vector2[] InverseDistortion(Vector3[] dots){
        Vector2[] dotsDistorted = new Vector2[dots.Length];

        float magn = distortionParam.x;
        float radial = distortionParam.y;
        float assym = distortionParam.z;

        for (int i = 0; i < dots.Length; i++){
            dotsDistorted[i].x = dots[i].x / dots[i].z;
            dotsDistorted[i].y = dots[i].y / dots[i].z;

            // calculate inverse distortion
            float r_sq = dotsDistorted[i].x* dotsDistorted[i].x + (dotsDistorted[i].y - assym) * (dotsDistorted[i].y - assym);
            dotsDistorted[i].x /= (magn + radial * r_sq);
            dotsDistorted[i].y /= (magn + radial * r_sq);
            if (i == dots.Length -1)
            {
                Debug.Log("Inverse Distortion:");
                Debug.Log(dotsDistorted[i]);
            }
        }
        return dotsDistorted;
    }

    //for every point in the array dots, projects the point into the scene and returns the 3d positions
    Vector4[] ProjectOnScene(Vector2[] dots)
    {
        Vector4[] dots4d = new Vector4[dots.Length];
        scene.SetActive(true);
        for (int i = 0; i < dots.Length; i++)
        {   
            RaycastHit hit;
            // dots can be projected in camera direction or always in z direction
            // camera direction:
            // Ray rayFromDot = new Ray(transform.position, transform.rotation * new Vector3(dots[i].x,dots[i].y,1)); // assumes this component to be attached to camera already
            // z direction:
            Ray rayFromDot = new Ray(transform.position, new Vector3(dots[i].x,dots[i].y,1));
            if (Physics.Raycast(rayFromDot, out hit))
            {
                // add 4th value to indicate color (white or black)
                dots4d[i] = new Vector4(hit.point.x, hit.point.y, hit.point.z, Random.Range(0f,0.66f)); // 0 to 0.33 is white, 0.33 to 0.66 is black
                if ((i==dots.Length-1) && (showFixationTarget))
                {
                    dots4d[i] = new Vector4(hit.point.x, hit.point.y, hit.point.z, 1.0f); // center point as fixation target
                }
            }
        }
        return dots4d;
    }

    Vector4[] AddColorValue(Vector3[] dots){
        Vector4[] dots4d = new Vector4[dots.Length];
        for (int i = 0; i < dots.Length; i++){
            dots4d[i] = new Vector4(dots[i].x, dots[i].y, dots[i].z, Random.Range(0,2));
        }

        return dots4d;
    }

    // Selecting dots and asigning new random positions to them 
    public void Resample(int nDots = nDots)
    {
        Vector3[] pos = RandomDots(nDots);
        Vector2[] invPos = InverseDistortion(pos);
        Vector4[] projectedPos = ProjectOnScene(invPos);
        dotPositions = projectedPos;
        positionBuffer.SetData(dotPositions);
    }

    public void Resample(int[] dotIndex)
    {
        Vector3[] pos = RandomDots(dotIndex.Length);
        Vector2[] invPos = InverseDistortion(pos);
        Vector4[] projectedPos = ProjectOnScene(invPos);
        
        for (int i = 0; i<dotIndex.Length; i++)
        {
            initDots[dotIndex[i]] = pos[i];
            dotPositions[dotIndex[i]] = projectedPos[i];
        }
        positionBuffer.SetData(dotPositions);
    }

    public void Reproject()
    {
        Vector2[] invPos = InverseDistortion(initDots);
        Vector4[] projectedPos = ProjectOnScene(invPos);
        positionBuffer.SetData(projectedPos);
    }

    void LoadAllBinaryFiles(string[] filePaths, List<TextAsset> binFiles)
    {
        foreach (string filePath in filePaths){
            string fileName = Path.GetFileNameWithoutExtension(filePath); 
            ResourceRequest resourceRequest = Resources.LoadAsync<TextAsset>("inverseDistortions/" + fileName);
            binFiles.Add(resourceRequest.asset as TextAsset);
            Debug.Log("Loaded: " + resourceRequest.asset.name);
        }
    }

    void ReadAllBinaryFiles(List<TextAsset> binarys, List<invDistArray> invDistArrays)
    {
        int rows = 1024;
        int cols = 1024;
        int i = 0;
        foreach (var bin in binarys)
        {
            i++;
            invDistArray arr;
            arr.name = bin.name;
            arr.array = ReadInverseDistortionData(bin, rows, cols);  
            invDistArrays.Add(arr);
            Debug.Log("Progress: " + i + " of " + binarys.Count);
        }
    }

    double[,] ReadInverseDistortionData(TextAsset textAsset, int rows, int cols)
    {
        double[,] dataArray = new double[rows, cols];
       
        // Read the file using FileStream and BinaryReader
        using (MemoryStream memoryStream = new MemoryStream(textAsset.bytes))
        using (BinaryReader reader = new BinaryReader(memoryStream))
        {
            // Get the total number of elements in the array
            int totalElements = (int)(memoryStream.Length / sizeof(double));

            // Read the binary data into the array
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    dataArray[i, j] = reader.ReadDouble();
                }
            }
        }
        // Debug.Log(dataArray[0, 0]);
        // Debug.Log(dataArray[1, 0]);
        // Debug.Log(dataArray[0, 1]);
        return dataArray;
    }


    (double [,],double[,]) ReadInverseDistortionData(float magn, float rad, float assym, int rows, int cols)
    {
        double[,] dataArrayX = new double[rows, cols];
        double[,] dataArrayY = new double[rows, cols];
        
        // Format the float variables to strings with two decimal places
        string mStr = magn.ToString("F2", CultureInfo.InvariantCulture);
        string rStr = rad.ToString("F2", CultureInfo.InvariantCulture);
        string aStr = assym.ToString("F2",CultureInfo.InvariantCulture);

        // Construct the file path string
        string filePath = string.Format("Assets/Resources/inverseDistortions/x_inverse_m{0}r{1}a{2}.bin", mStr, rStr, aStr);
        // Read the file using FileStream and BinaryReader
        using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fileStream))
        {
        // Get the total number of elements in the array
        int totalElements = (int)(fileStream.Length / sizeof(double));

        // Read the binary data into the array
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                dataArrayX[i, j] = reader.ReadDouble();
            }
        }
        }

        // Construct the file path string
        filePath = string.Format("Assets/Resources/inverseDistortions/y_inverse_m{0}r{1}a{2}.bin", mStr, rStr, aStr);
        // Read the file using FileStream and BinaryReader
        using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fileStream))
        {
        // Get the total number of elements in the array
        int totalElements = (int)(fileStream.Length / sizeof(double));

        // Read the binary data into the array
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                dataArrayY[i, j] = reader.ReadDouble();
            }
        }
        }
        return (dataArrayX, dataArrayY);
    }

    Vector2[] BilinearInterpolation(double[,] x_inverse, double[,] y_inverse, double[] x_range, double[] y_range, Vector2[] points)
    {   
        Vector2[] points_inverse = new Vector2[points.Length]; // return variable
        // get number of points in the input mesh
        int num_points_x = x_inverse.GetLength(1);
        int num_points_y = x_inverse.GetLength(0);
        // calculate distance between mesh points
        double dx = (x_range[1] - x_range[0]) / (num_points_x - 1);
        double dy = (y_range[1] - y_range[0]) / (num_points_y - 1);
        double grid_area = 1/(dx*dy);
        //TODO check if xs and ys have same length
        for(int i = 0; i < points.Length; i++)
        {
            double xs = points[i].x;
            double ys = points[i].y;
            //For a given sampling point (xs,ys), we calculate the indices of the surrounding points in the input mesh
            int x_index = (int)((xs - x_range[0]) / dx);
            int y_index = (int)((ys - y_range[0]) / dy);
            //Assert.IsTrue(x_index < num_points_x-1);
            //Assert.IsTrue(y_index < num_points_y-1);
            //x_index = max(0, min(x_index, num_points_x - 2))
            //y_index = max(0, min(y_index, num_points_y - 2))
            double x1 = x_range[0] + x_index * dx;
            double x2 = x_range[0] + (x_index+1) * dx;
            double y1 = y_range[0] + y_index * dy;
            double y2 = y_range[0] + (y_index+1) * dy;
            
            float x_inv = (float)(grid_area * ( x_inverse[x_index,y_index] * (x2 - xs) * (y2 - ys)
                                              + x_inverse[x_index + 1,y_index]     * (xs - x1) * (y2 - ys)
                                              + x_inverse[x_index,y_index + 1]     * (x2 - xs) * (ys - y1)
                                              + x_inverse[x_index + 1,y_index + 1] * (xs - x1) * (ys - y1)));
            float y_inv = (float)(grid_area * ( y_inverse[x_index,y_index] * (x2 - xs) * (y2 - ys)
                                              + y_inverse[x_index + 1,y_index]     * (xs - x1) * (y2 - ys)
                                              + y_inverse[x_index,y_index + 1]     * (x2 - xs) * (ys - y1)
                                              + y_inverse[x_index + 1,y_index + 1] * (xs - x1) * (ys- y1))); 

            // Check whether the inverse lands further outside than allowed
            if (Mathf.Sqrt(x_inv*x_inv+y_inv*y_inv) > Mathf.Tan(Mathf.Deg2Rad * 85)){
                x_inv = 0f;
                y_inv = 0f;
            }

            points_inverse[i].x = x_inv;
            points_inverse[i].y = y_inv;
        }
        return points_inverse;
    }   
}