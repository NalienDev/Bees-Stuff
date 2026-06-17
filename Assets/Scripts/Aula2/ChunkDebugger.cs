using UnityEngine;
using static Config;

public class ChunkDebugger : MonoBehaviour
{
    [SerializeField] private Material chunkMaterial;
    void Start()
    {
        CreateChunk(new Vector2Int(0, 0));
        CreateChunk(new Vector2Int(1, 0));
    }
    void CreateChunk(Vector2Int offset)
    {
        GameObject go = new GameObject($"Chunk_{offset.x}_{offset.y}");
        go.transform.position = new Vector3(
        offset.x * chunkSize, 0, offset.y * chunkSize);
        Chunk chunk = go.AddComponent<Chunk>();
        chunk.Initialize(offset, chunkMaterial);
    }
}
