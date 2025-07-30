using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ArrowGenerator : MonoBehaviour
{
    public float stemLength;
    public float stemWidth;
    public float tipLength;
    public float tipWidth;
    public float turnByDeg = 180f;

    [System.NonSerialized]
    public List<Vector3> verticesList;
    [System.NonSerialized]
    public List<int> trianglesList;

    Mesh mesh;

    void OnEnable()
    {
        SwimTest.OnTurnHead += TurnAroundZ;
    }

    void OnDisable()
    {
        SwimTest.OnTurnHead -= TurnAroundZ;
    }

    void Start()
    {
        //make sure Mesh Renderer has a material
        mesh = new Mesh();
        this.GetComponent<MeshFilter>().mesh = mesh;
        GenerateArrow();
    }

    void Update()
    {
        // GenerateArrow();
        if (Input.GetKeyDown(KeyCode.R))
        {
            TurnAroundZ();
        }
    }

    //arrow is generated starting at Vector3.zero
    //arrow is generated facing right, towards radian 0.
    void GenerateArrow()
    {
        //setup
        verticesList = new List<Vector3>();
        trianglesList = new List<int>();

        //stem setup
        Vector3 stemOrigin = Vector3.zero;
        float stemHalfWidth = stemWidth / 2f;
        //Stem points
        verticesList.Add(stemOrigin + (stemHalfWidth * Vector3.down));
        verticesList.Add(stemOrigin + (stemHalfWidth * Vector3.up));
        verticesList.Add(verticesList[0] + (stemLength * Vector3.right));
        verticesList.Add(verticesList[1] + (stemLength * Vector3.right));

        //Stem triangles
        trianglesList.Add(0);
        trianglesList.Add(1);
        trianglesList.Add(3);

        trianglesList.Add(0);
        trianglesList.Add(3);
        trianglesList.Add(2);

        //tip setup
        Vector3 tipOrigin = stemLength * Vector3.right;
        float tipHalfWidth = tipWidth / 2;

        //tip points
        verticesList.Add(tipOrigin + (tipHalfWidth * Vector3.up));
        verticesList.Add(tipOrigin + (tipHalfWidth * Vector3.down));
        verticesList.Add(tipOrigin + (tipLength * Vector3.right));

        //tip triangle
        trianglesList.Add(4);
        trianglesList.Add(6);
        trianglesList.Add(5);

        //assign lists to mesh.
        mesh.vertices = verticesList.ToArray();
        mesh.triangles = trianglesList.ToArray();
    }

    public void TurnAroundZ()
    {
        this.transform.Rotate(0f,0f,turnByDeg) ;
    }
}