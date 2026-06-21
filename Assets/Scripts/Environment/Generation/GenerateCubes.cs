using System.Collections;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

public class GenerateCubes : MonoBehaviour
{
    [SerializeField] private int size = 10;
    [SerializeField] private Material material;

    void Start()
    {

        CriarMesh();
    }

    private void CriarMesh()
    {
        Vector3 v0 = new Vector3(-0.5f, -0.5f, 0.5f);
        Vector3 v1 = new Vector3(0.5f, -0.5f, 0.5f);
        Vector3 v2 = new Vector3(0.5f, -0.5f, -0.5f);
        Vector3 v3 = new Vector3(-0.5f, -0.5f, -0.5f);
        Vector3 v4 = new Vector3(-0.5f, 0.5f, 0.5f);
        Vector3 v5 = new Vector3(0.5f, 0.5f, 0.5f);
        Vector3 v6 = new Vector3(0.5f, 0.5f, -0.5f);
        Vector3 v7 = new Vector3(-0.5f, 0.5f, -0.5f);

        CreateQuad(new[] { v4, v5, v1, v0 }, Vector3.forward);
        CreateQuad(new[] { v6, v7, v3, v2 }, Vector3.back);
        CreateQuad(new[] { v7, v6, v5, v4 }, Vector3.up);
        CreateQuad(new[] { v0, v1, v2, v3 }, Vector3.down);
        CreateQuad(new[] { v7, v4, v0, v3 }, Vector3.left);
        CreateQuad(new[] { v5, v6, v2, v1 }, Vector3.right);


        CombineQuads();
    }

    void CreateQuad(Vector3[] faceVertices, Vector3 normal)
    {
        Mesh mesh = new Mesh();
        mesh.vertices = faceVertices;
        mesh.triangles = new int[] { 3, 1, 0, 3, 2, 1 };
        mesh.normals = new Vector3[] { normal, normal, normal, normal };
        mesh.uv = new Vector2[] {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
        };

        GameObject quad = new GameObject("Quad");
        quad.transform.parent = transform;
        quad.AddComponent<MeshFilter>().mesh = mesh;
        quad.AddComponent<MeshRenderer>();
    }


    void CombineQuads()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];
        for (int i = 0; i < meshFilters.Length; i++)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
        }
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = new Mesh();
        mf.mesh.CombineMeshes(combine);
        MeshRenderer mr = gameObject.AddComponent<MeshRenderer>();
        mr.material = material;


        foreach (Transform child in transform)
            Destroy(child.gameObject);
    }

    void Update()
    {


        transform.Rotate(Vector3.up, 30f * Time.deltaTime);
    }


    IEnumerator InitializeCubes()
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.position = new Vector3(x, y, z);
                }
                yield return null;
            }
        }

    }

}
