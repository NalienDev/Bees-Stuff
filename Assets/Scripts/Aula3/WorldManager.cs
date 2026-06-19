using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Config;

/**
 * Gestor central do mundo.
 * ResponsÃ¡vel por carregar e descarregar chunks dinamicamente em torno do jogador,
 * utilizando corrotinas para distribuir a carga computacional e frustum culling para otimizar a renderizaÃ§Ã£o.
 */
public class WorldManager : MonoBehaviour
{
    [Header("ReferÃªncias")]
    [Tooltip("Transform do jogador, utilizado para rastrear a sua posiÃ§Ã£o no mundo")]
    public Transform player;
    [Tooltip("Prefab que contÃ©m o componente 'Chunk', instanciado para cada nova secÃ§Ã£o do mundo")]
    public GameObject chunkPrefab;
    [Tooltip("Material que contÃ©m o Atlas de texturas aplicado aos blocos")]
    public Material chunkMaterial;

    /* Mapeamento rÃ¡pido O(1) das coordenadas 2D para os GameObjects dos chunks instanciados no mundo. */
    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private Dictionary<Vector2Int, GameObject> sleepingChunks = new();

    /* Guarda a Ãºltima coordenada de chunk do jogador para recalcular apenas ao cruzar fronteiras. */
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    [Header("RenderizaÃ§Ã£o")]
    [Tooltip("NÃºmero mÃ¡ximo de chunks processados por frame.")]
    public int chunksPerFrame = 2;

    private Coroutine buildRoutine;

    private Camera mainCamera;
    private Plane[] frustumPlanes;
    [Tooltip("Se ativo, desativa a ocultaÃ§Ã£o de chunks fora da visÃ£o.")]
    public bool debug = false;

    void Awake()
    {
        mainCamera = Camera.main;
        // Inicializa o Atlas usando as imagens individuais da pasta Resources
        if (chunkMaterial != null)
        {
            Block.InitializeAtlas(chunkMaterial);
            // Substitui o material base pelo material gerado dinamicamente com o novo atlas
            chunkMaterial = Block.AtlasMaterial;
        }
        else
        {
            Debug.LogError("Atribui o chunkMaterial no Inspector para podermos extrair o Shader!");
        }
    }

    // Update â€” detectar mudanÃ§a de chunk do jogador
    void Update()
    {
        // atualizar o frustum da cÃ¢mara a cada frame
        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);

        Vector2Int current = GetPlayerChunk();

        // SÃ³ recalculamos se o jogador mudou de chunk.
        // Se o jogador se move dentro do mesmo chunk, nada acontece.
        if (current != lastPlayerChunk)
        {
            lastPlayerChunk = current;
            // Cancelar a coroutine anterior (se ainda estiver a correr)
            if (buildRoutine != null)
                StopCoroutine(buildRoutine);
            // Remover chunks fora do range (isto continua sÃ­ncrono)
            RemoveDistantChunks(current);

            // LanÃ§ar nova coroutine para gerar os novos
            buildRoutine = StartCoroutine(BuildChunks(GetNeededChunks(current)));
        }

