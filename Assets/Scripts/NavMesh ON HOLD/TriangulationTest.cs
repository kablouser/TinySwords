using System.Collections.Generic;
using UnityEngine;

public class TriangulationTest : MonoBehaviour
{
    public List<Vector2> input;
    public List<Vector2> vertices;
    public List<int> triangles;

    public List<Vector2Int> constraintsInput;
    public List<Vector2Int> intersectingEdges;

    public List<int> adjacency;

    public int testRotateAroundTriangleIndex;

    public void OnDrawGizmos()
    {
        if (input == null || vertices == null || triangles == null || constraintsInput == null || intersectingEdges == null)
            return;
        /*        else if (triangles.Count != adjacency.Count)
                    return;*/

        // input
        {
            Gizmos.color = Color.green;
            foreach (var v in input)
            {
                Gizmos.DrawSphere(v, 0.1f);
            }
        }

        // output
        {
            Gizmos.color = Color.white;
            // verts
            foreach (var v in vertices)
            {
                Gizmos.DrawSphere(v, 0.15f);
            }

            // bounds
            /*            Gizmos.DrawLine(new Vector3(-1f, -1f), new Vector3(+1f, -1f));
                        Gizmos.DrawLine(new Vector3(+1f, -1f), new Vector3(+1f, +1f));
                        Gizmos.DrawLine(new Vector3(+1f, +1f), new Vector3(-1f, +1f));
                        Gizmos.DrawLine(new Vector3(-1f, +1f), new Vector3(-1f, -1f));*/

            // triangles
            if (triangles.Count % 3 != 0)
                Debug.LogAssertionFormat(this, $"triangles is not multiple of 3! => {triangles.Count}");

            Vector3Int drawTriangle = new Vector3Int();
            for (int i = 0; i < triangles.Count; i++)
            {
                int drawTriangleI = i % 3;
                int vertexIndex = triangles[i];
                if (!(0 <= vertexIndex && vertexIndex < vertices.Count))
                {
                    Debug.LogAssertionFormat(this, $"vertex index is wrong => triangles[{i}]={vertexIndex}");
                    drawTriangle[drawTriangleI] = -1;
                    continue;
                }

                drawTriangle[drawTriangleI] = vertexIndex;
                if (drawTriangleI == 2 &&
                    drawTriangle.x != -1 &&
                    drawTriangle.y != -1 &&
                    drawTriangle.z != -1)
                {
                    GizmosMore.DrawTriangle(vertices[drawTriangle.x],
                                            vertices[drawTriangle.y],
                                            vertices[drawTriangle.z]);
                }
            }
        }

        // constraints
        //if (scroll % 2 == 1)
        {
/*            foreach (var c in constraintsInput)
            {
                if (0 <= c.x && c.x < vertices.Count &&
                    0 <= c.y && c.y < vertices.Count)
                {
                    Gizmos.color = intersectingEdges.Contains(c) ?
                        Color.green : Color.red;
                    GizmosMore.DrawArrow(vertices[c.x], vertices[c.y]);
                }
            }*/

            foreach (var c in intersectingEdges)
            {
                if (0 <= c.x && c.x < vertices.Count &&
                    0 <= c.y && c.y < vertices.Count)
                {
                    Gizmos.color = Color.red;
                    GizmosMore.DrawArrow(vertices[c.x], vertices[c.y]);
                }
            }
        }
    }

    [ContextMenu("_Run")]
    public void Run()
    {
        //UnityEditor.Undo.RecordObject(this, "TriangulationTest");
        vertices = new List<Vector2>(input);
        if (triangles == null)
            triangles = new List<int>();
        //DelaunayTriangulation2D.Triangulate(output, triangles);
    }

    public int steps = 0;
    public int scroll = 0;
    public int scroll2 = 0;
    public bool scrollMode = false;

    public string currentT;

    public void Start()
    {
/*        int size = Random.Range(10,10) * 3;
        input = new List<Vector2>(size);
        for (int i = 0; i < size; ++i)
        {
            input.Add(new Vector2(Mathf.Round(Random.value * 4f - 2f), Mathf.Round(Random.value * 4f - 2f)));
        }*/

        /*        input = new List<Vector2> {
                    new Vector2(0, 0),            
                    new Vector2(1, 1),            
                    new Vector2(1, 0),

                    new Vector2(0, 2),
                    new Vector2(1, 2),
                    new Vector2(1, 2),

                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    new Vector2(1, 1),
        };*/
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            scrollMode = !scrollMode;
        }

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S))
        {
            if (Input.GetKeyDown(KeyCode.A))
                steps++;
            else
                steps--;

            vertices = new List<Vector2>(input);
            if (intersectingEdges == null)
                intersectingEdges = new List<Vector2Int>();
            if (adjacency == null)
                adjacency = new List<int>();
            DelaunayTriangulation2D.Triangulate(vertices, triangles, constraintsInput, steps, intersectingEdges, adjacency);
        }

        if (0.1f < Input.mouseScrollDelta.y)
        {
            if (Input.GetKey(KeyCode.LeftControl))
                scroll2--;
            else
                --scroll;
        }
        else if (Input.mouseScrollDelta.y < -0.1f)
        {
            if (Input.GetKey(KeyCode.LeftControl))
                scroll2++;
            else
                ++scroll;
        }        
    }
}
