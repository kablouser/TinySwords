using System.Collections.Generic;
using UnityEngine;

public class TriangulationTest : MonoBehaviour
{
    public List<Vector2> vertices;
    public List<Vector2> output;
    public List<int> triangles;

    public void OnDrawGizmos()
    {
        if (vertices == null || output == null)
            return;

        // input
        {
            Gizmos.color = Color.green;
            foreach (var v in vertices)
            {
                Gizmos.DrawSphere(v, 0.1f);
            }
        }

        // output
        {
            Gizmos.color = Color.red;
            // verts
            foreach (var v in output)
            {
                Gizmos.DrawSphere(v, 0.15f);
            }

            // bounds
            Gizmos.DrawLine(new Vector3(-1f, -1f), new Vector3(+1f, -1f));
            Gizmos.DrawLine(new Vector3(+1f, -1f), new Vector3(+1f, +1f));
            Gizmos.DrawLine(new Vector3(+1f, +1f), new Vector3(-1f, +1f));
            Gizmos.DrawLine(new Vector3(-1f, +1f), new Vector3(-1f, -1f));

            // triangles
            if (triangles.Count % 3 != 0)
                Debug.LogAssertionFormat(this, $"triangles is not multiple of 3! => {triangles.Count}");

            Vector3Int drawTriangle = new Vector3Int();            
            for (int i = 0; i < triangles.Count; i++)
            {
                int drawTriangleI = i % 3;
                int vertexIndex = triangles[i];
                if (!(0 <= vertexIndex && vertexIndex < output.Count))
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
                    Gizmos.DrawLine(output[drawTriangle.x], output[drawTriangle.y]);
                    Gizmos.DrawLine(output[drawTriangle.y], output[drawTriangle.z]);
                    Gizmos.DrawLine(output[drawTriangle.z], output[drawTriangle.x]);
                }
            }
        }
    }

    [ContextMenu("_Run")]
    public void Run()
    {
        UnityEditor.Undo.RecordObject(this, "TriangulationTest");
        output = new List<Vector2>(vertices);
        if (triangles == null)
            triangles = new List<int>();

        DelaunayTriangulation2D.Triangulate(output, triangles);
    }
}