        // verificar visibilidade de todos os chunks ativos a cada frame
        if (!debug) UpdateChunkVisibility();
    }

    /**
     * Oculta a renderizaÃ§Ã£o de chunks que se encontrem totalmente fora do campo de visÃ£o da cÃ¢mara.
     */
    private void UpdateChunkVisibility()
    {
        foreach (var kvp in activeChunks)
        {
            GameObject chunkObj = kvp.Value;
            if (chunkObj == null) continue;

            // criar bounds do chunk para testar contra o frustum
            Vector3 chunkWorldPos = chunkObj.transform.position;
            Bounds bounds = new Bounds(
                chunkWorldPos + new Vector3(chunkSize / 2f, chunkHeight / 2f, chunkSize / 2f),
                new Vector3(chunkSize, chunkHeight, chunkSize)
            );

            bool visible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
            chunkObj.GetComponent<MeshRenderer>().enabled = visible;
        }
    }

    /**
     * Descarrega da memÃ³ria e destrÃ³i os objetos dos chunks que ficaram demasiado distantes do jogador.
     * 
     * @param current: A coordenada do chunk em que o jogador se encontra.
     */
    private void RemoveDistantChunks(Vector2Int current)
    {
        HashSet<Vector2Int> needed = GetNeededChunks(current);
        List<Vector2Int> toSleep = new();
        List<Vector2Int> toDestroy = new();

        foreach (var kvp in activeChunks)
        {
            if (!needed.Contains(kvp.Key))
                toSleep.Add(kvp.Key);
        }

        foreach (var key in toSleep)
        {
            GameObject chunkObj = activeChunks[key];

            // desativar visual
            MeshRenderer mr = chunkObj.GetComponent<MeshRenderer>();
            MeshFilter mf = chunkObj.GetComponent<MeshFilter>();
            if (mr != null) mr.enabled = false;
            if (mf != null) { mf.mesh.Clear(); mf.mesh = null; }

            // mover para sleeping
            sleepingChunks[key] = chunkObj;
            activeChunks.Remove(key);
        }

        // destruir chunks demasiado distantes dos sleeping
        foreach (var kvp in sleepingChunks)
        {
            int dist = Mathf.Max(Mathf.Abs(kvp.Key.x - current.x), Mathf.Abs(kvp.Key.y - current.y));
            if (dist > renderDistance * 3)
                toDestroy.Add(kvp.Key);
        }

        foreach (var key in toDestroy)
        {
            if (WorldBeeManager.Instance != null)
                WorldBeeManager.Instance.OnChunkRemoved(key);

            Destroy(sleepingChunks[key]);
            sleepingChunks.Remove(key);
        }
    }

    /**
     * Calcula todos os chunks que devem ser processados em torno do jogador.
     * 
     * @param center: A coordenada central a partir da qual o raio Ã© gerado.
     * @return: Conjunto (HashSet) com as coordenadas que representam os chunks.
     */
    private HashSet<Vector2Int> GetNeededChunks(Vector2Int center)
    {
        // 1. Calcular o conjunto de chunks necessÃ¡rios
        //    Todos os chunks dentro de renderDistance do centro.
        HashSet<Vector2Int> needed = new();
        for (int cx = center.x - renderDistance; cx <= center.x + renderDistance; cx++)
            for (int cz = center.y - renderDistance; cz <= center.y + renderDistance; cz++)
            {
                needed.Add(new Vector2Int(cx, cz));
            }
        return needed;
    }


    /**
     * Transforma a posiÃ§Ã£o espacial 3D do jogador na coordenada 2D inteira do chunk respetivo.
     * 
     * @return: A coordenada bidimensional do chunk subjacente.
     */
    Vector2Int GetPlayerChunk()
    {
        Vector3 pos = player.position;
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize));
            // FloorToInt garante que coordenadas negativas funcionam correctamente.
            // Exemplo: x = -1 com chunkSize = 16 â†’ chunk -1 (nÃ£o 0).
    }

    // MÃ©todo auxiliar: aceder a um chunk por coordenada (Ãºtil para cross-chunk)
    public Chunk GetChunk(Vector2Int coord)
    {
        if (activeChunks.TryGetValue(coord, out GameObject go))
            return go.GetComponent<Chunk>();
        return null;
    }

    IEnumerator BuildChunks(HashSet<Vector2Int> needed)
    {
        List<Vector2Int> newChunks = new();

        // fase 1 â€” identificar chunks novos
        foreach (var coord in needed)
        {
            if (!activeChunks.ContainsKey(coord) && !sleepingChunks.ContainsKey(coord))
                newChunks.Add(coord);
        }

        // fase 1.5 â€” reativar sleeping chunks
        List<Vector2Int> toWake = new();
        foreach (var coord in needed)
        {
            if (sleepingChunks.ContainsKey(coord))
                toWake.Add(coord);
        }

        foreach (var coord in toWake)
        {
            GameObject existing = sleepingChunks[coord];
            MeshFilter mf = existing.GetComponent<MeshFilter>();
            MeshRenderer mr = existing.GetComponent<MeshRenderer>();

            if (mf.mesh == null)
                mf.mesh = new Mesh();
            if (mr != null) mr.enabled = true;

            Chunk chunk = existing.GetComponent<Chunk>();
            chunk.DrawChunk();

            activeChunks[coord] = existing;
            sleepingChunks.Remove(coord);
        }

        // fase 2 â€” instanciar GameObjects (main thread obrigatÃ³rio)
        Dictionary<Vector2Int, Chunk> chunksToGenerate = new();
        foreach (var coord in newChunks)
        {
            Vector3 worldPos = new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize);
            GameObject go = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
            go.name = $"Chunk_{coord.x}_{coord.y}";

            Chunk chunk = go.GetComponent<Chunk>();
            // inicializar apenas variÃ¡veis â€” sem gerar dados ainda
            chunk.worldOffset = coord;
            chunk.chunkMaterial = chunkMaterial;
            chunk.worldManager = this;
            chunk.chunkData = new Block[chunkSize, chunkHeight, chunkSize];

            activeChunks[coord] = go;
            chunksToGenerate[coord] = chunk;
        }

        // fase 3 â€” gerar chunkData em threads separadas
        var tasks = new List<System.Threading.Tasks.Task>();
        foreach (var kvp in chunksToGenerate)
        {
            Chunk chunk = kvp.Value;
            // capturar para a lambda
            tasks.Add(System.Threading.Tasks.Task.Run(() => chunk.GenerateChunkData()));
        }

        // aguardar todas as threads sem bloquear a main thread
        var allTasks = System.Threading.Tasks.Task.WhenAll(tasks);
        while (!allTasks.IsCompleted)
            yield return null;

        // verificar se houve erros
        if (allTasks.IsFaulted)
            Debug.LogError("Erro na geraÃ§Ã£o de chunks: " + allTasks.Exception);

        // fase 4 â€” construir meshes na main thread (Unity nÃ£o permite noutras threads)
        HashSet<Vector2Int> newChunkSet = new(newChunks);
        HashSet<Vector2Int> toRedraw = new();

        int count = 0;
        foreach (var coord in newChunks)
        {
            Chunk chunk = GetChunk(coord);
            if (chunk != null)
            {
                chunk.BuildCollisionMesh();
                chunk.DrawChunk();

                if (WorldBeeManager.Instance != null)
                    WorldBeeManager.Instance.OnChunkGenerated(chunk);
            }

            if (activeChunks.ContainsKey(coord + Vector2Int.left) && !newChunkSet.Contains(coord + Vector2Int.left)) toRedraw.Add(coord + Vector2Int.left);
            if (activeChunks.ContainsKey(coord + Vector2Int.right) && !newChunkSet.Contains(coord + Vector2Int.right)) toRedraw.Add(coord + Vector2Int.right);
            if (activeChunks.ContainsKey(coord + Vector2Int.up) && !newChunkSet.Contains(coord + Vector2Int.up)) toRedraw.Add(coord + Vector2Int.up);
            if (activeChunks.ContainsKey(coord + Vector2Int.down) && !newChunkSet.Contains(coord + Vector2Int.down)) toRedraw.Add(coord + Vector2Int.down);

            count++;
            if (count % chunksPerFrame == 0)
                yield return null;
        }

        count = 0;
        foreach (var coord in toRedraw)
        {
            Chunk chunk = GetChunk(coord);
            if (chunk != null)
                chunk.DrawChunk();
            count++;
            if (count % chunksPerFrame == 0)
                yield return null;
        }
    }
}
