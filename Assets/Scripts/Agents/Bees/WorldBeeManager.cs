using UnityEngine;
using System.Collections.Generic;
using static Config;


public class WorldBeeManager : MonoBehaviour
{
    public static WorldBeeManager Instance { get; private set; }

    [Header("Prefabs")]
    [Tooltip("Prefab com BeeSpawner + GameHiveManager")]
    public GameObject hivePrefab;
    [Tooltip("Prefab invisivel com FlowerController - sem mesh, so logica")]
    public GameObject flowerControllerPrefab;


    private Dictionary<Vector2Int, List<GameObject>> chunkObjects = new();


    private List<FlowerController> allFlowers = new();
    public List<FlowerController> AllFlowers => allFlowers;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }


    public void OnChunkGenerated(Chunk chunk)
    {
        Vector2Int coord = chunk.worldOffset;


        if (chunkObjects.ContainsKey(coord)) return;

        List<GameObject> spawned = new();
        chunkObjects[coord] = spawned;

        Block[,,] data = chunk.chunkData;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    Block.BlockType type = data[x, y, z].type;
                    if (type != Block.BlockType.HIVE && type != Block.BlockType.FLOWER)
                        continue;

                    Vector3 worldPos = chunk.transform.position + new Vector3(x, y, z);

                    if (type == Block.BlockType.HIVE)
                    {
                        SpawnHive(worldPos, spawned);
                    }
                    else if (type == Block.BlockType.FLOWER)
                    {
                        SpawnFlower(worldPos, spawned);
                    }
                }
            }
        }
    }

    private void SpawnHive(Vector3 worldPos, List<GameObject> spawned)
    {
        if (hivePrefab == null) return;

        GameObject hiveGO = Instantiate(hivePrefab, worldPos, Quaternion.identity);
        spawned.Add(hiveGO);

        GameHiveManager hiveManager = hiveGO.GetComponent<GameHiveManager>();
        BeeSpawner beeSpawner = hiveGO.GetComponent<BeeSpawner>();

        if (beeSpawner != null)
        {
            beeSpawner.hiveTransform = hiveGO.transform;
            beeSpawner.hiveManager = hiveManager;

        }

        Debug.Log($"WorldBeeManager: Colmeia instanciada em {worldPos}");
    }

    private void SpawnFlower(Vector3 worldPos, List<GameObject> spawned)
    {
        if (flowerControllerPrefab == null) return;

        GameObject flowerGO = Instantiate(flowerControllerPrefab, worldPos, Quaternion.identity);
        spawned.Add(flowerGO);

        FlowerController fc = flowerGO.GetComponent<FlowerController>();
        if (fc != null)
            allFlowers.Add(fc);
    }


    public void OnChunkRemoved(Vector2Int coord)
    {
        if (!chunkObjects.TryGetValue(coord, out List<GameObject> objects)) return;

        foreach (var go in objects)
        {
            if (go == null) continue;

            FlowerController fc = go.GetComponent<FlowerController>();
            if (fc != null) allFlowers.Remove(fc);

            Destroy(go);
        }

        chunkObjects.Remove(coord);
    }


    public void DestroyFlowerAt(Vector3 worldPos)
    {
        FlowerController fc = allFlowers.Find(f =>
            f != null && Vector3Int.RoundToInt(f.transform.position) == Vector3Int.RoundToInt(worldPos));

        if (fc == null) return;

        allFlowers.Remove(fc);


        foreach (var list in chunkObjects.Values)
        {
            if (list.Remove(fc.gameObject))
                break;
        }

        Destroy(fc.gameObject);
    }
}
