using StarterAssets;
using UnityEngine;
using static Config;

/**
 * Classe que faz o tratamento de interações entre o jogador e o mundo (ex.: colocação e destruição de blocos).
 */
public class BlockInteraction : MonoBehaviour
{
    [Tooltip("Referência a WorldManager. Necessária para aceder aos chunks já carregados.")]
    public WorldManager worldManager;
    [Tooltip("Distância máxima (em blocos) que o jogador consegue interagir.")]
    public float maxDistance = 6f;
    /* Componente que capta o input do jogador. */
    private StarterAssetsInputs _input;
    [Tooltip("Tipo de bloco que o jogador poderá colocar no mundo.")]
    public Block.BlockType placeType = Block.BlockType.DIRT;
    [Tooltip("Highlight que indica o bloco para o qual o jogador está a apontar.")]
    public Transform highlightCube;
    private Block.BlockType[] palette = {
        Block.BlockType.DIRT,
        Block.BlockType.STONE,
        Block.BlockType.GRASS
    };
    private int currentIndex = 0;

    [Header("Mining Settings")]
    public float baseMiningTime = 0.5f;
    private float miningProgress = 0f;
    private Vector3 currentTargetPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    
    private GameObject breakOverlay;
    private Material breakMaterial;
    private Texture2D[] destroyTextures;
    
    private GameObject crossMeshOverlay;
    private GameObject crossMeshHighlight;
    
#if ENABLE_INPUT_SYSTEM
    private UnityEngine.InputSystem.PlayerInput _playerInput;
#endif

    private void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
#endif

        // Setup Break Overlay
        breakOverlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(breakOverlay.GetComponent<Collider>());
        breakOverlay.transform.localScale = new Vector3(1.02f, 1.02f, 1.02f); // Slightly larger to prevent Z-fighting
        
        breakMaterial = new Material(Shader.Find("Unlit/Transparent"));
        breakOverlay.GetComponent<MeshRenderer>().material = breakMaterial;
        breakOverlay.SetActive(false);

        // Load Crack Textures
        destroyTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
        {
            destroyTextures[i] = Resources.Load<Texture2D>("Destroy/destroy_stage_" + i);
            if (destroyTextures[i] != null)
            {
                // Force Point filtering at runtime to guarantee no blur!
                destroyTextures[i].filterMode = FilterMode.Point; 
            }
        }

        // Setup Cross-Mesh Overlays
        crossMeshOverlay = new GameObject("CrossMeshOverlay");
        MeshFilter cmMeshFilter = crossMeshOverlay.AddComponent<MeshFilter>();
        cmMeshFilter.mesh = BuildCrossMesh();
        MeshRenderer cmMeshRenderer = crossMeshOverlay.AddComponent<MeshRenderer>();
        cmMeshRenderer.material = breakMaterial;
        crossMeshOverlay.SetActive(false);

