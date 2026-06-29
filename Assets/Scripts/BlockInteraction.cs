using StarterAssets;
using UnityEngine;
using static Config;


public class BlockInteraction : MonoBehaviour
{
    [Tooltip("Referência a WorldManager. Necessária para aceder aos chunks já carregados.")]
    public WorldManager worldManager;
    [Tooltip("Distância máxima (em blocos) que o jogador consegue interagir.")]
    public float maxDistance = 6f;

    private StarterAssetsInputs input;
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
    private UnityEngine.InputSystem.PlayerInput playerInput;
#endif

    private void Start()
    {
        input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
        playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
#endif


        breakOverlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(breakOverlay.GetComponent<Collider>());
        breakOverlay.transform.localScale = new Vector3(1.02f, 1.02f, 1.02f);

        breakMaterial = new Material(Shader.Find("Unlit/Transparent"));
        breakOverlay.GetComponent<MeshRenderer>().material = breakMaterial;
        breakOverlay.SetActive(false);


        destroyTextures = new Texture2D[10];
        for (int i = 0; i < 10; i++)
        {
            destroyTextures[i] = Resources.Load<Texture2D>("Destroy/destroy_stage_" + i);
            if (destroyTextures[i] != null)
            {

                destroyTextures[i].filterMode = FilterMode.Point;
            }
        }


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


    private void Update()
    {
        PickBlock();
        HighlightBlock();
        DetectAction();
    }
    private void DetectAction()
    {
        bool isHoldingBreak = input.breakBlock;

#if ENABLE_INPUT_SYSTEM

        if (playerInput != null)
        {
            isHoldingBreak = playerInput.actions["BreakBlock"].IsPressed();
        }
#endif

        UpdateMining(isHoldingBreak);

        if (input.placeBlock)
        {
            input.placeBlock = false;
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

                miningProgress = 0f;
                currentTargetPos = targetPos;
            }

            miningProgress += Time.deltaTime / baseMiningTime;

            if (miningProgress >= 1f)
            {

                ModifyBlock(currentTargetPos, Block.BlockType.AIR);
                miningProgress = 0f;
                breakOverlay.SetActive(false);
                if (crossMeshOverlay != null) crossMeshOverlay.SetActive(false);
                return;
            }
        }
        else
        {


            if (miningProgress > 0f)
            {

                miningProgress -= Time.deltaTime / baseMiningTime;

                if (miningProgress <= 0f)
                {
                    miningProgress = 0f;
                    breakOverlay.SetActive(false);
                    if (crossMeshOverlay != null) crossMeshOverlay.SetActive(false);

                    input.breakBlock = false;
                    return;
                }
            }
            else
            {

                input.breakBlock = false;
                return;
            }
        }


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
        float scroll = input.scroll;
        input.scroll = 0;

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


    void ModifyBlock(Vector3 worldPos, Block.BlockType type)
    {
        int cs = chunkSize;
        int ch = chunkHeight;


        int bx = Mathf.RoundToInt(worldPos.x);
        int by = Mathf.RoundToInt(worldPos.y);
        int bz = Mathf.RoundToInt(worldPos.z);



        if (!CheckBoundaries(by)) return;


        Vector2Int chunkCoord = new Vector2Int(
            Mathf.FloorToInt((float)bx / cs),
            Mathf.FloorToInt((float)bz / cs));


        Chunk chunk = worldManager.GetChunk(chunkCoord);
        if (chunk == null) return;


        int localX = bx - chunkCoord.x * cs;
        int localY = by;
        int localZ = bz - chunkCoord.y * cs;



        if (localX < 0 || localX >= cs ||
        localY < 0 || localY >= ch ||
        localZ < 0 || localZ >= cs) return;

        Block block = chunk.chunkData[localX, localY, localZ];


        if (block.type == Block.BlockType.FLOWER && WorldBeeManager.Instance != null)
        {
            WorldBeeManager.Instance.DestroyFlowerAt(new Vector3(bx, by, bz));
        }

        block.SetType(type);

        chunk.DrawChunk();
        chunk.BuildCollisionMesh();

        if (localX == 0) RedrawNeighbour(chunkCoord + Vector2Int.left);
        if (localX == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.right);
        if (localZ == 0) RedrawNeighbour(chunkCoord + Vector2Int.down);
        if (localZ == cs - 1) RedrawNeighbour(chunkCoord + Vector2Int.up);
    }


    bool CheckBoundaries(int y)
    {
        return !(y <= 0) && !(y >= worldHeight);
    }


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


    private Mesh BuildCrossMesh()
    {
        Mesh mesh = new Mesh();


        float s = 0.505f;

        Vector3 q1v0 = new Vector3(-s, -s, -s);
        Vector3 q1v1 = new Vector3(s, -s, s);
        Vector3 q1v2 = new Vector3(s, s, s);
        Vector3 q1v3 = new Vector3(-s, s, -s);

        Vector3 q2v0 = new Vector3(-s, -s, s);
        Vector3 q2v1 = new Vector3(s, -s, -s);
        Vector3 q2v2 = new Vector3(s, s, -s);
        Vector3 q2v3 = new Vector3(-s, s, s);



        float offset = 0.01f;
        Vector3 shift1Front = new Vector3(-offset, 0, offset);
        Vector3 shift1Back = new Vector3(offset, 0, -offset);

        Vector3 shift2Front = new Vector3(offset, 0, offset);
        Vector3 shift2Back = new Vector3(-offset, 0, -offset);

        Vector3[] vertices = {

            q1v0 + shift1Front, q1v1 + shift1Front, q1v2 + shift1Front, q1v3 + shift1Front,

            q1v1 + shift1Back,  q1v0 + shift1Back,  q1v3 + shift1Back,  q1v2 + shift1Back,


            q2v0 + shift2Front, q2v1 + shift2Front, q2v2 + shift2Front, q2v3 + shift2Front,

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


    void PlaceBlock()
    {
        Ray ray = new Ray(Camera.main.transform.position,
            Camera.main.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            Vector3 targetPos = hit.point + hit.normal * 0.5f;
            int tx = Mathf.RoundToInt(targetPos.x);
            int ty = Mathf.RoundToInt(targetPos.y);
            int tz = Mathf.RoundToInt(targetPos.z);


            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                Bounds playerBounds = cc.bounds;

                Bounds blockBounds = new Bounds(new Vector3(tx, ty, tz), Vector3.one);
                if (playerBounds.Intersects(blockBounds))
                    return;
            }

            ModifyBlock(targetPos, placeType);
        }
    }




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

