using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class Tetrahedron
{
    public Vector4[] v = new Vector4[4];

    public Vector4 this[int i]
    {
        set { this.v[i] = value; }
        get { return this.v[i]; }
    }
}


public class SlicedHypercube : MonoBehaviour {

    // The hyperplane is defined by: Dot(normal, v) = position.
    [SerializeField] Vector4 normal = new Vector4(0, 0, 0, 1);
    [SerializeField] float position = 0.0f;
    [SerializeField] bool autoShift = false;
    [SerializeField] float period = 5;
    [SerializeField] Vector2 positionRange = new Vector2(-4, 4);

    Vector4[] basis = new Vector4[3];

    // Tetrahedralization of 3D cube
    readonly Vector3[,] CubeTetrahedralization = new Vector3[5, 4]
    { { new Vector3(1,1,1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(-1,-1,1) },
      { new Vector3(1,1,-1), new Vector3(1,1,1), new Vector3(-1,1,-1), new Vector3(1,-1,-1) },
      { new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(1,-1,-1), new Vector3(-1,-1,1) },
      { new Vector3(-1,1,1), new Vector3(1,1,1), new Vector3(-1,1,-1), new Vector3(-1,-1,1) },
      { new Vector3(-1,-1,-1), new Vector3(-1,-1,1), new Vector3(-1,1,-1), new Vector3(1,-1,-1) } };

    List<Tetrahedron> tetrahedrons = new List<Tetrahedron>();

    // Take 3D slice of a given 4D tetrahedron and add to the lists
    void GetSlice(List<Vector3> v3s, List<int> triangles, List<Vector3> normals, Tetrahedron t)
    {
        // offset for indices
        int offset = v3s.Count;

        bool[] side = new bool[4];
        float[] pos = new float[4];
                
        // check which sides the points are w.r.t. the hyperplane
        for (int i=0; i<4; i++)
        {
            pos[i] = Vector4.Dot(normal, t[i]) - position;
            side[i] = (pos[i] > 0);
        }

        int count = 0;
        for (int i=0; i<3; i++)
        {
            for (int j=i+1; j<4; j++)
            {
                if (side[i] != side[j])
                {
                    // Edge intersects the hyperplane
                    Vector4 x4 = t[i] * pos[j] - t[j] * pos[i]; 
                    // Vector4.Dot(x4, normal) = (pos[i] + position) * pos[j] - (pos[j] + position) * pos[i]
                    //    = position * (pos[j] - pos[i])
                    x4 /= (pos[j] - pos[i]);

                    float a = Vector4.Dot(x4, basis[0]),
                        b = Vector4.Dot(x4, basis[1]),
                        c = Vector4.Dot(x4, basis[2]);
                    v3s.Add(new Vector3(a, b, c));
                    count++;
                    //Debug.LogFormat("{0},{1}: {2},{3}", i,j,t[i], t[j]);
                    //Debug.LogFormat("{0} -> {1}", x4, new Vector3(a, b, c));
                }
            }
        }
        
        // generically # of intersections are 0, 3 or 4
        // add both sides?
        if (count == 3)
        {
            Vector3 v0 = v3s[offset], v1 = v3s[offset + 1], v2 = v3s[offset + 2];
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);

            triangles.AddRange(new int[] { offset, offset + 1, offset + 2 });
            //triangles.AddRange(new int[] { offset, offset + 2, offset + 1 });
        }
        else if (count == 4)
        {
            // two vertices are in one side, the other two are in the other side
            Vector3 v0 = v3s[offset], v1 = v3s[offset + 1], v2 = v3s[offset + 2];
            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            // order of intersecting edges:
            //   0 and 3 are in the same side  ->  (0,1),(0,2),(1,3),(2,3)   1->0->2->3  not cyclic
            //   0 and 1 are in the same side  ->  (0,2),(0,3),(1,2),(1,3)   2->0->3->1  not cyclic
            //   0 and 2 are in the same side  ->  (0,1),(0,3),(1,2),(2,3)   1->0->3->2  not cyclic
            triangles.AddRange(new int[] { offset, offset + 1, offset + 3, offset, offset + 3, offset + 2 });
            //triangles.AddRange(new int[] { offset, offset +1, offset +3, offset, offset +3, offset +2,
            //                                offset, offset + 2, offset + 1, offset + 2, offset +1, offset +3});
        }
    }