        crossMeshHighlight = new GameObject("CrossMeshHighlight");
        MeshFilter cmhMeshFilter = crossMeshHighlight.AddComponent<MeshFilter>();
        cmhMeshFilter.mesh = BuildCrossMesh();
        MeshRenderer cmhMeshRenderer = crossMeshHighlight.AddComponent<MeshRenderer>();
        if (highlightCube != null && highlightCube.GetComponent<MeshRenderer>() != null)
        {
            cmhMeshRenderer.material = highlightCube.GetComponent<MeshRenderer>().sharedMaterial;
        }
        crossMeshHighlight.SetActive(false);
    }

    /**
     * Ciclo principal que atualiza a posição do cursor de interação (highlightCube) e deteta os inputs de interação.
     */
    private void Update()
    {
        PickBlock();
        HighlightBlock();
        DetectAction();
    }
    private void DetectAction()
    {
        bool isHoldingBreak = _input.breakBlock;

#if ENABLE_INPUT_SYSTEM
        // Read the real-time held status directly from the Input Action to fix the stuck button bug
        if (_playerInput != null)
        {
            isHoldingBreak = _playerInput.actions["BreakBlock"].IsPressed();
        }
#endif

        UpdateMining(isHoldingBreak);

        if (_input.placeBlock)
        {
            _input.placeBlock = false;
            PlaceBlock();
        }
    }

    private void UpdateMining(bool isHoldingBreak)
    {
        bool isLookingAtBlock = highlightCube.gameObject.activeSelf || (crossMeshHighlight != null && crossMeshHighlight.activeSelf);

        if (isHoldingBreak && isLookingAtBlock)
        {
            Vector3 targetPos = highlightCube.gameObject.activeSelf ? highlightCube.position : crossMeshHighlight.transform.position;

            if (currentTargetPos != targetPos)
            {
                // Switched to a different block, instantly reset progress
                miningProgress = 0f;
                currentTargetPos = targetPos;
            }

            miningProgress += Time.deltaTime / baseMiningTime;

            if (miningProgress >= 1f)
            {
                // Break the block
                ModifyBlock(currentTargetPos, Block.BlockType.AIR);
                miningProgress = 0f;
                breakOverlay.SetActive(false);
                if (crossMeshOverlay != null) crossMeshOverlay.SetActive(false);
                return;
            }
        }
        else
        {
            // Not holding break OR looking at the sky
            // Gradually reverse (heal) the block's cracks!
            if (miningProgress > 0f)
            {
                // Reverses at the same speed it breaks. You can multiply this to heal faster.
                miningProgress -= Time.deltaTime / baseMiningTime;
                
                if (miningProgress <= 0f)
                {
                    miningProgress = 0f;
                    breakOverlay.SetActive(false);
                    if (crossMeshOverlay != null) crossMeshOverlay.SetActive(false);
                    // Clear any stuck input flags
                    _input.breakBlock = false; 
                    return;
                }
            }
            else
            {
                // Nothing is being mined
                _input.breakBlock = false; 
                return;
            }
        }

        // Update overlay visuals
        if (miningProgress > 0f)
        {
            Block block = GetBlockAt(currentTargetPos);
            GameObject activeOverlay = block.isCrossMesh ? crossMeshOverlay : breakOverlay;
            GameObject inactiveOverlay = block.isCrossMesh ? breakOverlay : crossMeshOverlay;

            if (activeOverlay != null)
            {
                activeOverlay.SetActive(true);
                activeOverlay.transform.position = currentTargetPos;
            }
            if (inactiveOverlay != null)
            {
                inactiveOverlay.SetActive(false);
            }

            int stage = Mathf.Clamp(Mathf.FloorToInt(miningProgress * 10), 0, 9);
            
            if (destroyTextures[stage] != null)
            {
                breakMaterial.mainTexture = destroyTextures[stage];
            }
        }
    }

    private void PickBlock()
    {
        float scroll = _input.scroll;
        _input.scroll = 0;
        // if (scroll > 0 || scroll < 0) Debug.Log("scroll: " + scroll);
        if (scroll > 0) { currentIndex = (currentIndex + 1) % palette.Length; EventManager.OnBlockInHandChange(false); }
        if (scroll < 0) { currentIndex = (currentIndex - 1 + palette.Length) % palette.Length; EventManager.OnBlockInHandChange(true); }
        placeType = palette[currentIndex];
        
    }

    private void HighlightBlock()
    {
        Ray ray = new Ray(Camera.main.transform.position,
        Camera.main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            Vector3 targetPos = hit.point - hit.normal * 0.5f;
            Vector3 center = new Vector3(
                Mathf.RoundToInt(targetPos.x),
                Mathf.RoundToInt(targetPos.y),
                Mathf.RoundToInt(targetPos.z));

            Block block = GetBlockAt(center);

            if (block.isCrossMesh)
            {
                highlightCube.gameObject.SetActive(false);
                if (crossMeshHighlight != null)
                {
                    crossMeshHighlight.transform.position = center;
                    crossMeshHighlight.SetActive(true);
                }
            }
            else
            {
                if (crossMeshHighlight != null) crossMeshHighlight.SetActive(false);
                highlightCube.position = center;
                highlightCube.gameObject.SetActive(true);
            }
        }
        else
        {
            highlightCube.gameObject.SetActive(false);
            if (crossMeshHighlight != null) crossMeshHighlight.SetActive(false);
        }
    }

    /**
     * Modifica um bloco e atualiza as meshes relacionadas.
     * NOTE: Garante também a atualização visual (redraw) dos chunks vizinhos se a alteração ocorrer numa fronteira.
     * 
     * @param worldPos: Posição no mundo onde a alteração deve ocorrer
     * @param type: O novo tipo de bloco a atribuir (...BlockType.AIR para destruir)
     */
    void ModifyBlock(Vector3 worldPos, Block.BlockType type)
    {
        int cs = chunkSize;
        int ch = chunkHeight;

        // obter as coordenadas globais do centro do bloco
        int bx = Mathf.RoundToInt(worldPos.x);
        int by = Mathf.RoundToInt(worldPos.y);
        int bz = Mathf.RoundToInt(worldPos.z);

        // Debug.Log("Global Coords: " + "x: " + bx + "y: " + by + "z: " + bz);

        if (!CheckBoundaries(by)) return;

        // calcular coordenadas do chunk, dividindo pelo tamanho deste
        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / cs),
            Mathf.FloorToInt((float)bz / cs));

        // Obter o chunk nas coordenadas calculadas
        Chunk chunk = worldManager.GetChunk(chunkCoord);
        if (chunk == null) return;

        // calcular coordenadas locais
        int localX = bx - chunkCoord.x * cs;
        int localY = by;
        int localZ = bz - chunkCoord.y * cs;

        // Debug.Log("Local Coords: " + "x: " + localX + "y: " + localY + "z: " + localZ);

        if (localX < 0 || localX >= cs ||
        localY < 0 || localY >= ch ||
        localZ < 0 || localZ >= cs) return;

        Block block = chunk.chunkData[localX, localY, localZ];

        // Se estivermos a destruir/substituir uma flor, temos de avisar o WorldBeeManager para destruir o GameObject associado
        if (block.type == Block.BlockType.FLOWER && WorldBeeManager.Instance != null)
        {
            WorldBeeManager.Instance.DestroyFlowerAt(new Vector3(bx, by, bz));
        }

        block.SetType(type);

        chunk.DrawChunk();
        chunk.BuildCollisionMesh();
        // Redesenhar vizinhos se na fronteira
        if (localX == 0) RedrawNeighbour(chunkCoord + Vector2Int.left);
        if (localX == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.right);
        if (localZ == 0) RedrawNeighbour(chunkCoord + Vector2Int.down);
        if (localZ == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.up);
    }

    /**
     * Valida se a coordenada vertical está dentro dos limites do mundo.
     * 
     * @param y: A altura Y global a validar
     * @return: True se for válida, False se ultrapassar os limites superior ou inferior
     */
    bool CheckBoundaries(int y)
    {
        return !(y <= 0) && !(y >= worldHeight);
    }

    /**
     * Helper para obter um bloco a partir de coordenadas globais.
     */
    private Block GetBlockAt(Vector3 worldPos)
    {
        int cs = chunkSize;
        int ch = chunkHeight;

        int bx = Mathf.RoundToInt(worldPos.x);
        int by = Mathf.RoundToInt(worldPos.y);
        int bz = Mathf.RoundToInt(worldPos.z);

        if (!CheckBoundaries(by)) return new Block(Block.BlockType.AIR, Vector3.zero);

        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / cs),
            Mathf.FloorToInt((float)bz / cs));

        Chunk chunk = worldManager.GetChunk(chunkCoord);
        if (chunk == null) return new Block(Block.BlockType.AIR, Vector3.zero);

        int localX = bx - chunkCoord.x * cs;
        int localY = by;
        int localZ = bz - chunkCoord.y * cs;

        if (localX < 0 || localX >= cs || localY < 0 || localY >= ch || localZ < 0 || localZ >= cs)
            return new Block(Block.BlockType.AIR, Vector3.zero);

        return chunk.chunkData[localX, localY, localZ];
    }

    /**
     * Helper para construir uma malha em X (Cross Mesh) centrada na origem.
     */
    private Mesh BuildCrossMesh()
    {
        Mesh mesh = new Mesh();
        
        // Mantemos o tamanho exato da base, a escala será gerida pela separação dos planos
        float s = 0.505f; 

        Vector3 q1v0 = new Vector3(-s, -s, -s);
        Vector3 q1v1 = new Vector3(s, -s, s);
        Vector3 q1v2 = new Vector3(s, s, s);
        Vector3 q1v3 = new Vector3(-s, s, -s);

        Vector3 q2v0 = new Vector3(-s, -s, s);
        Vector3 q2v1 = new Vector3(s, -s, -s);
        Vector3 q2v2 = new Vector3(s, s, -s);
        Vector3 q2v3 = new Vector3(-s, s, s);

        // O segredo para impedir o Z-Fighting em meshes coplanares:
        // Afastar as faces Front e Back ligeiramente na direção da sua normal, criando uma "sanduíche"
        float offset = 0.01f;
        Vector3 shift1Front = new Vector3(-offset, 0, offset);
        Vector3 shift1Back  = new Vector3(offset, 0, -offset);
        
        Vector3 shift2Front = new Vector3(offset, 0, offset);
        Vector3 shift2Back  = new Vector3(-offset, 0, -offset);

        Vector3[] vertices = {
            // Quad 1 Frente (afastado)
            q1v0 + shift1Front, q1v1 + shift1Front, q1v2 + shift1Front, q1v3 + shift1Front,
            // Quad 1 Trás (afastado para o lado oposto)
            q1v1 + shift1Back,  q1v0 + shift1Back,  q1v3 + shift1Back,  q1v2 + shift1Back,
            
            // Quad 2 Frente (afastado)
            q2v0 + shift2Front, q2v1 + shift2Front, q2v2 + shift2Front, q2v3 + shift2Front,
            // Quad 2 Trás (afastado para o lado oposto)
            q2v1 + shift2Back,  q2v0 + shift2Back,  q2v3 + shift2Back,  q2v2 + shift2Back
        };

        Vector2 uv00 = new Vector2(0, 0);
        Vector2 uv10 = new Vector2(1, 0);
        Vector2 uv01 = new Vector2(0, 1);
        Vector2 uv11 = new Vector2(1, 1);

        Vector2[] uvs = {
            uv00, uv10, uv11, uv01,
            uv00, uv10, uv11, uv01,
            uv00, uv10, uv11, uv01,
            uv00, uv10, uv11, uv01
        };

        int[] triangles = new int[24];
        int[] triTemplate = { 0, 2, 1, 0, 3, 2 };

        for (int q = 0; q < 4; q++)
        {
            for (int i = 0; i < 6; i++)
            {
                triangles[q * 6 + i] = (q * 4) + triTemplate[i];
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    /**
     * Coloca um novo bloco na superfície atingida pelo raycast.
     */
    void PlaceBlock()
    {
        Ray ray = new Ray(Camera.main.transform.position,
            Camera.main.transform.forward);
        // ir para a frente meio bloco para obter a posição correta deste (o hit.pos esta na superficie entre dois blocos)
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            Vector3 targetPos = hit.point + hit.normal * 0.5f;
            int tx = Mathf.RoundToInt(targetPos.x);
            int ty = Mathf.RoundToInt(targetPos.y);
            int tz = Mathf.RoundToInt(targetPos.z);

            // Verificar se o bloco alvo colide com o espaço ocupado pelo jogador
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                Bounds playerBounds = cc.bounds;
                // Bounding box do bloco alvo (cubo unitário centrado na posição inteira)
                Bounds blockBounds = new Bounds(new Vector3(tx, ty, tz), Vector3.one);
                if (playerBounds.Intersects(blockBounds))
                    return;
            }

            ModifyBlock(targetPos, placeType);
        }
    }

    /**
     * Antigo metodo BreakBlock removido para dar lugar ao MineBlock sustentado.
     */

    /**
     * Solicita a um chunk vizinho que reconstrua a sua malha (Mesh).
     * 
     * @param coord: Coordenada do chunk vizinho a atualizar
     */
    void RedrawNeighbour(Vector2Int coord)
    {
        Chunk c = worldManager.GetChunk(coord);
        if (c != null) 
        {
            c.DrawChunk();
            c.BuildCollisionMesh();
        }
    }
}

