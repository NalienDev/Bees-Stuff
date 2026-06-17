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

    private void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
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
        if (_input.breakBlock)
        {
            _input.breakBlock = false;
            BreakBlock();
        }

        if (_input.placeBlock)
        {
            _input.placeBlock = false;
            PlaceBlock();
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
            Vector3 center = hit.point - hit.normal * 0.5f;
            highlightCube.position = new Vector3(
            Mathf.RoundToInt(center.x),
            Mathf.RoundToInt(center.y),
            Mathf.RoundToInt(center.z));
            highlightCube.gameObject.SetActive(true);
        }
        else
        {
            highlightCube.gameObject.SetActive(false);
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
        block.type = type;
        block.isSolid = (type != Block.BlockType.AIR);

        chunk.DrawChunk();
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
     * Coloca um novo bloco na superfície atingida pelo raycast.
     */
    void PlaceBlock()
    {
        Ray ray = new Ray(Camera.main.transform.position,
            Camera.main.transform.forward);
        // ir para a frente meio bloco para obter a posição correta deste (o hit.pos esta na superficie entre dois blocos)
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            ModifyBlock(hit.point + hit.normal * 0.5f, placeType);
    }

    /**
     * Destrói o bloco que foi atingido pelo raycast.
     */
    public void BreakBlock()
    {
        Ray ray = new Ray(Camera.main.transform.position,
            Camera.main.transform.forward);
        // Recuar meio bloco para obter a posição correta deste (o hit.pos esta na superficie entre dois blocos)
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            ModifyBlock(hit.point - hit.normal * 0.5f, Block.BlockType.AIR);
    }

    /**
     * Solicita a um chunk vizinho que reconstrua a sua malha (Mesh).
     * 
     * @param coord: Coordenada do chunk vizinho a atualizar
     */
    void RedrawNeighbour(Vector2Int coord)
    {
        Chunk c = worldManager.GetChunk(coord);
        if (c != null) c.DrawChunk();
    }
}
