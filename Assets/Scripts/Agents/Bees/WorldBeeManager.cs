using UnityEngine;
using System.Collections.Generic;
using static Config;

/**
 * Ponte entre o mundo voxel e o sistema de abelhas.
 * Escaneia chunks gerados a procura de blocos HIVE e FLOWER,
 * instancia os GameObjects correspondentes e gere a lista global de flores.
 */
public class WorldBeeManager : MonoBehaviour
{
    public static WorldBeeManager Instance { get; private set; }

    [Header("Prefabs")]
    [Tooltip("Prefab com BeeSpawner + GameHiveManager")]
    public GameObject hivePrefab;
    [Tooltip("Prefab invisivel com FlowerController - sem mesh, so logica")]
    public GameObject flowerControllerPrefab;

    // Objetos instanciados por chunk, para limpeza quando o chunk e removido
    private Dictionary<Vector2Int, List<GameObject>> chunkObjects = new();

    // Lista global de flores acessivel a todas as abelhas
    private List<FlowerController> allFlowers = new();
    public List<FlowerController> AllFlowers => allFlowers;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /**
     * Chamado pelo WorldManager apos um chunk ser gerado e desenhado.
     * Escaneia o chunkData a procura de HIVE e FLOWER e instancia os GameObjects.
     */
    public void OnChunkGenerated(Chunk chunk)
    {
        Vector2Int coord = chunk.worldOffset;

        // Evitar processar o mesmo chunk duas vezes (ex: reativacao de sleeping chunk)
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
            // Spawn das abelhas iniciais e feito no Start() do BeeSpawner
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

    /**
     * Chamado pelo WorldManager quando um chunk e removido permanentemente.
     * Destroi os GameObjects associados e limpa a lista global de flores.
     */
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

    /**
     * Destroi o GameObject de uma flor especifica a partir da sua posicao global no mundo,
     * removendo-a tambem das listas internas do manager.
     */
    public void DestroyFlowerAt(Vector3 worldPos)
    {
        FlowerController fc = allFlowers.Find(f => 
            f != null && Vector3Int.RoundToInt(f.transform.position) == Vector3Int.RoundToInt(worldPos));

        if (fc == null) return;

        allFlowers.Remove(fc);

        // Remover tambem do chunkObjects para que OnChunkRemoved nao tente limpa-lo novamente
        foreach (var list in chunkObjects.Values)
        {
            if (list.Remove(fc.gameObject))
                break;
        }

        Destroy(fc.gameObject);
    }
}
