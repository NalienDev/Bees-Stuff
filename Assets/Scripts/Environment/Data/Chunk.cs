using System.Collections.Generic;
using UnityEngine;
using static Config;


[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    [Header("Chunk")]
    [Tooltip("Matriz 3D que armazena todas as instâncias de blocos gerados neste chunk")]
    public Block[,,] chunkData;
    [Tooltip("Material aplicado à malha gerada — deve conter o atlas de texturas dos blocos")]
    public Material chunkMaterial;
    [Tooltip("Coordenada do chunk no mundo em unidades de chunk — usado para calcular posições globais")]
    public Vector2Int worldOffset;

    [Header("Terreno")]
    [Tooltip("Escala do ruído FBm base — valores menores criam relevos mais suaves e extensos, valores maiores criam relevos mais acidentados")]
    public float scale = 0.05f;
    [Tooltip("Número de camadas de ruído FBm sobrepostas — valores maiores adicionam mais detalhe topográfico ao terreno")]
    public int octaves = 4;
    [Tooltip("Escala do ruído 3D de densidade volumétrica — controla a variação orgânica da superfície e criação de saliências")]
    public float densityScale = 0.02f;
    [Tooltip("Limiar de densidade para decidir se um bloco é sólido — valores mais altos criam menos terreno sólido")]
    public float densityThreshold = 0f;
    [Tooltip("Altura máxima em blocos até onde o terreno sólido base pode ser gerado")]
    public int maxSolidHeight = 26;
    [Tooltip("Altura do mar")]
    public int seaLevel = 8;
    [Tooltip("Amplitude a que é gerado detalhe no terreno")]
    public int detailAmplitude = 3;
    [Tooltip("Peso do Perlin3D de detalhe — valores maiores criam mais overhangs e variação vertical")]
    public float detailWeight = 8f;

    [Header("Grutas")]
    [Tooltip("Escala do ruído 3D para escavação — valores maiores criam grutas mais pequenas e frequentes")]
    public float caveScale = 0.2f;
    [Tooltip("Limiar que determina a frequência de escavação — valores mais altos criam menos grutas")]
    public float caveThreshold = 0.4f;
    [Tooltip("Margem de segurança para evitar escavação nos limites verticais do chunk")]
    public int margin = 2;
    [Tooltip("Altura máxima permitida para a superfície base")]
    public int maxSurfaceHeight = 5;

    [Header("Worms")]
    [Tooltip("Número de worms gerados por chunk — valores altos criam mais túneis mas aumentam o custo de geração")]
    public int wormsPerChunk = 1;
    [Tooltip("Número de passos que cada worm percorre — valores maiores criam túneis mais longos")]
    public int steps = 200;
    [Tooltip("Raio da esfera de escavação em cada passo do worm")]
    public float radius = 3f;
    [Tooltip("Distância percorrida pelo worm em cada passo — valores maiores criam túneis mais espaçados")]
    public float stepSize = 10f;
    [Tooltip("Escala do ruído de direcção do worm — valores menores criam túneis mais suaves e curvos")]
    public float directionScale = 0.05f;

    [Header("Cross-Chunk Culling")]

    public WorldManager worldManager;

    [System.NonSerialized]
    public bool isFullyBuilt = false;


    public void Initialize(Vector2Int offset, Material mat, WorldManager manager = null)
    {
        worldOffset = offset;
        chunkMaterial = mat;
        worldManager = manager;

    }


    void InitializeChunk()
    {
        chunkData = new Block[chunkSize, chunkHeight, chunkSize];
        Biome[,] columnBiomes = new Biome[chunkSize, chunkSize];
        GetColumnBiomes(columnBiomes);

        int[,] surfaceHeight = new int[chunkSize, chunkSize];


        GetColumnSurfaceHeight(surfaceHeight, columnBiomes);

        CreateInitialChunkData(surfaceHeight, columnBiomes);

        CaveGenerator.GenerateWorms(chunkData, chunkSize, chunkHeight, worldOffset, wormsPerChunk, steps, radius, stepSize, directionScale);

        GenerateWater();

        UpdateExposedDirt(columnBiomes);

        TreeGenerator.PlantTrees(chunkData, worldOffset, seaLevel, columnBiomes);
        VegetationGenerator.PlantVegetation(chunkData, worldOffset, seaLevel, columnBiomes);
    }


    public void GenerateChunkData()
    {
        Biome[,] columnBiomes = new Biome[chunkSize, chunkSize];
        GetColumnBiomes(columnBiomes);

        int[,] surfaceHeight = new int[chunkSize, chunkSize];
        GetColumnSurfaceHeight(surfaceHeight, columnBiomes);
        CreateInitialChunkData(surfaceHeight, columnBiomes);
        CaveGenerator.GenerateWorms(chunkData, chunkSize, chunkHeight, worldOffset, wormsPerChunk, steps, radius, stepSize, directionScale);
        GenerateWater();
        TextureCaveWalls(surfaceHeight);
        UpdateExposedDirt(columnBiomes);
        TreeGenerator.PlantTrees(chunkData, worldOffset, seaLevel, columnBiomes);
        VegetationGenerator.PlantVegetation(chunkData, worldOffset, seaLevel, columnBiomes);
    }

    private void GetColumnBiomes(Biome[,] columnBiomes)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float globalX = worldOffset.x * chunkSize + x + Config.seedOffsetX;
                float globalZ = worldOffset.y * chunkSize + z + Config.seedOffsetZ;
                columnBiomes[x, z] = BiomeManager.GetBiomeAt(globalX, globalZ);
            }
        }
    }

    private void GetBlendedTerrain(float globalX, float globalZ, out float b_scale, out int b_octaves, out float b_maxSolidHeight, out float b_detailWeight)
    {
        Biome b1 = BiomeManager.GetBiomeAt(globalX, globalZ);
        Biome b2 = BiomeManager.GetBiomeAt(globalX - 8, globalZ);
        Biome b3 = BiomeManager.GetBiomeAt(globalX + 8, globalZ);
        Biome b4 = BiomeManager.GetBiomeAt(globalX, globalZ - 8);
        Biome b5 = BiomeManager.GetBiomeAt(globalX, globalZ + 8);

        b_scale = (b1.scale + b2.scale + b3.scale + b4.scale + b5.scale) / 5f;
        b_octaves = Mathf.RoundToInt((b1.octaves + b2.octaves + b3.octaves + b4.octaves + b5.octaves) / 5f);
        b_maxSolidHeight = (b1.maxSolidHeight + b2.maxSolidHeight + b3.maxSolidHeight + b4.maxSolidHeight + b5.maxSolidHeight) / 5f;
        b_detailWeight = (b1.detailWeight + b2.detailWeight + b3.detailWeight + b4.detailWeight + b5.detailWeight) / 5f;
    }


    private void GetColumnSurfaceHeight(int[,] surfaceHeight, Biome[,] columnBiomes)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                surfaceHeight[x, z] = 0;
                float globalX = worldOffset.x * chunkSize + x + Config.seedOffsetX;
                float globalZ = worldOffset.y * chunkSize + z + Config.seedOffsetZ;

                GetBlendedTerrain(globalX, globalZ, out float b_scale, out int b_octaves, out float b_maxSolidHeight, out float b_detailWeight);

                float continentalness = NoiseUtils.FBm(globalX, globalZ, b_octaves / 2, densityScale / 4);
                float baseHeight = NoiseUtils.FBm(globalX, globalZ, b_octaves, densityScale);
                float detail = NoiseUtils.FBm(globalX, globalZ, (b_octaves / 2) * 3, densityScale * 5);
                float h = Mathf.Lerp(seaLevel, b_maxSolidHeight, continentalness * baseHeight) + detail * detailAmplitude;

                for (int y = chunkHeight - 1; y >= 0; y--)
                {
                    float d = NoiseUtils.Perlin3D(globalX * densityScale, y * densityScale, globalZ * densityScale);
                    if ((h - y) + d * b_detailWeight > 0f)
                    {
                        surfaceHeight[x, z] = y;
                        break;
                    }
                }
            }
        }
    }


    private void CreateInitialChunkData(int[,] surfaceHeight, Biome[,] columnBiomes)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float globalX = worldOffset.x * chunkSize + x + Config.seedOffsetX;
                float globalZ = worldOffset.y * chunkSize + z + Config.seedOffsetZ;
                Biome currentBiome = columnBiomes[x, z];

                GetBlendedTerrain(globalX, globalZ, out float b_scale, out int b_octaves, out float b_maxSolidHeight, out float b_detailWeight);

                float continentalness = NoiseUtils.FBm(globalX, globalZ, b_octaves / 2, densityScale / 4);
                float baseHeight = NoiseUtils.FBm(globalX, globalZ, b_octaves, densityScale);
                float detail = NoiseUtils.FBm(globalX, globalZ, (b_octaves / 2) * 3, densityScale * 5);
                float h = Mathf.Lerp(seaLevel, b_maxSolidHeight, continentalness * baseHeight) + detail * detailAmplitude;

                for (int y = 0; y < chunkHeight; y++)
                {

                    float densityNoise = NoiseUtils.Perlin3D(globalX * densityScale, y * densityScale, globalZ * densityScale);
                    float finalDensity = (h - y) + densityNoise * b_detailWeight;
                    bool solid = finalDensity > 0f;


                    if (solid && y > 1 && y < maxSurfaceHeight - margin && currentBiome.allowCaves)
                    {
                        float cx = globalX * caveScale;
                        float cy = y * caveScale;
                        float cz = globalZ * caveScale;
                        float caveNoise = NoiseUtils.Perlin3D(cx, cy, cz);
                        if (caveNoise > caveThreshold)
                            solid = false;
                    }


                    float densityNoiseAbove = NoiseUtils.Perlin3D(globalX * densityScale, (y + 1) * densityScale, globalZ * densityScale);
                    float finalDensityAbove = (h - (y + 1)) + densityNoiseAbove * b_detailWeight;
                    bool surfaceBlock = finalDensityAbove <= 0f;


                    Block.BlockType type;


                    float layerNoise = NoiseUtils.Perlin3D(globalX * 0.1f, y * 0.1f, globalZ * 0.1f) * 4f;
                    int depthFromSurface = surfaceHeight[x, z] - y;

                    if (!solid)
                    {
                        type = Block.BlockType.AIR;
                    }
                    else if (y <= 1 + (NoiseUtils.Perlin3D(globalX * 0.5f, y * 0.5f, globalZ * 0.5f) * 2f))
                    {

                        type = Block.BlockType.BEDROCK;
                    }
                    else if (finalDensity > 25f + layerNoise)
                    {

                        type = Block.BlockType.COBBLESTONE;
                    }
                    else if (finalDensity > 3.5f + (layerNoise * 0.5f))
                    {

                        type = Block.BlockType.STONE;
                    }
                    else
                    {

                        type = currentBiome.subSurfaceBlock;
                    }



                    if ((type == Block.BlockType.DIRT || type == Block.BlockType.SAND) && surfaceBlock)
                    {
                        type = currentBiome.surfaceBlock;
                    }

                    chunkData[x, y, z] = new Block(type, new Vector3(x, y, z));
                }
            }
        }
    }


    private void GenerateWater()
    {

        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (chunkData[x, y, z].type == Block.BlockType.AIR && y < seaLevel)
                        chunkData[x, y, z] = new Block(Block.BlockType.WATER, new Vector3(x, y, z));
                }
    }


    private void TextureCaveWalls(int[,] surfaceHeight)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                for (int y = 0; y < chunkHeight; y++)
                {
                    if (CaveGenerator.IsAir(chunkData, x, y, z)) continue;


                    if (y >= surfaceHeight[x, z]) continue;

                    bool nextToCarvedAir = CaveGenerator.HasCarvedAirNeighbour(
                        chunkData, chunkSize, chunkHeight, worldOffset, densityScale, octaves, scale, x, y, z);
                    if (nextToCarvedAir)
                        chunkData[x, y, z] = new Block(Block.BlockType.STONE, new Vector3(x, y, z));
                }
            }
        }
    }


    private void UpdateExposedDirt(Biome[,] columnBiomes)
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                Biome currentBiome = columnBiomes[x, z];
                for (int y = 0; y < chunkHeight; y++)
                {
                    Block.BlockType bt = chunkData[x, y, z].type;
                    if (bt == Block.BlockType.DIRT || bt == Block.BlockType.SAND)
                    {
                        bool exposedTop   = y < chunkHeight - 1 && chunkData[x, y + 1, z].type == Block.BlockType.AIR;
                        bool exposedFront = z < chunkSize - 1   && chunkData[x, y, z + 1].type == Block.BlockType.AIR;
                        bool exposedBack  = z > 0               && chunkData[x, y, z - 1].type == Block.BlockType.AIR;
                        bool exposedRight = x < chunkSize - 1   && chunkData[x + 1, y, z].type == Block.BlockType.AIR;
                        bool exposedLeft  = x > 0               && chunkData[x - 1, y, z].type == Block.BlockType.AIR;

                        if (exposedTop || exposedFront || exposedBack || exposedRight || exposedLeft)
                        {
                            chunkData[x, y, z].type = currentBiome.surfaceBlock;
                        }
                    }
                }
            }
        }
    }


    public void BuildCollisionMesh()
    {

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();


        List<Vector3> leavesVerts = new List<Vector3>();
        List<int> leavesTris = new List<int>();
        List<Vector2> leavesUvs = new List<Vector2>();

        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];

                    if (!block.isSolid) continue;

                    bool isLeaf = (block.type == Block.BlockType.LEAVES);
                    var vList = isLeaf ? leavesVerts : vertices;
                    var tList = isLeaf ? leavesTris : triangles;
                    var uList = isLeaf ? leavesUvs : uvs;

                    if (!HasSolidNeighbour(x, y, z + 1)) block.AddFaceToMeshData(Block.CubeFace.Front, vList, tList, uList);
                    if (!HasSolidNeighbour(x, y, z - 1)) block.AddFaceToMeshData(Block.CubeFace.Back, vList, tList, uList);
                    if (!HasSolidNeighbour(x, y + 1, z)) block.AddFaceToMeshData(Block.CubeFace.Top, vList, tList, uList);
                    if (!HasSolidNeighbour(x, y - 1, z)) block.AddFaceToMeshData(Block.CubeFace.Bottom, vList, tList, uList);
                    if (!HasSolidNeighbour(x - 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Left, vList, tList, uList);
                    if (!HasSolidNeighbour(x + 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Right, vList, tList, uList);
                }


        Mesh collisionMesh = new Mesh();
        collisionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        collisionMesh.vertices = vertices.ToArray();
        collisionMesh.triangles = triangles.ToArray();
        collisionMesh.RecalculateBounds();

        MeshCollider col = gameObject.GetComponent<MeshCollider>();
        if (col == null) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = collisionMesh;


        BuildLeavesCollider(leavesVerts, leavesTris);


        BuildVegetationColliders();
    }





    private void BuildLeavesCollider(List<Vector3> verts, List<int> tris)
    {

        Transform leavesChild = transform.Find("LeavesCollider");
        GameObject leavesGO;
        if (leavesChild != null)
        {
            leavesGO = leavesChild.gameObject;
        }
        else
        {
            leavesGO = new GameObject("LeavesCollider");
            leavesGO.transform.SetParent(transform, false);
            leavesGO.layer = LayerMask.NameToLayer("Leaves");
        }

        if (verts.Count == 0)
        {

            MeshCollider mc = leavesGO.GetComponent<MeshCollider>();
            if (mc != null) DestroyImmediate(mc);
            return;
        }

        Mesh leavesMesh = new Mesh();
        leavesMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        leavesMesh.vertices = verts.ToArray();
        leavesMesh.triangles = tris.ToArray();
        leavesMesh.RecalculateBounds();

        MeshCollider leavesCol = leavesGO.GetComponent<MeshCollider>();
        if (leavesCol == null) leavesCol = leavesGO.AddComponent<MeshCollider>();
        leavesCol.sharedMesh = leavesMesh;
    }

    private void BuildVegetationColliders()
    {
        Transform vegChild = transform.Find("VegetationCollider");
        GameObject vegGO;
        if (vegChild != null)
        {
            vegGO = vegChild.gameObject;
        }
        else
        {
            vegGO = new GameObject("VegetationCollider");
            vegGO.transform.SetParent(transform, false);
        }


        BoxCollider[] existing = vegGO.GetComponents<BoxCollider>();
        foreach (var col in existing)
        {
            DestroyImmediate(col);
        }

        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (block.isCrossMesh)
                    {
                        BoxCollider bc = vegGO.AddComponent<BoxCollider>();
                        bc.center = block.position;
                        bc.size = new Vector3(1f, 1f, 1f);
                        bc.isTrigger = true;
                    }
                }
    }


    public void DrawChunk()
    {

        List<Vector3> sharedVertices = new List<Vector3>();
        List<int> sharedTriangles = new List<int>();
        List<Vector2> sharedUvs = new List<Vector2>();


        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (block.isCrossMesh)
                    {
                        block.AddCrossToMeshData(sharedVertices, sharedTriangles, sharedUvs);
                        continue;
                    }
                    if (!block.isSolid) continue;
                    if (!IsOpaqueOrSameNeighbour(x, y, z + 1, block.type)) block.AddFaceToMeshData(Block.CubeFace.Front, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x, y, z - 1, block.type)) block.AddFaceToMeshData(Block.CubeFace.Back, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x, y + 1, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Top, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x, y - 1, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Bottom, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x - 1, y, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Left, sharedVertices, sharedTriangles, sharedUvs);
                    if (!IsOpaqueOrSameNeighbour(x + 1, y, z, block.type)) block.AddFaceToMeshData(Block.CubeFace.Right, sharedVertices, sharedTriangles, sharedUvs);
                }


        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                {
                    Block block = chunkData[x, y, z];
                    if (block.type != Block.BlockType.WATER) continue;
                    if (IsAir(x, y, z + 1)) block.AddFaceToMeshData(Block.CubeFace.Front, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x, y, z - 1)) block.AddFaceToMeshData(Block.CubeFace.Back, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x, y + 1, z)) block.AddFaceToMeshData(Block.CubeFace.Top, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x, y - 1, z)) block.AddFaceToMeshData(Block.CubeFace.Bottom, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x - 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Left, sharedVertices, sharedTriangles, sharedUvs);
                    if (IsAir(x + 1, y, z)) block.AddFaceToMeshData(Block.CubeFace.Right, sharedVertices, sharedTriangles, sharedUvs);
                }


        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = sharedVertices.ToArray();
        mesh.triangles = sharedTriangles.ToArray();
        mesh.uv = sharedUvs.ToArray();


        mesh.RecalculateNormals();
        mesh.RecalculateBounds();


        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
        mr.material = chunkMaterial;

        mr.enabled = true;
    }

    bool IsAir(int x, int y, int z)
    {
        if (y < 0 || y >= chunkHeight) return true;
        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            return chunkData[x, y, z].type == Block.BlockType.AIR;
        if (worldManager == null) return true;

        Vector2Int neighbourOffset = worldOffset;
        int localX = x;
        int localZ = z;

        if (x < 0) { neighbourOffset += Vector2Int.left; localX = chunkSize - 1; }
        else if (x >= chunkSize) { neighbourOffset += Vector2Int.right; localX = 0; }

        if (z < 0) { neighbourOffset += Vector2Int.down; localZ = chunkSize - 1; }
        else if (z >= chunkSize) { neighbourOffset += Vector2Int.up; localZ = 0; }

        Chunk neighbour = worldManager.GetChunk(neighbourOffset);
        if (neighbour == null || neighbour.chunkData == null) return true;

        return neighbour.chunkData[localX, y, localZ].type == Block.BlockType.AIR;
    }


    bool HasSolidNeighbour(int x, int y, int z)
    {

        if (y < 0 || y >= chunkHeight)
            return false;


        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
            return chunkData[x, y, z].isSolid;



        if (worldManager == null)
            return false;


        Vector2Int neighbourOffset = worldOffset;
        int localX = x;
        int localZ = z;

        if (x < 0)
        {
            neighbourOffset += Vector2Int.left;
            localX = chunkSize - 1;
        }
        else if (x >= chunkSize)
        {
            neighbourOffset += Vector2Int.right;
            localX = 0;
        }

        if (z < 0)
        {
            neighbourOffset += Vector2Int.down;
            localZ = chunkSize - 1;
        }
        else if (z >= chunkSize)
        {
            neighbourOffset += Vector2Int.up;
            localZ = 0;
        }

        Chunk neighbour = worldManager.GetChunk(neighbourOffset);


        if (neighbour == null || neighbour.chunkData == null)
            return false;

        return neighbour.chunkData[localX, y, localZ].isSolid;
    }


    bool IsOpaqueOrSameNeighbour(int x, int y, int z, Block.BlockType myType)
    {
        if (y < 0 || y >= chunkHeight)
            return false;

        if (x >= 0 && x < chunkSize && z >= 0 && z < chunkSize)
        {
            Block b = chunkData[x, y, z];
            return b.isSolid && (!b.isTranslucent || b.type == myType);
        }

        if (worldManager == null)
            return false;

        Vector2Int neighbourOffset = worldOffset;
        int localX = x;
        int localZ = z;

        if (x < 0) { neighbourOffset += Vector2Int.left; localX = chunkSize - 1; }
        else if (x >= chunkSize) { neighbourOffset += Vector2Int.right; localX = 0; }

        if (z < 0) { neighbourOffset += Vector2Int.down; localZ = chunkSize - 1; }
        else if (z >= chunkSize) { neighbourOffset += Vector2Int.up; localZ = 0; }

        Chunk neighbour = worldManager.GetChunk(neighbourOffset);
        if (neighbour == null || neighbour.chunkData == null)
            return false;

        Block nb = neighbour.chunkData[localX, y, localZ];
        return nb.isSolid && (!nb.isTranslucent || nb.type == myType);
    }
}