    // Schmidt's orthonormalization
    // assuming normal is not in xyz-space
    void SetBasis()
    {
        Vector4 n = Vector4.Normalize(normal);

        // y direction
        Vector4 v = new Vector4(0, 1, 0, 0);
        v = v - Vector4.Dot(v, n) * n;
        basis[1] = Vector4.Normalize(v);

        // x direction
        v = new Vector4(1, 0, 0, 0);
        v = v - Vector4.Dot(v, n) * n - Vector4.Dot(v, basis[1]) * basis[1];
        basis[0] = Vector4.Normalize(v);

        // z direction
        v = new Vector4(0, 0, 1, 0);
        v = v - Vector4.Dot(v, n) * n - Vector4.Dot(v, basis[0]) * basis[0] - Vector4.Dot(v, basis[1]) * basis[1];
        basis[2] = Vector4.Normalize(v);
        //Debug.LogFormat("Basis 0: {0}, Basis 1: {1}, Basis 2: {2}", basis[0], basis[1], basis[2]);
    }

    // generate tetrahedralization of hypercube faces
    void SetTetrahedrons()
    {
        for (int i=0; i<4; i++)
        {
            for (int s=-1; s<=1; s+=2)
            {
                for (int j=0; j<5; j++)
                {
                    Tetrahedron t = new Tetrahedron();
                    for (int k=0; k<4; k++)
                    {
                        Vector4 v = new Vector4();
                        v[i] = s;
                        for (int l = 0; l < 3; l++) {
                            v[(i + l + 1) % 4] = CubeTetrahedralization[j,k][l];
                        }
                        t.v[k] = v;
                        //Debug.LogFormat("{0},{1},{2}:{3}", i, j, k, v);
                    }
                    tetrahedrons.Add(t);
                }
            }
        }
    }

    // Unused because 
    //  normal directions are not set
    Mesh CreateMeshFull()
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        List<Color> colors = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i < 4; i++)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    for (int l = -1; l <= 1; l += 2)
                    {
                        Vector4[] v = new Vector4[2];
                        v[0][i] = 1;
                        v[0][(i + 1) % 4] = j;
                        v[0][(i + 2) % 4] = k;
                        v[0][(i + 3) % 4] = l;
                        v[1] = v[0];
                        v[0][i] = -1;
                        float p0 = Vector4.Dot(v[0], normal) - position,
                            p1 = Vector4.Dot(v[1], normal) - position;
                        if (p0*p1 <= 0)
                        {
                            // segment (v[0],v[1]) intersects the hyperplane
                            Vector4 w = (p1 * v[0] - p0 * v[1]) / (p1 - p0);
                            float x = Vector4.Dot(w, basis[0]),
                                y = Vector4.Dot(w, basis[1]),
                                z = Vector4.Dot(w, basis[2]);
                            vertices.Add(new Vector3(x, y, z));
                            colors.Add(new Color((i & 1) * 0.5f + 0.5f, (i & 2) * 0.25f + 0.5f, 1.0f, 1.0f));
                            uvs.Add(new Vector2(0, 0));
                        }
                    }
                }
            }
        }

        for (int i=0; i<vertices.Count-2; i++)
        {
            for (int j=i+1; j<vertices.Count-1; j++)
            {
                for (int k=j+1; k<vertices.Count; k++)
                {
                    //triangles.AddRange(new int[6] { i, j, k, i, k, j });
                    triangles.AddRange(new int[3] { i, j, k });
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;

    }

    Mesh CreateMeshFrom3DFaces()
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        //List<Color> colors = new List<Color>();
        //List<Vector2> uvs = new List<Vector2>();

        for (int i=0; i<tetrahedrons.Count; i++)
        {
            GetSlice(vertices, triangles, normals, tetrahedrons[i]);
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);

        List<Color> colors = new List<Color>(vertices.Count);
        mesh.SetColors(colors);

        List<Vector2> uvs = new List<Vector2>(vertices.Count);
        mesh.SetUVs(0, uvs);

        mesh.RecalculateBounds();

        return mesh;

    }

    Mesh CreateMesh()
    {
        return CreateMeshFrom3DFaces();
        //return CreateMeshFull();
    }

    private void UpdateMesh()
    {
        Mesh mesh = CreateMesh();
        GetComponent<MeshFilter>().mesh = mesh;
    }


    // Use this for initialization
    void Start () {
        SetTetrahedrons();
        SetBasis();
        /* // for test
        Tetrahedron t = new Tetrahedron();
        t.v[0] = new Vector4(1, 0, 0, 0);
        t.v[1] = new Vector4(0, 1, 0, 0);
        t.v[2] = new Vector4(0, 0, 1, 0);
        t.v[3] = new Vector4(0, 0, 0, 1);
        tetrahedrons.Add(t);
        */
        UpdateMesh();
    }
	
	// Update is called once per frame
	void Update () {
		if (autoShift)
        {
            position = (Mathf.Sin(Time.time*2.0f*Mathf.PI/period) + 1.0f) / 2.0f * (positionRange[1] - positionRange[0]) + positionRange[0];
            UpdateMesh();
        }
    }

    private void OnValidate()
    {
        SetBasis();
        UpdateMesh();

    }
}
